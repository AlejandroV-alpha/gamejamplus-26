using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.UI
{
    public static class ExpeditionInputPreferences
    {
        public const string OverridesKey = "GameJam.Input.Overrides";
        public static float Sensitivity => Mathf.Clamp(PlayerPrefs.GetFloat("GameJam.Input.Sensitivity", 1f), .1f, 3f);
        public static bool InvertY => PlayerPrefs.GetInt("GameJam.Input.InvertY", 0) == 1;
        public static void Load(InputActionAsset actions)
        {
            string json = PlayerPrefs.GetString(OverridesKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try { actions.LoadBindingOverridesFromJson(json); }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { Debug.LogWarning("[Input settings] Saved bindings invalid; using defaults."); actions.RemoveAllBindingOverrides(); }
        }
        public static void Save(InputActionAsset actions) { PlayerPrefs.SetString(OverridesKey, actions.SaveBindingOverridesAsJson()); PlayerPrefs.Save(); }
    }
}
