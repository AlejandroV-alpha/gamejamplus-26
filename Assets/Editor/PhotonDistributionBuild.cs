using GameJam.Multiplayer;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene processing changes the build copy, never the source scene on disk.
public sealed class PhotonDistributionBuild : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    public int callbackOrder => -10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        ReadConfiguration(out _, out _);
        Debug.Log("[Photon build] Fusion y Voice validados; configuración portátil habilitada. App IDs omitidos del log.");
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || !BuildPipeline.isBuildingPlayer) return;
        ReadConfiguration(out string fusion, out string voice);
        var configuration = new GameObject("Photon Distribution Configuration");
        SceneManager.MoveGameObjectToScene(configuration, scene);
        configuration.AddComponent<PhotonBuildConfiguration>().ConfigureForBuild(fusion, voice);
    }

    static void ReadConfiguration(out string fusion, out string voice)
    {
        fusion = PhotonLocalSettings.Read()?.AppIdFusion;
        voice = PhotonLocalSettings.ReadVoiceAppId();
        if (!System.Guid.TryParse(fusion, out var fusionId) || fusionId == System.Guid.Empty ||
            !System.Guid.TryParse(voice, out var voiceId) || voiceId == System.Guid.Empty)
            throw new BuildFailedException("Faltan App IDs locales válidos de Fusion o Voice. Configúralos con Tools/Configure-PhotonLocal.ps1 antes de generar la build. No se publicará una build sin multiplayer y voz configurados.");
    }
}
