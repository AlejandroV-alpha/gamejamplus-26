using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.UI
{
    // Independent of pause/tablet state so fullscreen remains accessible in every menu.
    public sealed class StandaloneDisplayControls : MonoBehaviour
    {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            var controls = new GameObject("Display Controls");
            DontDestroyOnLoad(controls);
            controls.AddComponent<StandaloneDisplayControls>();
#endif
        }

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.f11Key.wasPressedThisFrame) return;
            ExpeditionGraphics.Instance?.ToggleFullscreen();
        }
    }
}
