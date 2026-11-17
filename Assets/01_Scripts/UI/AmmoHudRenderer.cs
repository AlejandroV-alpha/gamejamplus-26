using UnityEngine;

namespace GameJam.UI
{
    /// <summary>
    /// Dibujo (IMGUI) del HUD del arma: tarjeta de municion con cartuchos, anillo de recarga
    /// y aviso de "sin municion". Sin estado de juego: recibe todo por <see cref="State"/>.
    /// </summary>
    internal static class AmmoHudRenderer
    {
        public struct State
        {
            public int Ammo, Magazine;
            public bool Reloading;
            public float ReloadProgress, ReloadRemaining;
            public float SinceDryFire, SinceReloadDone, SinceShot;
            public Texture WeaponIcon;
            public string WeaponName;
            public float Alpha;
        }

        public static readonly Color Cyan = new Color(.32f, .9f, 1f);
        public static readonly Color Amber = new Color(1f, .7f, .25f);
        public static readonly Color Red = new Color(1f, .32f, .3f);
        public static readonly Color Green = new Color(.35f, 1f, .6f);
        private static readonly Color Brass = new Color32(214, 170, 82, 255);
        private static readonly Color BrassDark = new Color32(150, 110, 45, 255);
        private static readonly Color Copper = new Color32(232, 128, 72, 255);

        private static GUIStyle big, small, label, center;
        private static float HudScale => Mathf.Clamp(Screen.height / 1080f, .95f, 1.6f);

        public static bool IsLow(State st) => st.Ammo > 0 && st.Ammo <= Mathf.CeilToInt(st.Magazine * .3f);

        private static Color StateColor(State st) =>
            st.Reloading ? Cyan : st.Ammo == 0 ? Red : IsLow(st) ? Amber : InteractionPromptCard.TextOn;

        // ───────────── Tarjeta de municion ─────────────

        /// <summary>Tarjeta anclada por su esquina inferior derecha.</summary>
        public static void DrawCounter(Vector2 bottomRight, State st)
        {
            if (st.Alpha <= .01f) return;
            Setup();
            float s = HudScale, a = st.Alpha;
            float punch = Mathf.Clamp01(1f - st.SinceShot / .18f);
            Color tone = StateColor(st);

            float w = 318 * s, h = 150 * s;
            float x = bottomRight.x - w, y = bottomRight.y - h + (1f - a) * 14 * s;
            var card = new Rect(x, y, w, h);

            // Fondo con el marco del equipo (9-slice) + brillo del estado.
            InteractionPromptCard.Fill(new Rect(card.x + 3 * s, card.y + 5 * s, w, h), new Color(0, 0, 0, .35f), a, 12 * s);
            InteractionPromptCard.Fill(new Rect(card.x + 5 * s, card.y + 5 * s, w - 10 * s, h - 10 * s), new Color32(12, 17, 24, 245), a, 9 * s);
            var frame = ExpeditionSprites.Get("alert_panel");
            if (frame != null)
            {
                float b = 26f * Mathf.Min(1f, s);
                GUI.color = InteractionPromptCard.Fade(Color.white, a);
                GUI.DrawTexture(card, frame.texture, ScaleMode.StretchToFill, true, 0, InteractionPromptCard.Fade(Color.white, a), new Vector4(b, b, b, b), 0f);
                GUI.color = Color.white;
            }
            if (st.Ammo == 0 && !st.Reloading)
            {
                float p = .5f + .5f * Mathf.Sin(Time.unscaledTime * 6f);
                InteractionPromptCard.Fill(new Rect(card.x + 6 * s, card.y + 6 * s, w - 12 * s, h - 12 * s), InteractionPromptCard.Fade(Red, .10f + .10f * p), a, 9 * s);
            }

            // Icono del arma en un circulo.
            float pad = 28 * s, iconBox = 64 * s;
            var iconRect = new Rect(x + pad, y + 24 * s, iconBox, iconBox);
            InteractionPromptCard.Fill(iconRect, new Color(1, 1, 1, .045f), a, 10 * s);
            InteractionPromptCard.Fill(new Rect(iconRect.x, iconRect.yMax - 3 * s, iconRect.width, 3 * s), InteractionPromptCard.Fade(tone, .8f), a, 1.5f * s);
            if (st.WeaponIcon != null)
            {
                float kick = punch * 4 * s;
                GUI.color = InteractionPromptCard.Fade(Color.white, a);
                GUI.DrawTexture(new Rect(iconRect.x + 4 * s - kick, iconRect.y + 4 * s, iconBox - 8 * s, iconBox - 8 * s), st.WeaponIcon, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
            }

            // Nombre + estado.
            float tx = iconRect.xMax + 14 * s;
            label.fontSize = Mathf.RoundToInt(11 * s);
            label.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, a);
            string name = string.IsNullOrEmpty(st.WeaponName) ? "PISTOLA" : st.WeaponName.ToUpperInvariant();
            GUI.Label(new Rect(tx, y + 26 * s, w, 14 * s), name, label);
            string status = st.Reloading ? "RECARGANDO" : st.Ammo == 0 ? "VACÍO" : IsLow(st) ? "BAJA" : "";
            if (status.Length > 0)
            {
                float nw = label.CalcSize(new GUIContent(name)).x;
                float sw = label.CalcSize(new GUIContent(status)).x + 12 * s;
                var chip = new Rect(tx + nw + 8 * s, y + 24 * s, sw, 17 * s);
                InteractionPromptCard.Fill(chip, InteractionPromptCard.Fade(tone, .2f), a, 8 * s);
                label.normal.textColor = InteractionPromptCard.Fade(tone, a);
                label.alignment = TextAnchor.MiddleCenter;
                GUI.Label(chip, status, label);
                label.alignment = TextAnchor.MiddleLeft;
            }

            // Numero grande con cero a la izquierda atenuado: "07".
            big.fontSize = Mathf.RoundToInt((40 + 6 * punch) * s);
            small.fontSize = Mathf.RoundToInt(18 * s);
            int shownAmmo = st.Reloading ? Mathf.FloorToInt(st.ReloadProgress * st.Magazine) : st.Ammo;
            string digits = Mathf.Clamp(shownAmmo, 0, 99).ToString("00");
            bool leadingZero = digits[0] == '0';
            float dw = big.CalcSize(new GUIContent("0")).x;
            float ny = y + 42 * s;
            big.normal.textColor = InteractionPromptCard.Fade(leadingZero ? new Color(1, 1, 1, .18f) : tone, a);
            GUI.Label(new Rect(tx, ny, dw + 4, 44 * s), digits[0].ToString(), big);
            big.normal.textColor = InteractionPromptCard.Fade(tone, a);
            GUI.Label(new Rect(tx + dw, ny, dw + 4, 44 * s), digits[1].ToString(), big);
            small.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, a);
            GUI.Label(new Rect(tx + dw * 2 + 6 * s, ny + 14 * s, 60 * s, 24 * s), "/ " + st.Magazine, small);

