using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Multiplayer
{
    [RequireComponent(typeof(VoiceNetworkObject))]
    public sealed class MarsProximityVoice : MonoBehaviour
    {
        [Min(1)] public float hearingDistance = 22f;
        public bool pushToTalk = true;
        public Key pushToTalkKey = Key.V;
        private VoiceNetworkObject voice;
        private MarsFusionAvatar avatar;
        private AudioSource output;
        private Recorder recorder;

        public static float MasterVoiceVolume = 1f;
        public static bool LocalPushToTalk = true;
        private float individualVolume = 1f;
        private bool individuallyMuted;
        private int appliedRevision = -1;
        private int appliedCaptureRevision = -1;
        private string[] availableMicrophones = System.Array.Empty<string>();
        private float nextDeviceCheck;
        private string knownDevices;
        private string loadedPreferenceKey;
        private bool suspendedRecording;
        public bool TalkPressed { get; private set; }
        public bool IsTransmitting => voice != null && voice.IsLocal && recorder != null && recorder.IsCurrentlyTransmitting;
        public float InputLevel => recorder != null && recorder.LevelMeter != null ? recorder.LevelMeter.CurrentAvgAmp : 0;
        public string CaptureStatus { get; private set; } = "Waiting for local microphone";
        public bool IndividuallyMuted => individuallyMuted;
        public string VolumePreferenceKey
        {
            get
            {
                if (voice == null || voice.Object == null || !voice.Object.IsValid || voice.Runner == null) return null;
                var owner = voice.Runner.GameMode == GameMode.Shared ? voice.Object.StateAuthority : voice.Object.InputAuthority;
                string id = voice.Runner.GetPlayerUserId(owner);
                if (string.IsNullOrEmpty(id)) return null;
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    return "GameJam.PeerVolume." + System.BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id))).Replace("-", "").Substring(0, 24);
            }
        }
        public void SetMuted(bool muted)
        {
            individuallyMuted = muted;
            if (loadedPreferenceKey != null) PlayerPrefs.SetInt(loadedPreferenceKey + ".Muted", muted ? 1 : 0);
            PlayerPrefs.Save();
        }
        public void SuspendForLocalTest(bool suspended)
        {
            if (recorder == null || voice == null || voice.Object == null || !voice.Object.IsValid || !voice.IsLocal) return;
            recorder.TransmitEnabled = false;
            recorder.RecordingEnabled = !suspended;
            suspendedRecording = suspended;
            if (!suspended) appliedCaptureRevision = -1;
        }
        public void RefreshCapture()
        {
            if (recorder == null || voice == null || !voice.IsLocal) return;
            recorder.TransmitEnabled = false;
            recorder.UseMicrophoneTypeFallback = true;
            recorder.VoiceDetection = !pushToTalk;
            recorder.VoiceDetectionThreshold = VoicePreferences.Threshold;
            if (!string.IsNullOrEmpty(VoicePreferences.Device))
            {
                recorder.MicrophoneType = Recorder.MicType.Unity;
                recorder.MicrophoneDevice = new Photon.Voice.DeviceInfo(VoicePreferences.Device);
            }
            else recorder.MicrophoneDevice = Photon.Voice.DeviceInfo.Default;
            if (!VoicePreferences.TestingMicrophone) recorder.RestartRecording();
            appliedRevision = VoicePreferences.Revision;
            appliedCaptureRevision = VoicePreferences.CaptureRevision;
        }

        public float IndividualVolume => individualVolume;
        public bool IsSpeaking => voice != null && (voice.IsLocal
            ? (recorder != null && recorder.IsCurrentlyTransmitting)
            : (output != null && output.isPlaying && !output.mute));

        public void SetIndividualVolume(float volume)
        {
            individualVolume = Mathf.Clamp01(volume);
            if (output != null) output.volume = individualVolume * MasterVoiceVolume;
            if (loadedPreferenceKey != null) { PlayerPrefs.SetFloat(loadedPreferenceKey, individualVolume); PlayerPrefs.Save(); }
        }

        public void SetPushToTalk(bool ptt)
        {
            pushToTalk = ptt;
            if (recorder != null) { recorder.VoiceDetection = !ptt; recorder.VoiceDetectionThreshold = VoicePreferences.Threshold; }
            if (recorder != null && !ptt) recorder.TransmitEnabled = !VoicePreferences.InputSuspended && (avatar == null || avatar.IsAlive);
        }

        private void Awake()
        {
            voice = GetComponent<VoiceNetworkObject>();
            avatar = GetComponent<MarsFusionAvatar>();
            recorder = GetComponentInChildren<Recorder>(true);
            pushToTalk = LocalPushToTalk = PlayerPrefs.GetInt("GameJam.VoiceMode", 1) == 1;
            MasterVoiceVolume = PlayerPrefs.GetFloat("GameJam.MasterVoiceVolume", 1f);

            if (recorder != null && pushToTalk) recorder.TransmitEnabled = false;
            var speaker = GetComponentInChildren<Speaker>(true);
            output = speaker != null ? speaker.GetComponent<AudioSource>() : null;
            if (output != null)
            {
                output.spatialBlend = 1;
                output.rolloffMode = AudioRolloffMode.Linear;
                output.minDistance = 2f;
                output.maxDistance = hearingDistance;
                output.dopplerLevel = 0;
                output.volume = individualVolume * MasterVoiceVolume;
            }
        }

        private void Update()
        {
            if (voice.Object == null || !voice.Object.IsValid) return;
            if (voice.IsLocal)
            {
                if (recorder == null) return;
                pushToTalk = LocalPushToTalk;
                pushToTalkKey = VoicePreferences.TalkKey;
                if (VoicePreferences.TestingMicrophone && !suspendedRecording)
                { recorder.TransmitEnabled = false; recorder.RecordingEnabled = false; suspendedRecording = true; }
                else if (!VoicePreferences.TestingMicrophone && suspendedRecording)
                { recorder.RecordingEnabled = true; suspendedRecording = false; appliedCaptureRevision = -1; }
                if (Time.unscaledTime >= nextDeviceCheck)
                {
                    nextDeviceCheck = Time.unscaledTime + 2f;
                    availableMicrophones = Microphone.devices;
                    string devices = string.Join("|", availableMicrophones);
                    if (knownDevices != devices) { knownDevices = devices; appliedCaptureRevision = -1; }
                }
                if (appliedCaptureRevision != VoicePreferences.CaptureRevision && !VoicePreferences.TestingMicrophone) RefreshCapture();
                if (appliedRevision != VoicePreferences.Revision)
                {
                    recorder.VoiceDetection = !pushToTalk;
                    recorder.VoiceDetectionThreshold = VoicePreferences.Threshold;
                    appliedRevision = VoicePreferences.Revision;
                }
                bool deviceAvailable = availableMicrophones.Length > 0 &&
                    (string.IsNullOrEmpty(VoicePreferences.Device) || System.Array.IndexOf(availableMicrophones, VoicePreferences.Device) >= 0);
                TalkPressed = Keyboard.current != null && Keyboard.current[pushToTalkKey].isPressed;
                bool transmit = deviceAvailable && !VoicePreferences.InputSuspended && (avatar == null || avatar.IsAlive);
                if (pushToTalk) transmit &= TalkPressed;
                CaptureStatus = !deviceAvailable ? "Microphone unavailable: reconnect/select device and check OS permission" :
                    VoicePreferences.TestingMicrophone ? "Local test only (not transmitted)" :
                    recorder.IsCurrentlyTransmitting ? "Transmitting audio" : recorder.LevelMeter == null ? "Waiting for microphone capture" : "Ready";
                recorder.TransmitEnabled = transmit;
                if (avatar != null)
                {
                    // Holding the talk button drives the replicated face even during silence.
                    float level = !transmit ? 0 : pushToTalk ? .8f :
                        recorder.IsCurrentlyTransmitting && recorder.LevelMeter != null ? recorder.LevelMeter.CurrentAvgAmp : 0;
                    avatar.SetVoiceLevel(level);
                }
            }
            else if (output != null)
            {
                // A hard cutoff supplements the AudioSource rolloff and prevents distant leakage.
                var local = voice.Runner.GetPlayerObject(voice.Runner.LocalPlayer);
                if (loadedPreferenceKey == null)
                {
                    loadedPreferenceKey = VolumePreferenceKey;
                    if (loadedPreferenceKey != null)
                    {
                        individualVolume = PlayerPrefs.GetFloat(loadedPreferenceKey, 1f);
                        individuallyMuted = PlayerPrefs.GetInt(loadedPreferenceKey + ".Muted", 0) == 1;
                    }
                }
                output.mute = individuallyMuted || local == null || (local.transform.position - transform.position).sqrMagnitude > hearingDistance * hearingDistance;
                output.volume = individualVolume * MasterVoiceVolume;
            }
        }
    }
}
