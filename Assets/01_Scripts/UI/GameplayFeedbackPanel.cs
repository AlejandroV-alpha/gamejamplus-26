using UnityEngine;

namespace GameJam.UI
{
    /// <summary>Shared presentation for the existing IMGUI interaction prompts.</summary>
    internal static class GameplayFeedbackPanel
    {
        // Las esquinas naranjas del sprite ocupan ~30 px: se dibujan en 9-slice para que
        // no crezcan al estirar el panel ni pisen el texto.
        private const float SpriteBorder = 30f;

        private static GUIStyle titleStyle, hintStyle, valueStyle;

        public static void Draw(string title, string hint, Color accent, float progress = -1f, bool top = false, string icon = null)
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GameJam.UI.UiFont.Label()) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip, padding = new RectOffset() };
                hintStyle = new GUIStyle(GameJam.UI.UiFont.Label()) { alignment = TextAnchor.MiddleLeft, wordWrap = false, padding = new RectOffset() };
                valueStyle = new GUIStyle(GameJam.UI.UiFont.Label()) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, padding = new RectOffset() };
                titleStyle.font = valueStyle.font = UiFont.TitleFont;
            }
            float s = Mathf.Clamp(Screen.height / 1080f, .75f, 1.5f);
            titleStyle.fontSize = GameJam.UI.UiFont.Px(21 * s);
            hintStyle.fontSize = GameJam.UI.UiFont.Px(16 * s);
            valueStyle.fontSize = GameJam.UI.UiFont.Px(20 * s);
            titleStyle.normal.textColor = new Color32(240, 247, 255, 255);
            valueStyle.normal.textColor = accent;

            bool hasProgress = progress >= 0f;
            bool hasHint = !string.IsNullOrEmpty(hint);
            float padX = 34 * s, padTop = 22 * s, padBottom = 22 * s;
            float titleH = 30 * s, barH = 8 * s, hintH = 28 * s;

            // Ancho segun el contenido, con limites.
            float iconW = icon != null ? 30 * s + 12 * s : 0f;
            float valueW = hasProgress ? 64 * s : 0f;
            float wanted = Mathf.Max(
                titleStyle.CalcSize(new GUIContent(title)).x + iconW + valueW + 12 * s,
                hasHint ? HintWidth(hint, s) : 0f) + padX * 2;
            Rect safe = Screen.safeArea;
            float width = Mathf.Clamp(wanted, 420 * s, Mathf.Min(640 * s, safe.width - 32 * s));
            float height = padTop + titleH + (hasProgress ? 14 * s + barH : 0f) + (hasHint ? 10 * s + hintH : 0f) + padBottom;

            float safeTop = Screen.height - safe.yMax;
            float y = top ? safeTop + 32 * s : Screen.height - safe.yMin - height - 110 * s;
            y = Mathf.Max(safeTop + 16 * s, y);
            float x = safe.x + (safe.width - width) * .5f;

            var previousColor = GUI.color;
            try
            {
                GUI.color = Color.white;
                var backdrop = new Rect(x, y, width, height);
                var sprite = ExpeditionSprites.Get("alert_panel");
                if (sprite != null)
                {
                    // Fondo solido: el centro del sprite deja pasar el color del terreno.
                    InteractionPromptCard.Fill(new Rect(x + 6 * s, y + 6 * s, width - 12 * s, height - 12 * s), new Color32(14, 18, 24, 240), 1f, 8 * s);
                    float b = SpriteBorder * Mathf.Min(1f, s);
                    GUI.DrawTexture(backdrop, sprite.texture, ScaleMode.StretchToFill, true, 0, Color.white, new Vector4(b, b, b, b), 0f);
                }
                else InteractionPromptCard.Fill(backdrop, new Color32(9, 18, 30, 245), 1f, 10 * s);

                float cx = x + padX, cw = width - padX * 2, cy = y + padTop;
                if (icon != null)
                {
                    ExpeditionSprites.Draw(new Rect(cx, cy, 30 * s, 30 * s), icon);
                    cx += iconW; cw -= iconW;
                }
                GUI.Label(new Rect(cx, cy, cw - valueW, titleH), title, titleStyle);
                if (hasProgress)
                {
                    progress = Mathf.Clamp01(progress);
                    GUI.Label(new Rect(x + width - padX - valueW, cy, valueW, titleH), $"{progress * 100:0}%", valueStyle);
                    cy += titleH + 14 * s;
                    var track = new Rect(x + padX, cy, width - padX * 2, barH);
                    InteractionPromptCard.Fill(track, new Color32(32, 48, 64, 255), 1f, barH * .5f);
                    if (progress > 0f)
                    {
                        var fill = new Rect(track.x, track.y, Mathf.Max(barH, track.width * progress), barH);
                        InteractionPromptCard.Fill(fill, accent, 1f, barH * .5f);
                    }
                    cy += barH;
                }
                else cy += titleH;

                if (hasHint)
                {
                    cy += 10 * s;
                    // Banda suave detras del hint para que se lea sobre el fondo texturizado.
                    var band = new Rect(x + padX - 8 * s, cy, width - (padX - 8 * s) * 2, hintH);
                    InteractionPromptCard.Fill(band, new Color(0f, 0f, 0f, .32f), 1f, 6 * s);
                    InteractionPromptCard.DrawInline(new Rect(x + padX, cy, width - padX * 2, hintH), hint, hintStyle,
                        new Color32(214, 228, 238, 255), 1f, s);
                }
            }
            finally { GUI.color = previousColor; }
        }

        private static float HintWidth(string hint, float s)
        {
            // Aproximacion: texto sin corchetes + espacio extra por cada tecla.
            int keys = 0;
            foreach (char c in hint) if (c == '[') keys++;
            string plain = hint.Replace("[", "").Replace("]", "");
            return hintStyle.CalcSize(new GUIContent(plain)).x + keys * 30 * s;
        }
    }
}
