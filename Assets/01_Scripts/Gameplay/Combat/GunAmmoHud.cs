using GameJam.Inventory;
using GameJam.Multiplayer;
using GameJam.UI;
using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// HUD local del arma: contador de balas con cartuchos, aviso "sin municion · [R] recargar"
    /// y anillo de recarga con cuenta atras alrededor de la mira.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GunAmmoHud : MonoBehaviour
    {
        private Gun gun;
        private PlayerEquipment equipment;
        private MarsFusionAvatar avatar;
        private GUIStyle bigStyle, smallStyle, labelStyle, ringStyle;
        private float visible, reloadVisible, lastAmmo = -1, ammoPunch, reloadDoneTime = -10f;
        private bool wasReloading;

        private static readonly Color Ok = new Color32(238, 245, 250, 255);
        private static readonly Color Low = new Color(1f, .68f, .25f);
        private static readonly Color Empty = new Color(1f, .33f, .3f);
        private static readonly Color Reload = new Color(.35f, .9f, 1f);

        private void Awake()
        {
            gun = GetComponent<Gun>();
            equipment = GetComponent<PlayerEquipment>();
            avatar = GetComponent<MarsFusionAvatar>();
        }

        private bool Active => gun != null && gun.Object != null && gun.Object.IsValid && gun.HasStateAuthority
            && equipment != null && equipment.IsWeaponEquipped && avatar != null && avatar.IsAlive && !avatar.TabletOpen;

        private void OnGUI()
        {
            if (gun == null || gun.Object == null || !gun.Object.IsValid || !gun.HasStateAuthority) return;
            bool active = Active;
            if (Event.current.type == EventType.Repaint)
            {
                visible = Mathf.MoveTowards(visible, active ? 1f : 0f, Time.unscaledDeltaTime * 6f);
                bool reloading = active && gun.IsReloading;
                if (wasReloading && !reloading && active) reloadDoneTime = Time.unscaledTime;
                wasReloading = reloading;
                reloadVisible = Mathf.MoveTowards(reloadVisible, reloading ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                int ammoNow = active ? gun.GetCurrentAmmo() : -1;
                if (ammoNow >= 0 && ammoNow != lastAmmo && lastAmmo >= 0) ammoPunch = 1f;
                lastAmmo = ammoNow;
                ammoPunch = Mathf.MoveTowards(ammoPunch, 0f, Time.unscaledDeltaTime * 5f);
            }
            if (visible <= .01f) return;
            Setup();
            float s = InteractionPromptCard.Scale;
            int ammo = Mathf.Max(0, gun.GetCurrentAmmo());
            int mag = gun.MagazineSize;
            DrawCounter(s, ammo, mag);
            DrawReloadRing(s);
            DrawWarning(s, ammo, mag);
        }

        /// <summary>Contador abajo a la derecha: numero grande + fila de cartuchos.</summary>
        private void DrawCounter(float s, int ammo, int mag)
        {
            float a = visible;
            Color state = gun.IsReloading ? Reload : ammo == 0 ? Empty : ammo <= Mathf.CeilToInt(mag * .3f) ? Low : Ok;
            bigStyle.fontSize = Mathf.RoundToInt((34 + 6 * ammoPunch) * s);
            smallStyle.fontSize = Mathf.RoundToInt(16 * s);
            labelStyle.fontSize = Mathf.RoundToInt(11 * s);

            float pip = 6 * s, pipH = 16 * s, pipGap = 4 * s;
            int shown = Mathf.Min(mag, 20);
            float pipsW = shown * pip + (shown - 1) * pipGap;
            string count = gun.IsReloading ? "—" : ammo.ToString();
            float countW = bigStyle.CalcSize(new GUIContent("00")).x;
            float magW = smallStyle.CalcSize(new GUIContent("/ " + mag)).x;
            float w = Mathf.Max(countW + magW + 10 * s, pipsW) + 36 * s;
            float h = 96 * s;
            Rect safe = Screen.safeArea;
            float x = safe.xMax - w - 28 * s;
            float y = Screen.height - safe.yMin - h - 170 * s + (1f - a) * 12 * s;

            var card = new Rect(x, y, w, h);
            InteractionPromptCard.Fill(new Rect(card.x + 2 * s, card.y + 4 * s, card.width, card.height), new Color(0, 0, 0, .3f), a, 10 * s);
            InteractionPromptCard.Fill(card, InteractionPromptCard.Panel, a, 10 * s);
            InteractionPromptCard.Fill(new Rect(card.xMax - 4 * s, card.y + 12 * s, 4 * s, card.height - 24 * s), state, a, 2 * s);

            float cx = x + 16 * s;
            labelStyle.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, a);
            GUI.Label(new Rect(cx, y + 10 * s, w, 14 * s), gun.IsReloading ? "RECARGANDO" : "MUNICIÓN", labelStyle);
            bigStyle.normal.textColor = InteractionPromptCard.Fade(state, a);
            GUI.Label(new Rect(cx, y + 22 * s, countW + 6 * s, 42 * s), count, bigStyle);
            smallStyle.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, a);
            GUI.Label(new Rect(cx + countW + 8 * s, y + 34 * s, magW + 4, 26 * s), "/ " + mag, smallStyle);

            // Cartuchos: llenos = balas restantes; durante la recarga se van llenando.
            float filled = gun.IsReloading ? gun.ReloadProgress * shown : ammo * shown / (float)mag;
            float py = y + h - pipH - 12 * s;
            for (int i = 0; i < shown; i++)
            {
                var r = new Rect(cx + i * (pip + pipGap), py, pip, pipH);
                float fill = Mathf.Clamp01(filled - i);
                InteractionPromptCard.Fill(r, new Color(1, 1, 1, .1f), a, 2 * s);
                if (fill > 0f)
                {
                    var f = new Rect(r.x, r.yMax - r.height * fill, r.width, r.height * fill);
                    InteractionPromptCard.Fill(f, state, a, 2 * s);
                }
            }
        }

        /// <summary>Anillo alrededor de la mira que se completa durante la recarga.</summary>
        private void DrawReloadRing(float s)
        {
            float done = Mathf.Clamp01(1f - (Time.unscaledTime - reloadDoneTime) / .45f);
            float a = Mathf.Max(reloadVisible, done) * visible;
            if (a <= .01f) return;
            Vector2 c = new Vector2(Screen.width * .5f, Screen.height * .5f);
            float radius = (34 + 6 * done) * s;
            float progress = gun.IsReloading ? gun.ReloadProgress : 1f;
            const int Segments = 48;
            float seg = 2f * Mathf.PI * radius / Segments;
            Matrix4x4 m = GUI.matrix;
            for (int i = 0; i < Segments; i++)
            {
                float t = i / (float)Segments;
                bool on = t < progress;
                Color col = on ? (done > 0f ? Color.Lerp(Reload, Ok, done) : Reload) : new Color(1, 1, 1, .15f);
                float angle = t * 360f - 90f;
                GUI.matrix = m;
                GUIUtility.RotateAroundPivot(angle, c);
                InteractionPromptCard.Fill(new Rect(c.x + radius - 2.5f * s, c.y - seg * .32f, 5 * s, seg * .64f), col, a, 1.5f * s);
            }
            GUI.matrix = m;
            if (!gun.IsReloading) return;
            ringStyle.fontSize = Mathf.RoundToInt(13 * s);
            ringStyle.normal.textColor = InteractionPromptCard.Fade(Reload, a);
            GUI.Label(new Rect(c.x - 80 * s, c.y + radius + 8 * s, 160 * s, 20 * s), $"RECARGANDO  {gun.ReloadRemaining:0.0}s", ringStyle);
        }

        /// <summary>Aviso bajo la mira cuando no quedan balas (o quedan muy pocas).</summary>
        private void DrawWarning(float s, int ammo, int mag)
        {
            if (gun.IsReloading) return;
            bool empty = ammo == 0;
            bool low = !empty && ammo <= Mathf.CeilToInt(mag * .3f);
            if (!empty && !low) return;
            float sinceDry = Time.unscaledTime - gun.LastDryFireTime;
            float shake = sinceDry < .35f ? Mathf.Sin(sinceDry * 60f) * (1f - sinceDry / .35f) * 6f * s : 0f;
            float pulse = empty ? .75f + .25f * Mathf.Sin(Time.unscaledTime * 6f) : .85f;
            float a = visible * pulse;

            string title = empty ? "SIN MUNICIÓN" : "MUNICIÓN BAJA";
            string hint = "Pulsa [R] para recargar";
            labelStyle.fontSize = Mathf.RoundToInt(14 * s);
            smallStyle.fontSize = Mathf.RoundToInt(15 * s);
            float titleW = labelStyle.CalcSize(new GUIContent(title)).x;
            float hintW = smallStyle.CalcSize(new GUIContent("Pulsa R para recargar")).x + 30 * s;
            float w = Mathf.Max(titleW, hintW) + 40 * s, h = 64 * s;
            float x = Screen.width * .5f - w * .5f + shake;
            float y = Screen.height * .5f + 64 * s;
            Color accent = empty ? Empty : Low;
            var card = new Rect(x, y, w, h);
            InteractionPromptCard.Fill(card, InteractionPromptCard.Panel, a, 10 * s);
            InteractionPromptCard.Fill(new Rect(card.x, card.y, card.width, 3 * s), accent, a, 1.5f * s);
            labelStyle.normal.textColor = InteractionPromptCard.Fade(accent, a);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(x, y + 8 * s, w, 20 * s), title, labelStyle);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            InteractionPromptCard.DrawInline(new Rect(x + (w - hintW) * .5f, y + 30 * s, hintW, 26 * s), hint, smallStyle, InteractionPromptCard.TextOn, a, s);
        }

        private void Setup()
        {
            if (bigStyle != null) return;
            bigStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), wordWrap = false };
            smallStyle = new GUIStyle(bigStyle) { fontStyle = FontStyle.Normal };
            labelStyle = new GUIStyle(bigStyle);
            ringStyle = new GUIStyle(bigStyle) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
