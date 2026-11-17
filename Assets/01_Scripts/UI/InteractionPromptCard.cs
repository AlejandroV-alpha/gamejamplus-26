using UnityEngine;

namespace GameJam.UI
{
    /// <summary>
    /// Tarjeta flotante de interaccion anclada a un punto del mundo (IMGUI) y utilidades
    /// compartidas para dibujar teclas. Mide todo antes de dibujar: el texto siempre cabe.
    /// </summary>
    internal static class InteractionPromptCard
    {
        /// <summary>Tecla especial: dibuja un raton con el boton izquierdo resaltado.</summary>
        public const string MouseLeft = "@LMB";

        public struct Row
        {
            public string Key;      // "E", "G" o MouseLeft
            public string Action;   // "Reanimar"
            public string Tag;      // Chip opcional a la derecha: "MANTENER"
            public bool Enabled;    // Atenuada si no se puede usar ahora
        }

        private static GUIStyle headerStyle, nameStyle, keyStyle, actionStyle, tagStyle;

        internal static readonly Color Panel = new Color32(9, 17, 28, 236);
        internal static readonly Color Border = new Color32(110, 160, 195, 70);
        internal static readonly Color KeyFill = new Color32(238, 245, 250, 255);
        internal static readonly Color KeyShadow = new Color32(120, 140, 158, 255);
        internal static readonly Color KeyText = new Color32(12, 22, 34, 255);
        internal static readonly Color TextOn = new Color32(238, 245, 250, 255);
        internal static readonly Color TextSoft = new Color32(168, 188, 204, 255);
        internal static readonly Color TextOff = new Color32(118, 136, 150, 255);

        public static float Scale => Mathf.Clamp(Screen.height / 1080f, .75f, 1.6f);

