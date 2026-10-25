using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GameJam.UI
{
    // Local display/quality preferences. Never edits the source render pipeline asset.
    public sealed class ExpeditionGraphics : MonoBehaviour
    {
        public static ExpeditionGraphics Instance { get; private set; }
        UniversalRenderPipelineAsset originalPipeline, runtimePipeline;
        bool previousPipelineOverride;
        int oldWidth, oldHeight;
        FullScreenMode oldMode;
        float confirmBy;
        public bool PendingDisplay => confirmBy > 0;
        public int SecondsRemaining => Mathf.Max(0, Mathf.CeilToInt(confirmBy - Time.realtimeSinceStartup));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("Expedition Graphics Settings"); DontDestroyOnLoad(go);
            go.AddComponent<ExpeditionGraphics>();
        }
        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            SceneManager.sceneLoaded += SceneLoaded;
            if (PlayerPrefs.HasKey("GameJam.Graphics.Preset")) ApplyQuality();
#if !UNITY_EDITOR
            if (PlayerPrefs.HasKey("GameJam.Graphics.Mode"))
                Screen.SetResolution(PlayerPrefs.GetInt("GameJam.Graphics.Width", Screen.currentResolution.width),
                    PlayerPrefs.GetInt("GameJam.Graphics.Height", Screen.currentResolution.height),
                    PlayerPrefs.GetInt("GameJam.Graphics.Mode", 1) == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
#endif
            ApplyFrameLimit();
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode) { ApplyCamera(); }
        void Update() { if (PendingDisplay && Time.realtimeSinceStartup >= confirmBy) RevertDisplay(); }
        public void PreviewDisplay(int width, int height, bool fullscreen)
        {
#if !UNITY_EDITOR
            if (PendingDisplay) RevertDisplay();
            oldWidth = Screen.width; oldHeight = Screen.height; oldMode = Screen.fullScreenMode;
            confirmBy = Time.realtimeSinceStartup + 15f;
            Screen.SetResolution(Mathf.Max(640, width), Mathf.Max(480, height), fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }
        public void ConfirmDisplay()
        {
            if (!PendingDisplay) return;
            confirmBy = 0;
            PlayerPrefs.SetInt("GameJam.Graphics.Width", Screen.width); PlayerPrefs.SetInt("GameJam.Graphics.Height", Screen.height);
            PlayerPrefs.SetInt("GameJam.Graphics.Mode", Screen.fullScreen ? 1 : 0); PlayerPrefs.Save();
        }
        public void RevertDisplay()
        {
            if (!PendingDisplay) return;
            confirmBy = 0; Screen.SetResolution(oldWidth, oldHeight, oldMode);
        }
        public void ToggleFullscreen()
        {
            if (PendingDisplay) return;
            if (Screen.fullScreen) PreviewDisplay(Mathf.Min(1280, Screen.currentResolution.width), Mathf.Min(720, Screen.currentResolution.height), false);
            else PreviewDisplay(Screen.currentResolution.width, Screen.currentResolution.height, true);
            // F11 is a direct user action; timed confirmation is for the settings form.
            if (PendingDisplay) StartCoroutine(ConfirmNextFrame());
        }
        System.Collections.IEnumerator ConfirmNextFrame() { yield return new WaitForEndOfFrame(); ConfirmDisplay(); }
        public void SetPreset(int value)
        {
            PlayerPrefs.SetInt("GameJam.Graphics.Preset", Mathf.Clamp(value, 0, 2)); ApplyQuality(); PlayerPrefs.Save();
        }
        void ApplyQuality()
        {
            int preset = Mathf.Clamp(PlayerPrefs.GetInt("GameJam.Graphics.Preset", 1), 0, 2);
            if (runtimePipeline == null && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                previousPipelineOverride = QualitySettings.renderPipeline != null;
                originalPipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                runtimePipeline = Instantiate(urp); runtimePipeline.name = "Local Expedition Quality (Runtime)";
                QualitySettings.renderPipeline = runtimePipeline;
            }
            if (runtimePipeline != null)
            {
                runtimePipeline.renderScale = preset == 0 ? .75f : preset == 1 ? .9f : 1f;
                runtimePipeline.shadowDistance = preset == 0 ? 25 : preset == 1 ? 45 : 70;
                runtimePipeline.msaaSampleCount = preset == 0 ? 1 : preset == 1 ? 2 : 4;
            }
            QualitySettings.lodBias = preset == 0 ? 1 : preset == 1 ? 1.5f : 2;
            ApplyCamera();
        }
        void ApplyCamera()
        {
            if (!PlayerPrefs.HasKey("GameJam.Graphics.Preset") && !PlayerPrefs.HasKey("GameJam.Graphics.PostProcessing")) return;
            if (Camera.main != null && Camera.main.TryGetComponent<UniversalAdditionalCameraData>(out var camera))
                camera.renderPostProcessing = PlayerPrefs.GetInt("GameJam.Graphics.PostProcessing", 1) == 1;
        }
        public void SetPostProcessing(bool enabled)
        {
            PlayerPrefs.SetInt("GameJam.Graphics.PostProcessing", enabled ? 1 : 0); ApplyCamera(); PlayerPrefs.Save();
        }
        public void ApplyFrameLimit()
        {
            QualitySettings.vSyncCount = PlayerPrefs.GetInt("GameJam.Graphics.VSync", 0);
            Application.targetFrameRate = PlayerPrefs.GetInt("GameJam.Graphics.FpsLimit", 120);
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Instance != this) return;
            RevertDisplay(); Instance = null;
            if (runtimePipeline != null)
            {
                QualitySettings.renderPipeline = previousPipelineOverride ? originalPipeline : null;
                Destroy(runtimePipeline);
            }
        }
    }
}