            // Tecla R a la derecha cuando conviene recargar.
            if (!st.Reloading && st.Ammo < st.Magazine)
            {
                float kw = InteractionPromptCard.KeyWidth("R", s);
                var key = new Rect(card.xMax - pad - kw, ny + 8 * s, kw, 28 * s);
                InteractionPromptCard.DrawKey(key, "R", true, a * (st.Ammo == 0 || IsLow(st) ? 1f : .55f), s);
            }

            // Cartuchos.
            DrawBullets(new Rect(x + pad + 6 * s, card.yMax - 52 * s, w - pad * 2 - 12 * s, 26 * s), st, tone, a, s);

            // Barra de recarga en la base de la tarjeta.
            if (st.Reloading)
            {
                var track = new Rect(x + pad + 6 * s, card.yMax - 20 * s, w - pad * 2 - 12 * s, 3 * s);
                InteractionPromptCard.Fill(track, new Color(1, 1, 1, .1f), a, 1.5f * s);
                InteractionPromptCard.Fill(new Rect(track.x, track.y, track.width * st.ReloadProgress, track.height), Cyan, a, 1.5f * s);
            }
        }

        /// <summary>Balas dibujadas como cartuchos (vaina de laton + punta de cobre).</summary>
        private static void DrawBullets(Rect area, State st, Color tone, float a, float s)
        {
            int count = Mathf.Clamp(st.Magazine, 1, 30);
            float gap = 5 * s;
            float bw = Mathf.Min(13 * s, (area.width - gap * (count - 1)) / count);
            area.x += (area.width - (bw * count + gap * (count - 1))) * .5f;
            float bh = area.height;
            float filled = st.Reloading ? st.ReloadProgress * count : st.Ammo * count / (float)Mathf.Max(1, st.Magazine);
            bool low = IsLow(st) && !st.Reloading;
            for (int i = 0; i < count; i++)
            {
                float bx = area.x + i * (bw + gap);
                float fill = Mathf.Clamp01(filled - i);
                if (fill <= 0f)
                {
                    // Hueco vacio.
                    InteractionPromptCard.Fill(new Rect(bx, area.y + bh * .3f, bw, bh * .7f), new Color(1, 1, 1, .08f), a, 2 * s);
                    InteractionPromptCard.Fill(new Rect(bx + bw * .15f, area.y + bh * .08f, bw * .7f, bh * .3f), new Color(1, 1, 1, .05f), a, bw * .35f);
                    continue;
                }
                // Al recargar cada bala "salta" a su sitio.
                float pop = st.Reloading ? (1f - fill) * 6 * s : 0f;
                float by = area.y - pop;
                float ba = a * Mathf.Clamp01(fill * 1.5f);
                Color tip = low ? Amber : st.Reloading ? Color.Lerp(Copper, Cyan, .35f) : Copper;
                // Punta.
                InteractionPromptCard.Fill(new Rect(bx + bw * .15f, by + bh * .04f, bw * .7f, bh * .36f), tip, ba, bw * .35f);
                // Vaina con sombra lateral para dar volumen.
                InteractionPromptCard.Fill(new Rect(bx, by + bh * .32f, bw, bh * .68f), Brass, ba, 1.5f * s);
                InteractionPromptCard.Fill(new Rect(bx + bw * .62f, by + bh * .32f, bw * .38f, bh * .68f), BrassDark, ba * .6f, 1.5f * s);
                // Reborde inferior.
                InteractionPromptCard.Fill(new Rect(bx - .5f * s, by + bh - 3 * s, bw + 1 * s, 3 * s), BrassDark, ba, 1 * s);
            }
        }

