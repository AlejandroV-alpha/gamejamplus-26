using UnityEngine;

namespace GameJam.Multiplayer
{
    // Injected into compiled scenes only. No App IDs are stored in source scenes or assets.
    [DefaultExecutionOrder(-32000)]
    public sealed class PhotonBuildConfiguration : MonoBehaviour
    {
        [SerializeField, HideInInspector] string fusionAppId;
        [SerializeField, HideInInspector] string voiceAppId;
        static string fusion;
        static string voice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetConfiguration() { fusion = null; voice = null; }

        void Awake() { Load(this); }

        static void Load(PhotonBuildConfiguration configuration)
        {
            fusion = configuration.fusionAppId;
            voice = configuration.voiceAppId;
        }

        public static string FusionAppId { get { EnsureLoaded(); return fusion; } }
        public static string VoiceAppId { get { EnsureLoaded(); return voice; } }

        static void EnsureLoaded()
        {
            if (!string.IsNullOrEmpty(fusion) && !string.IsNullOrEmpty(voice)) return;
            foreach (var configuration in Resources.FindObjectsOfTypeAll<PhotonBuildConfiguration>())
            {
                if (!configuration.gameObject.scene.IsValid()) continue;
                Load(configuration);
                return;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForBuild(string fusionId, string voiceId)
        {
            fusionAppId = fusionId;
            voiceAppId = voiceId;
        }
#endif
    }
}
