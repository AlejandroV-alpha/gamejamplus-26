using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

namespace GameJam.Multiplayer
{
    public static class VoicePreferences
    {
        public static bool CapturingBinding;
        public static bool TestingMicrophone;
        public static int Revision { get; private set; }
        public static int CaptureRevision { get; private set; }
        public static string Device => PlayerPrefs.GetString("GameJam.Voice.Device", "");
        public static float Threshold => Mathf.Clamp(PlayerPrefs.GetFloat("GameJam.Voice.Threshold", .01f), .001f, .2f);
        public static Key TalkKey => Enum.TryParse(PlayerPrefs.GetString("GameJam.Voice.Key", "V"), out Key key) && key != Key.None ? key : Key.V;
        public static bool InputSuspended => CapturingBinding || TestingMicrophone || !Application.isFocused ||
            (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
             EventSystem.current.currentSelectedGameObject.GetComponentInParent<TMP_InputField>() != null);
        public static void Changed(bool restartCapture = false) { Revision++; if (restartCapture) CaptureRevision++; PlayerPrefs.Save(); }
        public static bool TrySetTalkKey(Key key)
        {
            // These keys already perform a gameplay/menu action outside InputActions.
            if (key == Key.None || key == Key.Escape || key == Key.Tab || key == Key.E || key == Key.Q ||
                key == Key.R || key == Key.I || key == Key.W || key == Key.A || key == Key.S || key == Key.D ||
                key == Key.Space || key == Key.LeftShift || key == Key.F8 || key == Key.F11 ||
                (key >= Key.Digit1 && key <= Key.Digit9)) return false;
            var local = MarsFusionAvatar.Local;
            var asset = local != null ? local.GetComponent<Starter.ThirdPersonCharacter.PlayerInput>()?.BindingAsset : null;
            if (asset != null)
                foreach (var action in asset)
                    foreach (var binding in action.bindings)
                        if (!binding.isComposite && string.Equals(binding.effectivePath, "<Keyboard>/" + key, StringComparison.OrdinalIgnoreCase)) return false;
            PlayerPrefs.SetString("GameJam.Voice.Key", key.ToString()); Changed(); return true;
        }
    }
}
