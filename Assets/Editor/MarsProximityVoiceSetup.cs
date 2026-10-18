using System.IO;
using System.Linq;
using Fusion;
using GameJam.Multiplayer;
using Photon.Voice;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MarsProximityVoiceSetup
{
    private const string Request = ".local/Photon/voice-setup.request";
    static MarsProximityVoiceSetup() => EditorApplication.delayCall += ApplyRequestedSetup;

    private static void ApplyRequestedSetup()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ApplyRequestedSetup;
            return;
        }
        Configure();
        File.Delete(Request);
    }

    [MenuItem("GameJam/Photon/Configure proximity voice")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play before configuring voice prefabs.");
        const string runnerPath = "Assets/PhotonStarter/j_FusionRunner.prefab";
        var runner = PrefabUtility.LoadPrefabContents(runnerPath);
        try
        {
            if (runner.GetComponent<MarsProximityVoiceClient>() == null) runner.AddComponent<MarsProximityVoiceClient>();
            PrefabUtility.SaveAsPrefabAsset(runner, runnerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(runner); }

        foreach (string path in new[] { "Assets/02_Prefabs/Fusion/A_FusionAstronaut.prefab", "Assets/02_Prefabs/Fusion/j_FusionAstronaut.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<VoiceNetworkObject>() == null) root.AddComponent<VoiceNetworkObject>();
                var recorder = root.GetComponent<Recorder>() ?? root.AddComponent<Recorder>();
                recorder.TransmitEnabled = false;
                recorder.VoiceDetection = true;
                recorder.VoiceDetectionThreshold = .015f;
                recorder.VoiceDetectionDelayMs = 250;
                recorder.SamplingRate = POpusCodec.Enums.SamplingRate.Sampling16000;
                recorder.FrameDuration = OpusCodec.FrameDuration.Frame20ms;
                recorder.Bitrate = 24000;
                recorder.DebugEchoMode = false;
                recorder.RecordWhenJoined = true;
                recorder.StopRecordingWhenPaused = false;
                var voice = root.GetComponent<MarsProximityVoice>() ?? root.AddComponent<MarsProximityVoice>();
                voice.hearingDistance = 22;
                voice.pushToTalk = true;
                voice.pushToTalkKey = UnityEngine.InputSystem.Key.V;
                var speakerTransform = root.transform.Find("Proximity Voice Speaker");
                var speakerObject = speakerTransform != null ? speakerTransform.gameObject : new GameObject("Proximity Voice Speaker");
                speakerObject.transform.SetParent(root.transform, false);
                speakerObject.transform.localPosition = Vector3.up * 1.55f;
                if (speakerObject.GetComponent<Speaker>() == null) speakerObject.AddComponent<Speaker>();
                var audio = speakerObject.GetComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1;
                audio.rolloffMode = AudioRolloffMode.Linear;
                audio.minDistance = 2;
                audio.maxDistance = 22;
                audio.dopplerLevel = 0;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(".local/Photon");
        File.WriteAllText(".local/Photon/voice-setup-result.txt", "Runner configured; both Fusion player prefabs have VoiceNetworkObject, Recorder and 3D Speaker. Range: 22 m. Credentials not serialized.");
        Debug.Log("[Proximity voice] Runner and both astronaut prefabs configured. Range: 22 m; local microphone only; no echo.");
    }
}
