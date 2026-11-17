using TMPro;
using UnityEngine;

namespace GameJam.UI
{
    /// <summary>
    /// Tipografia del juego (Google Fonts, licencia SIL OFL):
    ///  · Orbitron: titulos, encabezados, teclas y numeros (aire de nave espacial).
    ///  · Exo 2:    todo el texto de lectura (pistas, menus, descripciones).
    /// Cambiar aqui cambia todo el HUD IMGUI; TextMeshPro usa "Exo2 SDF" como fuente por defecto.
    /// </summary>
    internal static class UiFont
    {
        private const string Folder = "UI/Fonts/";
        private static Font title, body;
        private static TMP_FontAsset tmpBody, tmpTitle;
        private static bool loaded;

        public static Font TitleFont { get { Load(); return title; } }
        public static Font BodyFont { get { Load(); return body; } }
        /// <summary>Fuente TextMeshPro de texto (Exo 2).</summary>
        public static TMP_FontAsset Tmp { get { Load(); return tmpBody; } }
        /// <summary>Fuente TextMeshPro de titulos y numeros (Orbitron).</summary>
        public static TMP_FontAsset TmpTitle { get { Load(); return tmpTitle; } }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            title = Resources.Load<Font>(Folder + "Orbitron");
            body = Resources.Load<Font>(Folder + "Exo2");
            tmpBody = Resources.Load<TMP_FontAsset>(Folder + "Exo2 SDF") ?? TMP_Settings.defaultFontAsset;
            tmpTitle = Resources.Load<TMP_FontAsset>(Folder + "Orbitron SDF") ?? tmpBody;
        }

        /// <summary>Tamaño con un minimo legible.</summary>
        public static int Px(float size) => Mathf.Max(12, Mathf.RoundToInt(size));

        /// <summary>Estilo de texto de lectura (Exo 2).</summary>
        public static GUIStyle Label()
        {
            var style = new GUIStyle(GUI.skin.label);
            if (BodyFont != null) style.font = BodyFont;
            return style;
        }

        /// <summary>Estilo de titulos, teclas y numeros (Orbitron).</summary>
        public static GUIStyle Title()
        {
            var style = new GUIStyle(GUI.skin.label);
            if (TitleFont != null) style.font = TitleFont;
            return style;
        }
    }
}