        public static void Draw(Vector3 screenPoint, string header, string name, Color accent, Row[] rows, float alpha, string iconSprite = null, Texture iconTexture = null)
        {
            if (alpha <= .01f || screenPoint.z <= 0f || rows == null || rows.Length == 0) return;
            Setup();
            float s = Scale;
            headerStyle.fontSize = GameJam.UI.UiFont.Px(11 * s);
            nameStyle.fontSize = GameJam.UI.UiFont.Px(19 * s);
            actionStyle.fontSize = GameJam.UI.UiFont.Px(16 * s);
            tagStyle.fontSize = GameJam.UI.UiFont.Px(10 * s);

            float pad = 16 * s, gap = 12 * s, rowH = 32 * s, rowGap = 8 * s;
            Texture icon = iconTexture != null ? iconTexture : iconSprite != null && ExpeditionSprites.Get(iconSprite) != null ? ExpeditionSprites.Get(iconSprite).texture : null;
            float iconSize = icon != null ? 40 * s : 0f;

            // Medidas.
            float keyW = rowH;
            foreach (var r in rows) keyW = Mathf.Max(keyW, KeyWidth(r.Key, s));
            float actionW = 0f, tagW = 0f;
            foreach (var r in rows)
            {
                actionW = Mathf.Max(actionW, actionStyle.CalcSize(new GUIContent(r.Action)).x);
                if (!string.IsNullOrEmpty(r.Tag)) tagW = Mathf.Max(tagW, tagStyle.CalcSize(new GUIContent(r.Tag)).x + 14 * s);
            }
            Vector2 headerSize = headerStyle.CalcSize(new GUIContent(header));
            Vector2 nameSize = string.IsNullOrEmpty(name) ? Vector2.zero : nameStyle.CalcSize(new GUIContent(name));
            float titleW = Mathf.Max(headerSize.x, nameSize.x) + (icon != null ? iconSize + 10 * s : 0f);
            float rowsW = keyW + gap + actionW + (tagW > 0 ? gap + tagW : 0f);
            float contentW = Mathf.Max(rowsW, titleW);
            float titleH = Mathf.Max(headerSize.y + nameSize.y, iconSize);
            float width = contentW + pad * 2 + 4 * s;
            float height = pad + titleH + 18 * s + rows.Length * rowH + (rows.Length - 1) * rowGap + pad;

            // Posicion: encima del punto, dentro de la pantalla, con una entrada suave.
            float x = screenPoint.x - width * .5f;
            float y = Screen.height - screenPoint.y - height - 18 * s;
            Rect safe = Screen.safeArea;
            x = Mathf.Clamp(x, safe.xMin + 8, safe.xMax - width - 8);
            y = Mathf.Clamp(y, Screen.height - safe.yMax + 8, Screen.height - safe.yMin - height - 8);
            float ease = 1f - (1f - alpha) * (1f - alpha);
            y += (1f - ease) * 12f * s;

            Color previous = GUI.color;
            try
            {
                var card = new Rect(x, y, width, height);
                Fill(new Rect(card.x + 2 * s, card.y + 4 * s, card.width, card.height), new Color(0, 0, 0, .35f), alpha, 10 * s);
                Fill(new Rect(card.x - 1, card.y - 1, card.width + 2, card.height + 2), Border, alpha, 10 * s);
                Fill(card, Panel, alpha, 10 * s);
                Fill(new Rect(card.x, card.y + 12 * s, 4 * s, card.height - 24 * s), accent, alpha, 2 * s);
                // Pequeño puntero que une la tarjeta con el objetivo.
                float px = Mathf.Clamp(screenPoint.x, card.x + 20 * s, card.xMax - 20 * s);
                Fill(new Rect(px - 1 * s, card.yMax, 2 * s, 12 * s), Fade(accent, .7f), alpha, 0);
                Fill(new Rect(px - 3 * s, card.yMax + 11 * s, 6 * s, 6 * s), accent, alpha, 3 * s);

                float cx = card.x + pad + 4 * s;
                float cy = card.y + pad;
                float tx = cx;
                if (icon != null)
                {
                    GUI.color = Fade(Color.white, alpha);
                    GUI.DrawTexture(new Rect(cx, cy + (titleH - iconSize) * .5f, iconSize, iconSize), icon, ScaleMode.ScaleToFit, true);
                    tx += iconSize + 10 * s;
                }
                float ty = cy + (titleH - headerSize.y - nameSize.y) * .5f;
                headerStyle.normal.textColor = Fade(accent, alpha);
                GUI.Label(new Rect(tx, ty, contentW, headerSize.y), header, headerStyle);
                if (nameSize.y > 0)
                {
                    nameStyle.normal.textColor = Fade(TextOn, alpha);
                    GUI.Label(new Rect(tx, ty + headerSize.y, contentW, nameSize.y), name, nameStyle);
                }
                cy += titleH + 9 * s;
                Fill(new Rect(cx, cy, contentW, Mathf.Max(1f, s)), new Color(1, 1, 1, .08f), alpha, 0);
                cy += 9 * s;

                foreach (var r in rows)
                {
                    float ra = alpha * (r.Enabled ? 1f : .6f);
                    DrawKey(new Rect(cx, cy, keyW, rowH), r.Key, r.Enabled, ra, s);
                    actionStyle.normal.textColor = Fade(r.Enabled ? TextOn : TextOff, ra);
                    GUI.Label(new Rect(cx + keyW + gap, cy, actionW + 4, rowH), r.Action, actionStyle);
                    if (!string.IsNullOrEmpty(r.Tag))
                    {
                        float tw = tagStyle.CalcSize(new GUIContent(r.Tag)).x + 14 * s;
                        var tagRect = new Rect(card.x + card.width - pad - tw, cy + (rowH - 18 * s) * .5f, tw, 18 * s);
                        Fill(tagRect, new Color(1, 1, 1, .07f), ra, 9 * s);
                        tagStyle.normal.textColor = Fade(TextSoft, ra);
                        GUI.Label(tagRect, r.Tag, tagStyle);
                    }
                    cy += rowH + rowGap;
                }
            }
            finally { GUI.color = previous; }
        }

        // ───────────── Teclas reutilizables ─────────────

        public static float KeyWidth(string key, float s)
        {
            Setup();
            if (key == MouseLeft) return 26 * s;
            keyStyle.fontSize = GameJam.UI.UiFont.Px(14 * s);
            return Mathf.Max(30 * s, keyStyle.CalcSize(new GUIContent(key)).x + 18 * s);
        }

