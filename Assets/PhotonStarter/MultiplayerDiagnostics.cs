using System;
using System.IO;
using System.Text;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameJam.Multiplayer
{
    // Local opt-in diagnostics. Reports intentionally omit room/player names, audio and App IDs.
    public sealed class MultiplayerDiagnostics : MonoBehaviour
    {
        NetworkRunner runner;
        double lastSample, frameSum;
        int frames;
        double previousRtt;
        bool hasPreviousRtt;
        readonly FrameTiming[] timings = new FrameTiming[1];
        public bool Visible { get; set; }
        public float Fps { get; private set; }
        public double RttMs { get; private set; }
        public double RttVariationMs { get; private set; }
        public double CpuMs { get; private set; }
        public double GpuMs { get; private set; }
        public string LastReport { get; private set; }

        public static MultiplayerDiagnostics Ensure(NetworkRunner target)
        {
            var diagnostics = target.GetComponent<MultiplayerDiagnostics>();
            return diagnostics != null ? diagnostics : target.gameObject.AddComponent<MultiplayerDiagnostics>();
        }
        void Awake() { runner = GetComponent<NetworkRunner>(); lastSample = Time.realtimeSinceStartupAsDouble; }
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) Visible = !Visible;
            frameSum += Time.unscaledDeltaTime; frames++;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastSample < .5) return;
            Fps = frameSum > 0 ? (float)(frames / frameSum) : 0;
            frameSum = 0; frames = 0; lastSample = now;
            if (runner != null && runner.IsRunning && runner.GameMode == GameMode.Shared && runner.IsConnectedToServer)
            {
                RttMs = runner.GetRttToPhotonCloud().last * 1000;
                if (hasPreviousRtt) RttVariationMs = .8 * RttVariationMs + .2 * Math.Abs(RttMs - previousRtt);
                previousRtt = RttMs; hasPreviousRtt = true;
            }
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0) { CpuMs = timings[0].cpuFrameTime; GpuMs = timings[0].gpuFrameTime; }
        }
        public string Summary()
        {
            bool running = runner != null && runner.IsRunning;
            string region = running ? runner.SessionInfo.Region : MultiplayerConfiguration.SelectedRegion;
            var voice = runner != null ? runner.GetComponent<MarsProximityVoiceClient>() : null;
            string voiceState = voice != null && voice.Client != null ? voice.Client.State.ToString() : "not initialized";
            return $"FPS {Fps:F0} | frame {(Fps > 0 ? 1000 / Fps : 0):F1} ms\nCPU {(CpuMs > 0 ? CpuMs.ToString("F1") : "n/a")} ms | GPU {(GpuMs > 0 ? GpuMs.ToString("F1") : "n/a")} ms\n" +
                $"Cloud RTT {(hasPreviousRtt ? RttMs.ToString("F0") : "n/a")} ms | RTT variation {RttVariationMs:F0} ms\nPacket loss: not sampled\nRegion {region} | Fusion {(running ? runner.GameMode.ToString() : "stopped")}\n" +
                $"Master client: {(running && runner.IsSharedModeMasterClient ? "local" : "remote / none")} | Voice {voiceState}\nProtocol {MultiplayerConfiguration.ProtocolVersion}\nBuild {Application.version} | Scene {SceneManager.GetActiveScene().name}";
        }
        public void Export()
        {
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "Diagnostics"); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "multiplayer-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
                File.WriteAllText(path, "UTC " + DateTime.UtcNow.ToString("O") + "\nUnity " + Application.unityVersion + "\n" + Summary(), Encoding.UTF8);
                LastReport = path; Debug.Log("[Multiplayer diagnostics] Report saved: " + path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { LastReport = "Cannot write diagnostic report."; Debug.LogWarning(LastReport); }
        }
        void OnGUI()
        {
            if (!Visible) return;
            float width = Mathf.Min(540, Screen.width - 20);
            GUILayout.BeginArea(new Rect(10, 10, width, Mathf.Min(360, Screen.height - 20)), GUI.skin.box);
            GUILayout.Label("MULTIPLAYER DIAGNOSTICS / F8"); GUILayout.Label(Summary());
            if (GUILayout.Button("Export report")) Export();
            if (!string.IsNullOrEmpty(LastReport)) GUILayout.Label("Report: " + LastReport);
            GUILayout.EndArea();
        }
    }
}
