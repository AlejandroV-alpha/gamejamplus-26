using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GameJam.Multiplayer
{
    /// <summary>
    /// Fullscreen video that survives a scene change: it covers the screen while the next scene
    /// loads, plays the clip, then fades out over the new scene.
    /// </summary>
    public sealed class SceneTransitionVideo : MonoBehaviour
    {
        public float fadeOutSeconds = 0.6f;
        private VideoPlayer player;
        private CanvasGroup group;
        private float fadeStart = -1f;

        public static SceneTransitionVideo Play(VideoClip clip)
        {
            var go = new GameObject("j_SceneTransitionVideo");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            go.AddComponent<CanvasScaler>();
            var t = go.AddComponent<SceneTransitionVideo>();
            t.group = go.AddComponent<CanvasGroup>();
            t.group.blocksRaycasts = true;

            // Black first, so nothing of the old/new scene shows while the video prepares.
            var black = new GameObject("Negro").AddComponent<Image>();
            black.transform.SetParent(go.transform, false);
            black.color = Color.black;
            Stretch(black.rectTransform);

            if (clip == null) { t.fadeStart = Time.unscaledTime + 1f; return t; }

            var texture = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = "TransitionVideoRT" };
            var screen = new GameObject("Video").AddComponent<RawImage>();
            screen.transform.SetParent(go.transform, false);
            screen.texture = texture;
            screen.color = new Color(1, 1, 1, 0); // shown once the first frame is ready
            Stretch(screen.rectTransform);
            screen.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            screen.GetComponent<AspectRatioFitter>().aspectRatio = (float)clip.width / clip.height;

            t.player = go.AddComponent<VideoPlayer>();
            t.player.playOnAwake = false;
            t.player.clip = clip;
            t.player.renderMode = VideoRenderMode.RenderTexture;
            t.player.targetTexture = texture;
            t.player.audioOutputMode = VideoAudioOutputMode.None;
            t.player.isLooping = false;
            t.player.prepareCompleted += p => { screen.color = Color.white; p.Play(); };
            t.player.loopPointReached += _ => t.fadeStart = Time.unscaledTime;
            t.player.Prepare();
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (fadeStart < 0f || Time.unscaledTime < fadeStart) return;
            group.alpha = 1f - Mathf.Clamp01((Time.unscaledTime - fadeStart) / fadeOutSeconds);
            if (group.alpha <= 0f)
            {
                if (player != null && player.targetTexture != null) player.targetTexture.Release();
                Destroy(gameObject);
            }
        }
    }
}