        /// <summary>Dibuja una tecla con relieve o el icono de raton (boton izquierdo).</summary>
        public static void DrawKey(Rect rect, string key, bool enabled, float alpha, float s)
        {
            Setup();
            if (key == MouseLeft)
            {
                var body = new Rect(rect.x + (rect.width - 20 * s) * .5f, rect.y + (rect.height - 28 * s) * .5f, 20 * s, 28 * s);
                Fill(body, enabled ? KeyFill : TextOff, alpha, 10 * s);
                var inner = new Rect(body.x + 2 * s, body.y + 2 * s, body.width - 4 * s, body.height - 4 * s);
                Fill(inner, Panel, alpha, 8 * s);
                // Boton izquierdo encendido.
                Fill(new Rect(inner.x + 1 * s, inner.y + 1 * s, inner.width * .5f - 1.5f * s, inner.height * .42f), enabled ? KeyFill : TextOff, alpha, 4 * s);
                Fill(new Rect(inner.center.x - .75f * s, inner.y + 2 * s, 1.5f * s, inner.height * .38f), enabled ? KeyFill : TextOff, alpha * .6f, 0);
                return;
            }
            keyStyle.fontSize = GameJam.UI.UiFont.Px(14 * s);
            Fill(new Rect(rect.x, rect.y + 2 * s, rect.width, rect.height), enabled ? KeyShadow : new Color32(40, 52, 62, 255), alpha, 6 * s);
            Fill(new Rect(rect.x, rect.y, rect.width, rect.height - 2 * s), enabled ? KeyFill : new Color32(70, 88, 102, 255), alpha, 6 * s);
            keyStyle.normal.textColor = Fade(enabled ? KeyText : TextOn, alpha);
            GUI.Label(new Rect(rect.x, rect.y - 1 * s, rect.width, rect.height), key, keyStyle);
        }

        /// <summary>
        /// Linea de texto con teclas en linea: "Mantén [E] para volver a intentarlo".
        /// </summary>
        public static void DrawInline(Rect area, string text, GUIStyle style, Color color, float alpha, float s)
        {
            Setup();
            float x = area.x;
            float keyH = Mathf.Min(area.height, style.fontSize + 10 * s);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i);
                string chunk = open < 0 ? text.Substring(i) : text.Substring(i, open - i);
                if (chunk.Length > 0)
                {
                    var content = new GUIContent(chunk);
                    float w = style.CalcSize(content).x;
                    style.normal.textColor = Fade(color, alpha);
                    GUI.Label(new Rect(x, area.y, Mathf.Min(w + 2, area.xMax - x), area.height), content, style);
                    x += w;
                }
                if (open < 0) break;
                int close = text.IndexOf(']', open);
                if (close < 0) break;
                string key = text.Substring(open + 1, close - open - 1);
                float kw = KeyWidth(key, s * .85f);
                DrawKey(new Rect(x + 3 * s, area.y + (area.height - keyH) * .5f, kw, keyH), key, true, alpha, s * .85f);
                x += kw + 6 * s;
                i = close + 1;
            }
        }

        private static void Setup()
        {
            if (headerStyle != null) return;
            headerStyle = new GUIStyle(GameJam.UI.UiFont.Label()) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, padding = new RectOffset(), margin = new RectOffset() };
            nameStyle = new GUIStyle(headerStyle);
            keyStyle = new GUIStyle(headerStyle) { alignment = TextAnchor.MiddleCenter };
            actionStyle = new GUIStyle(headerStyle) { fontStyle = FontStyle.Normal };
            tagStyle = new GUIStyle(headerStyle) { alignment = TextAnchor.MiddleCenter };
            // Orbitron en encabezados, teclas y etiquetas; Exo 2 en nombres y acciones.
            headerStyle.font = keyStyle.font = tagStyle.font = UiFont.TitleFont;
        }

        internal static Color Fade(Color c, float a) { c.a *= a; return c; }

        internal static void Fill(Rect rect, Color color, float alpha, float radius)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Fade(color, alpha), Vector4.zero, radius);
        }
    }
}
