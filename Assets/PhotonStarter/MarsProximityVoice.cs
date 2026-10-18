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
        public bool pushToTalk;
        public Key pushToTalkKey = Key.B;
        private VoiceNetworkObject voice;
        private MarsFusionAvatar avatar;
        private AudioSource output;
        private Recorder recorder;

        private void Awake()
        {
            voice = GetComponent<VoiceNetworkObject>();
            avatar = GetComponent<MarsFusionAvatar>();
            recorder = GetComponentInChildren<Recorder>(true);
            var speaker = GetComponentInChildren<Speaker>(true);
            output = speaker != null ? speaker.GetComponent<AudioSource>() : null;
            if (output != null)
            {
                output.spatialBlend = 1;
                output.rolloffMode = AudioRolloffMode.Linear;
                output.minDistance = 2f;
                output.maxDistance = hearingDistance;
                output.dopplerLevel = 0;
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
                if (avatar != null && recorder.IsCurrentlyTransmitting && recorder.LevelMeter != null)
                    avatar.SetVoiceLevel(recorder.LevelMeter.CurrentAvgAmp);
            }
            else if (output != null)
            {
                // A hard cutoff supplements the AudioSource rolloff and prevents distant leakage.
                var local = voice.Runner.GetPlayerObject(voice.Runner.LocalPlayer);
                output.mute = local == null || (local.transform.position - transform.position).sqrMagnitude > hearingDistance * hearingDistance;
            }
        }
    }
}
