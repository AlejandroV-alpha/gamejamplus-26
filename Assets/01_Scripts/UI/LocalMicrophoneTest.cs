using GameJam.Multiplayer;
using UnityEngine;

namespace GameJam.UI
{
    // Explicit opt-in local loopback. No network recorder is used to play the test.
    public sealed class LocalMicrophoneTest : MonoBehaviour
    {
        AudioSource output;
        AudioClip capture;
        string device;
        readonly float[] samples = new float[256];
        public bool IsRunning => capture != null;
        public float Level { get; private set; }
        public string Status { get; private set; } = "";
        public bool StartTest(bool listen)
        {
            StopTest();
            if (Microphone.devices.Length == 0) { Status = "Microphone unavailable / check OS permission"; return false; }
            device = string.IsNullOrEmpty(VoicePreferences.Device) ? Microphone.devices[0] : VoicePreferences.Device;
            if (System.Array.IndexOf(Microphone.devices, device) < 0) { Status = "Selected microphone disconnected"; return false; }
            VoicePreferences.TestingMicrophone = true;
            MarsFusionAvatar.Local?.GetComponent<MarsProximityVoice>()?.SuspendForLocalTest(true);
            try { capture = Microphone.Start(device, true, 2, 48000); }
            catch (System.Exception e) when (e is System.ArgumentException || e is System.InvalidOperationException) { capture = null; }
            if (capture == null) { StopTest(); Status = "Cannot capture microphone / check OS permission"; return false; }
            output = gameObject.AddComponent<AudioSource>(); output.clip = capture; output.loop = true; output.spatialBlend = 0;
            output.volume = .4f; output.mute = !listen; Status = "Local test only; never sent to room";
            return true;
        }
        void Update()
        {
            if (capture == null) return;
            int position = Microphone.GetPosition(device);
            if (position > 1024 && output != null && !output.isPlaying) { output.timeSamples = Mathf.Max(0, position - 1024); output.Play(); }
            if (position < samples.Length) return;
            capture.GetData(samples, position - samples.Length);
            float total = 0; foreach (float sample in samples) total += sample * sample;
            Level = Mathf.Sqrt(total / samples.Length);
        }
        public void StopTest()
        {
            if (output != null) { output.Stop(); Destroy(output); output = null; }
            if (capture != null) { Microphone.End(device); Destroy(capture); capture = null; }
            Level = 0; VoicePreferences.TestingMicrophone = false;
            MarsFusionAvatar.Local?.GetComponent<MarsProximityVoice>()?.SuspendForLocalTest(false);
        }
        void OnApplicationFocus(bool focused) { if (!focused) StopTest(); }
        void OnDisable() { StopTest(); }
    }
}
