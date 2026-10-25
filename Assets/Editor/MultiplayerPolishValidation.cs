using System;
using Fusion;
using GameJam.Multiplayer;
using UnityEditor;
using UnityEngine;

public static class MultiplayerPolishValidation
{
    [MenuItem("GameJam/Diagnostics/Validate multiplayer policies")]
    public static void ValidatePolicies()
    {
        string[] enemies = {
            "Assets/02_Prefabs/GiantGolemMars.prefab", "Assets/02_Prefabs/GiantGolemNeptune.prefab",
            "Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Fusion.prefab",
            "Assets/Enemies/GusanoExcavador/Gusano_Excavador_Fusion.prefab",
            "Assets/Enemies/JungleNightmare/Prefabs/JungleNightmare_Fusion.prefab"
        };
        foreach (string path in enemies)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var network = prefab != null ? prefab.GetComponent<NetworkObject>() : null;
            if (network == null || (network.Flags & NetworkObjectFlags.MasterClientObject) == 0 ||
                (network.Flags & NetworkObjectFlags.DestroyWhenStateAuthorityLeaves) != 0)
                throw new InvalidOperationException("Invalid enemy authority policy: " + path);
        }
        foreach (string name in new[] { "A_FusionAstronaut", "A_SurvivalAstronaut" })
        {
            var network = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Fusion/" + name + ".prefab").GetComponent<NetworkObject>();
            if ((network.Flags & NetworkObjectFlags.MasterClientObject) != 0 || (network.Flags & NetworkObjectFlags.DestroyWhenStateAuthorityLeaves) == 0)
                throw new InvalidOperationException("Player ownership changed unexpectedly: " + name);
        }
        Debug.Log("[Multiplayer validation] PASS: five world enemy policies, two player ownership policies. Protocol=" + MultiplayerConfiguration.ProtocolVersion);
    }

    [MenuItem("GameJam/Diagnostics/Build multiplayer validation (Windows)")]
    public static void BuildValidationPlayer()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
        { Debug.LogWarning("Stop Play before building the validation executable."); return; }
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
        { Debug.LogWarning("Select the Windows 64-bit build target first."); return; }
        ValidatePolicies();
        string directory = System.IO.Path.GetFullPath(".local/MenuValidation/Windows");
        System.IO.Directory.CreateDirectory(directory);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/00_Scenes/j_LobyDeEspera.unity", "Assets/00_Scenes/Mars.unity", "Assets/00_Scenes/b_NeptuneExploration.unity" },
            locationPathName = System.IO.Path.Combine(directory, "ExpeditionValidation.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Validation build failed: " + report.summary.result);
        Debug.Log("[Multiplayer validation] BUILD PASS: " + report.summary.totalSize + " bytes; " + report.summary.totalTime);
    }

    [MenuItem("GameJam/Diagnostics/Print and export live status")]
    public static void LiveStatus()
    {
        var session = UnityEngine.Object.FindFirstObjectByType<MarsFusionSession>();
        if (!Application.isPlaying || session == null || session.Runner == null) { Debug.LogWarning("Start a session first."); return; }
        var diagnostics = MultiplayerDiagnostics.Ensure(session.Runner); diagnostics.Visible = true;
        Debug.Log("[Multiplayer validation]\n" + diagnostics.Summary()); diagnostics.Export();
    }
}
