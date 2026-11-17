using UnityEngine;

namespace GameJam.UI
{
    /// <summary>
    /// Tarjeta flotante de interaccion anclada a un punto del mundo (IMGUI).
    /// Mide cada linea antes de dibujar, asi el texto siempre cabe en la tarjeta.
    /// </summary>
    internal static class InteractionPromptCard
    {
        public struct Row
        {
            public string Key;      // Texto de la tecla: "E", "CLIC IZQ."
            public string Action;   // Accion: "Mantener para reanimar"
            public bool Enabled;    // Atenuada si no se puede usar ahora
        }

        private static GUIStyle headerStyle, nameStyle, keyStyle, actionStyle;
        private static Texture2D rounded, roundedSmall;

        private static readonly Color Panel = new Color32(8, 16, 27, 232);
        private static readonly Color Border = new Color32(90, 140, 175, 90);
        private static readonly Color KeyFill = new Color32(236, 244, 250, 255);
        private static readonly Color KeyText = new Color32(10, 20, 32, 255);
        private static readonly Color TextOn = new Color32(236, 244, 250, 255);
        private static readonly Color TextOff = new Color32(120, 140, 155, 255);

        public static void Draw(Vector3 screenPoint, string header, string name, Color accent, Row[] rows, float alpha)
        {
            if (alpha <= .01f || screenPoint.z <= 0f) return;
            Setup();
            float scale = Mathf.Clamp(Screen.height / 1080f, .7f, 1.6f);
            headerStyle.fontSize = Mathf.RoundToInt(12 * scale);
            nameStyle.fontSize = Mathf.RoundToInt(18 * scale);
            keyStyle.fontSize = Mathf.RoundToInt(13 * scale);
            actionStyle.fontSize = Mathf.RoundToInt(16 * scale);

            float pad = 16 * scale, gap = 10 * scale, rowH = 30 * scale, rowGap = 8 * scale;
            float keyPadX = 9 * scale;

            // Medir todo antes de dibujar.
            float keyW = 0f;
            foreach (var r in rows) keyW = Mathf.Max(keyW, keyStyle.CalcSize(new GUIContent(r.Key)).x + keyPadX * 2);
            keyW = Mathf.Max(keyW, rowH);
            float actionW = 0f;
            foreach (var r in rows) actionW = Mathf.Max(actionW, actionStyle.CalcSize(new GUIContent(r.Action)).x);
            Vector2 headerSize = headerStyle.CalcSize(new GUIContent(header));
            Vector2 nameSize = string.IsNullOrEmpty(name) ? Vector2.zero : nameStyle.CalcSize(new GUIContent(name));

            float contentW = Mathf.Max(keyW + gap + actionW, headerSize.x, nameSize.x);
            float width = contentW + pad * 2 + 4 * scale;
            float headH = headerSize.y + (nameSize.y > 0 ? nameSize.y : 0) + 6 * scale;
            float height = pad + headH + 8 * scale + rows.Length * rowH + (rows.Length - 1) * rowGap + pad;

            // Anclar encima del punto, sin salir de la pantalla.
            float x = screenPoint.x - width * .5f;
            float y = Screen.height - screenPoint.y - height - 14 * scale;
            Rect safe = Screen.safeArea;
            x = Mathf.Clamp(x, safe.xMin + 8, safe.xMax - width - 8);
            y = Mathf.Clamp(y, Screen.height - safe.yMax + 8, Screen.height - safe.yMin - height - 8);
            // Pequeña entrada desde abajo.
            y += (1f - alpha) * 10f * scale;

            Color previous = GUI.color;
            try
            {
                var card = new Rect(x, y, width, height);
                // Borde + panel.
                Fill(new Rect(card.x - 1, card.y - 1, card.width + 2, card.height + 2), Border, alpha, rounded);
                Fill(card, Panel, alpha, rounded);
                // Barra de acento a la izquierda.
                Fill(new Rect(card.x, card.y + 10 * scale, 4 * scale, card.height - 20 * scale), accent, alpha, roundedSmall);

                float cx = card.x + pad + 4 * scale;
                float cy = card.y + pad;
                headerStyle.normal.textColor = Fade(accent, alpha);
                GUI.Label(new Rect(cx, cy, contentW, headerSize.y), header, headerStyle);
                cy += headerSize.y;
                if (nameSize.y > 0)
                {
                    nameStyle.normal.textColor = Fade(TextOn, alpha);
                    GUI.Label(new Rect(cx, cy, contentW, nameSize.y), name, nameStyle);
                    cy += nameSize.y;
                }
                cy += 6 * scale;
                Fill(new Rect(cx, cy, contentW, Mathf.Max(1f, scale)), new Color(1, 1, 1, .08f), alpha, null);
                cy += 8 * scale;

                foreach (var r in rows)
                {
                    float rowAlpha = alpha * (r.Enabled ? 1f : .55f);
                    var keyRect = new Rect(cx, cy, keyW, rowH);
                    Fill(keyRect, r.Enabled ? KeyFill : new Color32(70, 88, 102, 255), rowAlpha, roundedSmall);
                    // Sombra inferior para que parezca una tecla.
                    Fill(new Rect(keyRect.x, keyRect.yMax - 3 * scale, keyRect.width, 3 * scale), new Color(0, 0, 0, .25f), rowAlpha, null);
                    keyStyle.normal.textColor = Fade(r.Enabled ? KeyText : TextOn, rowAlpha);
                    GUI.Label(keyRect, r.Key, keyStyle);
                    actionStyle.normal.textColor = Fade(r.Enabled ? TextOn : TextOff, rowAlpha);
                    GUI.Label(new Rect(keyRect.xMax + gap, cy, actionW + 4, rowH), r.Action, actionStyle);
                    cy += rowH + rowGap;
                }
            }
            finally { GUI.color = previous; }
        }

        private static void Setup()
        {
            if (headerStyle != null) return;
            headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, padding = new RectOffset() };
            nameStyle = new GUIStyle(headerStyle);
            keyStyle = new GUIStyle(headerStyle) { alignment = TextAnchor.MiddleCenter };
            actionStyle = new GUIStyle(headerStyle) { fontStyle = FontStyle.Normal };
            rounded = MakeRounded(32, 8);
            roundedSmall = MakeRounded(16, 4);
        }

        private static Color Fade(Color c, float a) { c.a *= a; return c; }

        private static void Fill(Rect rect, Color color, float alpha, Texture2D tex)
        {
            // DrawTexture con borderRadius dibuja el rectangulo redondeado directamente.
            GUI.color = Color.white;
            float radius = tex == null ? 0f : tex == rounded ? 8f : 4f;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Fade(color, alpha), Vector4.zero, radius);
        }

        /// <summary>Textura blanca con esquinas redondeadas (antialias) para usar como 9-slice.</summary>
        private static Texture2D MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - .5f, x + .5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - .5f, y + .5f - (size - radius)));
                    float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            return tex;
        }
    }
}
