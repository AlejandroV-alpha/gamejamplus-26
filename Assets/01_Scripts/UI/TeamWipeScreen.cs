using UnityEngine;

namespace GameJam.UI
{
    /// <summary>
    /// Pantalla "TODO EL EQUIPO HA CAÍDO": oscurece la escena con un tono rojo,
    /// muestra el aviso y una cuenta atras en anillo hasta reiniciar el nivel.
    /// </summary>
    internal static class TeamWipeScreen
    {
        private static readonly Color Red = new Color(1f, .3f, .3f);
        private static GUIStyle title, sub, count, small;

        /// <param name="remaining">Segundos que faltan para reiniciar.</param>
        /// <param name="total">Duracion total de la cuenta atras.</param>
        /// <param name="elapsed">Segundos desde que aparecio la pantalla (para animar la entrada).</param>
        public static void Draw(float remaining, float total, float elapsed)
        {
            Setup();
            float s = Mathf.Clamp(Screen.height / 1080f, .8f, 1.6f);
            float a = Mathf.Clamp01(elapsed / .5f);
            float ease = 1f - (1f - a) * (1f - a);
            Rect screen = new Rect(0, 0, Screen.width, Screen.height);

            // Oscurecido general + tinte rojo + bordes mas oscuros (vineta).
            InteractionPromptCard.Fill(screen, new Color(0, 0, 0, .55f), ease, 0);
            InteractionPromptCard.Fill(screen, new Color(.45f, 0f, 0f, .18f), ease, 0);
            // Vineta radial suave (sin franjas).
            GUI.color = InteractionPromptCard.Fade(Color.white, ease);
            GUI.DrawTexture(screen, Vignette, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;

            Vector2 c0 = new Vector2(Screen.width * .5f, Screen.height * .44f + (1f - ease) * 20 * s);

            // Icono de peligro animado.
            var frame = ExpeditionSprites.Get(ExpeditionSprites.Frame("danger", elapsed, .8f, true)) ?? ExpeditionSprites.Get("warning");
            float iconSize = 76 * s * (1f + .05f * Mathf.Sin(elapsed * 6f));
            if (frame != null)
            {
                GUI.color = InteractionPromptCard.Fade(Color.white, ease);
                GUI.DrawTexture(new Rect(c0.x - iconSize * .5f, c0.y - 150 * s - iconSize * .5f, iconSize, iconSize), frame.texture, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
            }

            // Banda horizontal tipo "mision fallida" detras del titulo.
            float bandH = 150 * s;
            var band = new Rect(0, c0.y - 112 * s, Screen.width, bandH);
            InteractionPromptCard.Fill(band, new Color(0, 0, 0, .5f), ease, 0);
            float lineW = Screen.width * .55f * ease;
            InteractionPromptCard.Fill(new Rect(c0.x - lineW * .5f, band.y, lineW, 2 * s), Red, ease, 0);
            InteractionPromptCard.Fill(new Rect(c0.x - lineW * .5f, band.yMax - 2 * s, lineW, 2 * s), InteractionPromptCard.Fade(Red, .6f), ease, 0);

            // Titulo.
            title.fontSize = UiFont.Px(44 * s);
            title.normal.textColor = InteractionPromptCard.Fade(Red, ease);
            GUI.Label(new Rect(0, c0.y - 96 * s, Screen.width, 60 * s), "TODO EL EQUIPO HA CAÍDO", title);
            sub.fontSize = UiFont.Px(20 * s);
            sub.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, ease);
            GUI.Label(new Rect(0, c0.y - 36 * s, Screen.width, 30 * s), "La misión se reinicia desde este nivel", sub);

            // Anillo de cuenta atras con el numero dentro.
            float progress = total > 0f ? Mathf.Clamp01(1f - remaining / total) : 1f;
            Vector2 ring = new Vector2(c0.x, c0.y + 70 * s);
            float radius = 46 * s, thick = 6 * s;
            InteractionPromptCard.Fill(new Rect(ring.x - radius - 10 * s, ring.y - radius - 10 * s, (radius + 10 * s) * 2, (radius + 10 * s) * 2), new Color(0, 0, 0, .45f), ease, radius + 10 * s);
            const int Segments = 72;
            float seg = 2f * Mathf.PI * radius / Segments;
            Matrix4x4 m = GUI.matrix;
            for (int i = 0; i < Segments; i++)
            {
                float t = (i + .5f) / Segments;
                Color col = t <= progress ? Red : new Color(1, 1, 1, .12f);
                GUI.matrix = m;
                GUIUtility.RotateAroundPivot(t * 360f - 90f, ring);
                InteractionPromptCard.Fill(new Rect(ring.x + radius - thick * .5f, ring.y - seg * .56f, thick, seg * 1.12f), col, ease, 0);
            }
            GUI.matrix = m;
            int secs = Mathf.Max(0, Mathf.CeilToInt(remaining));
            float tick = remaining - Mathf.Floor(remaining);
            count.fontSize = UiFont.Px(46 * s * (1f + .12f * tick * tick));
            count.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextOn, ease);
            GUI.Label(new Rect(ring.x - radius, ring.y - radius, radius * 2, radius * 2), secs.ToString(), count);

            small.fontSize = UiFont.Px(13 * s);
            small.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, ease * .8f);
            GUI.Label(new Rect(0, ring.y + radius + 16 * s, Screen.width, 20 * s), "REINICIANDO NIVEL", small);
        }

        private static Texture2D vignette;
        private static Texture2D Vignette
        {
            get
            {
                if (vignette != null) return vignette;
                const int N = 128;
                vignette = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + .5f) / N * 2f - 1f, dy = (y + .5f) / N * 2f - 1f;
                        float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - .35f) / .85f);
                        vignette.SetPixel(x, y, new Color(0, 0, 0, d * d * .85f));
                    }
                vignette.Apply();
                return vignette;
            }
        }

        private static void Setup()
        {
            if (title != null) return;
            title = UiFont.Title();
            title.alignment = TextAnchor.MiddleCenter;
            title.fontStyle = FontStyle.Bold;
            sub = UiFont.Label();
            sub.alignment = TextAnchor.MiddleCenter;
            count = UiFont.Title();
            count.alignment = TextAnchor.MiddleCenter;
            count.fontStyle = FontStyle.Bold;
            small = UiFont.Title();
            small.alignment = TextAnchor.MiddleCenter;
        }
    }
}
