using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

namespace GameJam.Multiplayer
{
    public sealed class SceneTransitionVideo : MonoBehaviour
    {
        public float fadeOutSeconds = .6f;
        private static SceneTransitionVideo current;
        public static bool IsBlockingInput => current != null && current.covering;
        private VideoPlayer player;
        private RenderTexture texture;
        private RawImage screen;
        private CanvasGroup group;
        private VideoClip clip;
        private int destinationIndex;
        private float coverAt, waitStarted, playbackStarted, lastProgress, fadeStart = -1f;
        private long lastFrame = -1;
        private bool covering, preparing, playing, finished;
        public static bool IsPresent => current != null;
        public static void CancelCurrent()
        {
            if (current != null) Destroy(current.gameObject);
        }

        public static SceneTransitionVideo Play(VideoClip clip, int destinationIndex = -1, float delay = 0f)
        {
            if (current != null) return current;
            var go = new GameObject("j_SceneTransitionVideo");
            DontDestroyOnLoad(go);
            var transition = go.AddComponent<SceneTransitionVideo>();
            current = transition;
            transition.clip = clip;
            transition.destinationIndex = destinationIndex;
            transition.coverAt = Time.realtimeSinceStartup + Mathf.Max(0, delay);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            go.AddComponent<CanvasScaler>();
            transition.group = go.AddComponent<CanvasGroup>();
            transition.group.alpha = 0;
            transition.group.blocksRaycasts = false;
            var black = new GameObject("Negro").AddComponent<Image>();
            black.transform.SetParent(go.transform, false);
            black.color = Color.black;
            Stretch(black.rectTransform);
            return transition;
        }

        private bool SceneReady()
        {
            if (destinationIndex < 0) return true;
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.buildIndex != destinationIndex) return false;
            var session = FindFirstObjectByType<MarsFusionSession>();
            if (session == null || session.Runner == null || !session.Runner.IsRunning) return false;
            var manager = session.Runner.GetComponent<Fusion.NetworkSceneManagerDefault>();
            return (manager == null || !manager.IsBusy)
                && session.Runner.GetPlayerObject(session.Runner.LocalPlayer) != null;
        }

        private void PrepareVideo()
        {
            preparing = true;
            waitStarted = Time.realtimeSinceStartup;
            if (clip == null) { Finish("no-clip"); return; }
            float scale = Mathf.Min(1f, 1280f / Mathf.Max(1, clip.width));
            texture = new RenderTexture(Mathf.Max(1, Mathf.RoundToInt(clip.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(clip.height * scale)), 0) { name = "TransitionVideoRT" };
            screen = new GameObject("Video").AddComponent<RawImage>();
            screen.transform.SetParent(transform, false);
            screen.texture = texture;
            screen.color = new Color(1,1,1,0);
            Stretch(screen.rectTransform);
            var aspect = screen.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = (float)clip.width / Mathf.Max(1, clip.height);
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.clip = clip;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = texture;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.isLooping = false;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.sendFrameReadyEvents = true;
            player.prepareCompleted += Prepared;
            player.frameReady += FrameReady;
            player.loopPointReached += Ended;
            player.errorReceived += Failed;
            Debug.Log("[Transition video] stage=preparing scene=" + SceneManager.GetActiveScene().buildIndex);
            player.Prepare();
        }

        private void Prepared(VideoPlayer source)
        {
            if (finished) return;
            playing = true;
            playbackStarted = lastProgress = Time.realtimeSinceStartup;
            source.Play();
            Debug.Log("[Transition video] stage=playing");
        }
        private void FrameReady(VideoPlayer source, long frame)
        {
            if (finished) return;
            screen.color = Color.white;
            if (frame != lastFrame) { lastFrame = frame; lastProgress = Time.realtimeSinceStartup; }
        }
        private void Ended(VideoPlayer source) => Finish("completed");
        private void Failed(VideoPlayer source, string error)
        {
            Debug.LogWarning("[Transition video] decoder-error=" + error);
            Finish("decoder-error");
        }
        private void Finish(string reason)
        {
            if (finished) return;
            finished = true;
            fadeStart = Time.realtimeSinceStartup;
            if (player != null) player.Pause();
            Debug.Log("[Transition video] stage=closing reason=" + reason);
        }
        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (!covering)
            {
                if (now < coverAt) return;
                covering = true;
                group.alpha = 1;
                group.blocksRaycasts = true;
                waitStarted = now;
            }
            if (!finished)
            {
                if (!preparing && SceneReady()) PrepareVideo();
                else if (!preparing && now - waitStarted > 60) Finish("scene-timeout");
                else if (preparing && !playing && now - waitStarted > 12) Finish("prepare-timeout");
                else if (playing && now - lastProgress > 6) Finish("playback-stalled");
                else if (playing && now - playbackStarted > (float)clip.length + 10) Finish("playback-timeout");
            }
            if (fadeStart < 0) return;
            group.alpha = 1 - Mathf.Clamp01((now - fadeStart) / Mathf.Max(.01f, fadeOutSeconds));
            if (group.alpha <= 0) Destroy(gameObject);
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private void OnDestroy()
        {
            if (current == this) current = null;
            if (player != null)
            {
                player.prepareCompleted -= Prepared;
                player.frameReady -= FrameReady;
                player.loopPointReached -= Ended;
                player.errorReceived -= Failed;
                player.Stop();
                player.targetTexture = null;
            }
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
