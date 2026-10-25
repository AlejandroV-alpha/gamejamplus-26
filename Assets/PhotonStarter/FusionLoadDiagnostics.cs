using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Multiplayer
{
    // Attached to the persistent runner, so a scene activation stall remains measurable.
    public sealed class FusionLoadDiagnostics : MonoBehaviour
    {
        private NetworkRunner runner;
        private double previous;
        public static void Attach(NetworkRunner target)
        {
            if (target != null) MultiplayerDiagnostics.Ensure(target);
            if (target != null && target.GetComponent<FusionLoadDiagnostics>() == null)
                target.gameObject.AddComponent<FusionLoadDiagnostics>();
        }
        private void Awake()
        {
            runner = GetComponent<NetworkRunner>();
            previous = Time.realtimeSinceStartupAsDouble;
            Debug.Log("[Fusion load] protocol=" + MultiplayerConfiguration.ProtocolVersion
                + " sdk=" + typeof(NetworkRunner).Assembly.GetName().Version + " unity=" + Application.unityVersion);
        }
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double gap = now - previous;
            previous = now;
            if (gap < 1) return;
            Debug.LogWarning("[Fusion load] main-thread-gap=" + gap.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + "s scene=" + SceneManager.GetActiveScene().name + " focused=" + Application.isFocused
                + " running=" + (runner != null && runner.IsRunning)
                + " sceneBusy=" + (runner != null && runner.IsSceneManagerBusy));
        }
    }
}
