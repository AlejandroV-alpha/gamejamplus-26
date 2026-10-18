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
        private float individualVolume = 1f;

        public float IndividualVolume => individualVolume;
        public bool IsSpeaking => voice != null && (voice.IsLocal
            ? (recorder != null && recorder.IsCurrentlyTransmitting)
            : (output != null && output.isPlaying && !output.mute));

        public void SetIndividualVolume(float volume)
        {
            individualVolume = Mathf.Clamp01(volume);
            if (output != null) output.volume = individualVolume * MasterVoiceVolume;
        }

        public void SetPushToTalk(bool ptt)
        {
            pushToTalk = ptt;
            if (recorder != null && !ptt) recorder.TransmitEnabled = (avatar == null || avatar.IsAlive);
        }

        private void Awake()
        {
            voice = GetComponent<VoiceNetworkObject>();
            avatar = GetComponent<MarsFusionAvatar>();
            recorder = GetComponentInChildren<Recorder>(true);
            pushToTalk = PlayerPrefs.GetInt("GameJam.VoiceMode", 1) == 1;
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
                bool transmit = avatar == null || avatar.IsAlive;
                if (pushToTalk) transmit &= Keyboard.current != null && Keyboard.current[pushToTalkKey].isPressed;
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
                output.mute = local == null || (local.transform.position - transform.position).sqrMagnitude > hearingDistance * hearingDistance;
                output.volume = individualVolume * MasterVoiceVolume;
            }
        }
    }
}
