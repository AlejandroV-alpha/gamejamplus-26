using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.UI
{
    // Independent of pause/tablet state so fullscreen remains accessible in every menu.
    public sealed class StandaloneDisplayControls : MonoBehaviour
    {
        int windowWidth = 1280;
        int windowHeight = 720;

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
            if (Screen.fullScreen)
            {
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            }
            else
            {
                windowWidth = Screen.width;
                windowHeight = Screen.height;
                var display = Screen.currentResolution;
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            }
        }
    }
}