        // ───────────── Centro de pantalla: recarga y avisos ─────────────

        public static void DrawCenter(Vector2 c, State st)
        {
            if (st.Alpha <= .01f) return;
            Setup();
            float s = HudScale;
            DrawRing(c, st, s);
            DrawWarning(c, st, s);
        }

        private static void DrawRing(Vector2 c, State st, float s)
        {
            float done = Mathf.Clamp01(1f - st.SinceReloadDone / .6f);
            float a = st.Alpha * (st.Reloading ? 1f : done);
            if (a <= .01f) return;
            float radius = (40 + 6 * done) * s;
            float back = radius + 12 * s;
            InteractionPromptCard.Fill(new Rect(c.x - back, c.y - back, back * 2, back * 2), new Color(0, 0, 0, .28f), a, back);
            float progress = st.Reloading ? st.ReloadProgress : 1f;
            Color tone = st.Reloading ? Cyan : Color.Lerp(Green, Color.white, done * .4f);
            const int Segments = 72;
            float seg = 2f * Mathf.PI * radius / Segments;
            float thick = 7 * s;
            Matrix4x4 m = GUI.matrix;
            for (int i = 0; i < Segments; i++)
            {
                float t = (i + .5f) / Segments;
                bool on = t <= progress;
                // Degradado: la cola mas suave, la cabeza mas brillante.
                Color col = on ? Color.Lerp(InteractionPromptCard.Fade(tone, .45f), tone, t / Mathf.Max(.01f, progress)) : new Color(1, 1, 1, .12f);
                GUI.matrix = m;
                GUIUtility.RotateAroundPivot(t * 360f - 90f, c);
                InteractionPromptCard.Fill(new Rect(c.x + radius - thick * .5f, c.y - seg * .56f, thick, seg * 1.12f), col, a, 0);
            }
            GUI.matrix = m;
            if (st.Reloading)
            {
                // Punto luminoso en la cabeza del arco.
                float ang = (progress * 360f - 90f) * Mathf.Deg2Rad;
                Vector2 head = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
                InteractionPromptCard.Fill(new Rect(head.x - 6 * s, head.y - 6 * s, 12 * s, 12 * s), InteractionPromptCard.Fade(Cyan, .35f), a, 6 * s);
                InteractionPromptCard.Fill(new Rect(head.x - 3.5f * s, head.y - 3.5f * s, 7 * s, 7 * s), Color.white, a, 3.5f * s);
            }

            // Pastilla bajo el anillo: "RECARGANDO 0.8s" o "¡LISTO!".
            string text = st.Reloading ? "RECARGANDO" : "¡LISTO!";
            string time = st.Reloading ? st.ReloadRemaining.ToString("0.0") + "s" : "";
            label.fontSize = Mathf.RoundToInt(13 * s);
            small.fontSize = Mathf.RoundToInt(14 * s);
            float tw = label.CalcSize(new GUIContent(text)).x;
            float timeW = time.Length > 0 ? small.CalcSize(new GUIContent("0.0s")).x + 12 * s : 0f;
            float pw = tw + timeW + 28 * s, ph = 28 * s;
            var pill = new Rect(c.x - pw * .5f, c.y + back + 8 * s, pw, ph);
            InteractionPromptCard.Fill(pill, InteractionPromptCard.Panel, a, ph * .5f);
            label.normal.textColor = InteractionPromptCard.Fade(tone, a);
            GUI.Label(new Rect(pill.x + 12 * s, pill.y, tw + 2, ph), text, label);
            if (time.Length > 0)
            {
                InteractionPromptCard.Fill(new Rect(pill.x + 12 * s + tw + 6 * s, pill.y + 6 * s, Mathf.Max(1f, s), ph - 12 * s), new Color(1, 1, 1, .2f), a, 0);
                small.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextOn, a);
                GUI.Label(new Rect(pill.xMax - timeW - 6 * s, pill.y, timeW, ph), time, small);
            }
        }

        private static void DrawWarning(Vector2 c, State st, float s)
        {
            if (st.Reloading) return;
            bool empty = st.Ammo == 0;
            if (!empty && !IsLow(st)) return;
            float shake = st.SinceDryFire < .4f ? Mathf.Sin(st.SinceDryFire * 70f) * (1f - st.SinceDryFire / .4f) * 7f * s : 0f;
            float flash = Mathf.Clamp01(1f - st.SinceDryFire / .25f);
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 5f);
            Color tone = empty ? Red : Amber;
            float a = st.Alpha;

            string title = empty ? "SIN MUNICIÓN" : "MUNICIÓN BAJA";
            label.fontSize = Mathf.RoundToInt((empty ? 16 : 13) * s);
            small.fontSize = Mathf.RoundToInt(14 * s);
            float iconW = 26 * s;
            float titleW = label.CalcSize(new GUIContent(title)).x;
            float keyW = InteractionPromptCard.KeyWidth("R", s);
            float actW = small.CalcSize(new GUIContent("Recargar")).x;
            float h = (empty ? 48 : 40) * s;
            float w = 18 * s + iconW + 10 * s + titleW + 22 * s + keyW + 8 * s + actW + 18 * s;
            var card = new Rect(c.x - w * .5f + shake, c.y + 96 * s, w, h);

            if (empty)
            {
                float halo = 6 * s + 4 * s * pulse;
                InteractionPromptCard.Fill(new Rect(card.x - halo, card.y - halo, card.width + halo * 2, card.height + halo * 2), InteractionPromptCard.Fade(Red, .12f + .1f * pulse + .3f * flash), a, h * .5f + halo);
            }
            InteractionPromptCard.Fill(card, InteractionPromptCard.Panel, a, h * .5f);
            InteractionPromptCard.Fill(new Rect(card.x + 1, card.y + 1, card.width - 2, card.height - 2), InteractionPromptCard.Fade(tone, .08f + .25f * flash), a, h * .5f);

            float x = card.x + 18 * s;
            var icon = ExpeditionSprites.Get("warning");
            if (icon != null)
            {
                GUI.color = InteractionPromptCard.Fade(Color.white, a);
                GUI.DrawTexture(new Rect(x, card.y + (h - iconW) * .5f, iconW, iconW), icon.texture, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
            }
            x += iconW + 10 * s;
            label.normal.textColor = InteractionPromptCard.Fade(tone, a);
            GUI.Label(new Rect(x, card.y, titleW + 2, h), title, label);
            x += titleW + 11 * s;
            InteractionPromptCard.Fill(new Rect(x, card.y + 12 * s, Mathf.Max(1f, s), h - 24 * s), new Color(1, 1, 1, .18f), a, 0);
            x += 11 * s;
            InteractionPromptCard.DrawKey(new Rect(x, card.y + (h - 28 * s) * .5f, keyW, 28 * s), "R", true, a, s);
            x += keyW + 8 * s;
            small.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextOn, a);
            GUI.Label(new Rect(x, card.y, actW + 4, h), "Recargar", small);
        }

        private static void Setup()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), wordWrap = false };
            small = new GUIStyle(big) { fontStyle = FontStyle.Normal };
            label = new GUIStyle(big);
            center = new GUIStyle(big) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
