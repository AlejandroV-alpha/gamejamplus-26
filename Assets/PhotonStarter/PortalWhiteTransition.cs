using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJam.Multiplayer
{
    // Local overlay, independent of either scene and of the cinematic VideoPlayer.
    public sealed class PortalWhiteTransition : MonoBehaviour
    {
        public float fadeToWhiteSeconds = .65f;
        public float revealSeconds = 1.3f;
        private static PortalWhiteTransition current;
        public static bool IsBlockingInput => current != null;
        public static bool IsFullyWhite => current != null && current.group.alpha >= 1f;
        private CanvasGroup group;
        private NetworkRunner runner;
        private int destination;
        private float started, readyAt = -1, revealAt = -1, loadingAt = -1;

        public static void Begin(int sceneIndex, NetworkRunner sessionRunner)
        {
            if (current != null) return;
            var go = new GameObject("j_PortalWhiteTransition");
            DontDestroyOnLoad(go);
            current = go.AddComponent<PortalWhiteTransition>();
            current.runner = sessionRunner;
            current.destination = sceneIndex;
            current.started = Time.realtimeSinceStartup;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            current.group = go.AddComponent<CanvasGroup>();
            current.group.alpha = 0;
            current.group.blocksRaycasts = true;
            var image = new GameObject("White").AddComponent<Image>();
            image.transform.SetParent(go.transform, false);
            image.color = Color.white;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            Debug.Log("[Portal transition] stage=fade-to-white");
        }

        private bool DestinationReady()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex != destination || !scene.isLoaded || runner == null || !runner.IsRunning) return false;
            var manager = runner.GetComponent<NetworkSceneManagerDefault>();
            return (manager == null || !manager.IsBusy)
                && runner.GetPlayerObject(runner.LocalPlayer) != null && Camera.main != null;
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (revealAt < 0)
            {
                group.alpha = Mathf.SmoothStep(0,1,(now-started)/Mathf.Max(.01f,fadeToWhiteSeconds));
                if (group.alpha < .999f) return;
                if (DestinationReady())
                {
                    if (readyAt < 0) readyAt = now;
                    // Leave a short settling interval for the spawned player's camera.
                    if (now-readyAt >= .3f)
                    {
                        revealAt = now;
                        Debug.Log("[Portal transition] stage=reveal");
                    }
                }
                else readyAt = -1;
                var manager = runner != null ? runner.GetComponent<NetworkSceneManagerDefault>() : null;
                if (manager != null && manager.IsBusy && loadingAt < 0) loadingAt = now;
                if ((now-started > 2 && (runner == null || !runner.IsRunning))
                    || (loadingAt >= 0 && now-loadingAt > 90))
                {
                    Debug.LogWarning("[Portal transition] stage=aborted connection-or-load-failed");
                    LobbyDepartureEffects.ReleaseLocalPortalWait();
                    revealAt = now;
                }
                return;
            }
            group.alpha = 1-Mathf.SmoothStep(0,1,(now-revealAt)/Mathf.Max(.01f,revealSeconds));
            if (group.alpha <= 0) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
