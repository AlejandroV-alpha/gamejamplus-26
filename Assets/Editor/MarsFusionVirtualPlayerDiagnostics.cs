#if UNITY_EDITOR
using System;
using System.IO;
using Fusion;
using UnityEditor;
using UnityEngine;

namespace GameJam.Multiplayer.Editor
{
    // Read-only probe: verifies the actual imported asset in each virtual player.
    [Serializable]
    public class MarsFusionVirtualPlayerProbe : FusionMppmCommand
    {
        public string OutputDirectory;

        public override void Execute()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Mars Fusion virtual config] Detén Play antes de validar la configuración.");
                return;
            }
            string result = "mppm=" + FusionMppm.Status;
            try
            {
                var asset = Resources.Load<NetworkProjectConfigAsset>("NetworkProjectConfig");
                result += " resourceLoaded=" + (asset != null)
                    + " assetDatabaseLoaded=" + (AssetDatabase.LoadAssetAtPath<NetworkProjectConfigAsset>(
                        "Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion") != null);
                NetworkProjectConfig.UnloadGlobal();
                result += " globalLoaded=" + (NetworkProjectConfig.Global != null);
            }
            catch (Exception e)
            {
                result += " configuration-failed " + e.GetType().Name + ": " + e.Message;
            }
            Debug.Log("[Mars Fusion virtual config] " + result);
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(Path.Combine(OutputDirectory,
                System.Diagnostics.Process.GetCurrentProcess().Id + ".txt"), result);
        }
    }

    public static class MarsFusionVirtualPlayerDiagnostics
    {
        [MenuItem("GameJam/Photon/Validate virtual player configuration")]
        public static void Validate()
        {
            var command = new MarsFusionVirtualPlayerProbe
            {
                OutputDirectory = Path.GetFullPath("Temp/MarsFusionVirtualConfig")
            };
            command.Execute();
            FusionMppm.MainEditor?.Send(command);
        }
    }
}
#endif
