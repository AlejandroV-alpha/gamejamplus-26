using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameJam.Tablet;

namespace GameJam.Combat
{
    // Local presentation only. Health values and damage events come from enemy authority.
    public sealed class EnemyCombatUI : MonoBehaviour
    {
        private static RectTransform overlay;
        private RectTransform panel, fill, trail, fillEdge;
        private TabletRoundedPanel impactFlash, healthGraphic, borderGraphic;
        private float lastDamageAtForAnimation = -10f;
        private TextMeshProUGUI valueLabel;
        private Func<float> readHealth;
        private Func<bool> canShow;
        private float maximum, displayed, delayed, lastDamageAt;
        private Renderer[] visuals;
        private Camera view;
        private int damageSequence;

        public static EnemyCombatUI Attach(GameObject enemy, string title, Func<float> health,
            float maxHealth, Func<bool> visible = null)
        {
            var ui = enemy.GetComponent<EnemyCombatUI>();
            if (ui != null) return ui;
            ui = enemy.AddComponent<EnemyCombatUI>();
            ui.readHealth = health;
            ui.canShow = visible;
            ui.maximum = Mathf.Max(1, maxHealth);
            ui.displayed = ui.delayed = 1;
            ui.visuals = enemy.GetComponentsInChildren<Renderer>(true);
            ui.Build(title);
            return ui;
        }

        private static RectTransform Overlay()
        {
            if (overlay != null) return overlay;
            var go = new GameObject("EnemyCombatOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            overlay = (RectTransform)go.transform;
            return overlay;
        }

        private static RectTransform Rect(string title, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(title, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform Box(string title, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            var rect = Rect(title, parent, size, position);
            var graphic = rect.gameObject.AddComponent<TabletRoundedPanel>();
            graphic.radius = 3;
            graphic.color = color;
            graphic.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI Text(string title, Transform parent, Vector2 size, Vector2 position, float fontSize)
        {
            var rect = Rect(title, parent, size, position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = new Color(.92f, .95f, 1);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        private void Build(string title)
        {
            panel = Box("EnemyHealth_" + title, Overlay(), new Vector2(238, 66), Vector2.zero,
                new Color(.17f, .24f, .30f, .95f));
            borderGraphic = panel.GetComponent<TabletRoundedPanel>();
            borderGraphic.radius = 7;
            var body = Box("ArmoredBody", panel, new Vector2(234, 62), Vector2.zero,
                new Color(.025f, .035f, .055f, .96f));
            body.GetComponent<TabletRoundedPanel>().radius = 6;
            Box("TopAccent", body, new Vector2(66, 2), new Vector2(-68, 30), new Color(.96f, .2f, .28f));
            var marker = Box("ThreatMarker", body, new Vector2(7, 7), new Vector2(-102, 16), new Color(1, .24f, .3f));
            marker.localRotation = Quaternion.Euler(0, 0, 45);
            var name = Text("Name", body, new Vector2(192, 23), new Vector2(7, 16), 14);
            name.text = title.ToUpperInvariant();
            name.fontStyle = FontStyles.Bold;
            name.characterSpacing = 1.1f;
            name.alignment = TextAlignmentOptions.Left;
            Box("TrackRim", body, new Vector2(212, 16), new Vector2(0, -3), new Color(.24f, .12f, .17f));
            var track = Box("Track", body, new Vector2(208, 12), new Vector2(0, -3), new Color(.07f, .09f, .13f));
            trail = Box("DamageTrail", track, new Vector2(208, 12), new Vector2(-104, 0), new Color(1, .55f, .39f));
            fill = Box("Health", track, new Vector2(208, 12), new Vector2(-104, 0), new Color(.92f, .13f, .24f));
            fill.pivot = trail.pivot = new Vector2(0, .5f);
            healthGraphic = fill.GetComponent<TabletRoundedPanel>();
            var gloss = Box("UpperHighlight", fill, new Vector2(208, 3), new Vector2(104, 3.5f), new Color(1, .65f, .68f, .42f));
            gloss.anchorMin = new Vector2(0, 1);
            gloss.anchorMax = Vector2.one;
            gloss.offsetMin = new Vector2(0, -3);
            gloss.offsetMax = Vector2.zero;
            for (int i = 1; i < 10; i++)
                Box("Segment", track, new Vector2(1.5f, 12), new Vector2(-104 + i * 20.8f, 0), new Color(.025f, .035f, .055f, .6f));
            fillEdge = Box("LiveEdge", track, new Vector2(2, 12), new Vector2(103, 0), new Color(1, .77f, .79f, .9f));
            impactFlash = Box("HitFlash", track, new Vector2(208, 12), Vector2.zero, Color.clear).GetComponent<TabletRoundedPanel>();
            var caption = Text("Caption", body, new Vector2(100, 17), new Vector2(-54, -23), 10);
            caption.text = "VITALIDAD";
            caption.characterSpacing = 2;
            caption.color = new Color(.51f, .63f, .73f);
            caption.alignment = TextAlignmentOptions.Left;
            valueLabel = Text("Value", body, new Vector2(108, 18), new Vector2(50, -23), 12);
            valueLabel.fontStyle = FontStyles.Bold;
            valueLabel.alignment = TextAlignmentOptions.Right;
            valueLabel.color = new Color(.87f, .91f, .96f);
            panel.gameObject.SetActive(false);
        }

        private Bounds BodyBounds()
        {
            var bounds = new Bounds(transform.position + Vector3.up, Vector3.one);
            bool first = true;
            foreach (var renderer in visuals)
            {
                if (renderer == null || !renderer.enabled
                    || (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        public Vector3 DamagePosition => BodyBounds().center + Vector3.up * .3f;

        public void Damage(float amount) => Damage(amount, DamageKind.Normal, false);

        public void Damage(float amount, DamageKind kind, bool critical = false)
        {
            if (kind != DamageKind.Heal) lastDamageAt = lastDamageAtForAnimation = Time.unscaledTime;
            EnemyDamagePopup.Show(this, amount, DamagePosition, kind, critical);
        }

        // Kept for existing callers; popups now alternate sides by themselves.
        public static void ShowDamage(float amount, Vector3 position, int lane = 0)
            => EnemyDamagePopup.Show(null, amount, position, DamageKind.Normal, false);

        internal static RectTransform PopupRoot() => Overlay();
        internal static RectTransform PopupRect(string title, Transform parent, Vector2 size, Vector2 position)
            => Rect(title, parent, size, position);
        internal static TextMeshProUGUI PopupText(string title, Transform parent, Vector2 size, Vector2 position, float fontSize)
            => Text(title, parent, size, position, fontSize);

        private void LateUpdate()
        {
            if (panel == null || readHealth == null) return;
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
            bool visible = view != null && (canShow == null || canShow())
                && !GameJam.Multiplayer.SceneTransitionVideo.IsPresent
                && Vector3.Distance(view.transform.position, transform.position) < 65f;
            if (!visible) { panel.gameObject.SetActive(false); return; }
            float health = Mathf.Max(0, readHealth());
            var bounds = BodyBounds();
            var screen = view.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y + .45f, bounds.center.z));
            visible = health > 0 && screen.z > 0 && screen.x > 0 && screen.x < Screen.width
                && screen.y > 0 && screen.y < Screen.height;
            panel.gameObject.SetActive(visible);
            if (!visible) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, null, out var point);
            float hitAge = Time.unscaledTime - lastDamageAtForAnimation;
            float punch = Mathf.Exp(-hitAge * 12f);
            panel.anchoredPosition = point + new Vector2(Mathf.Sin(hitAge * 55f) * 3f * punch, 0);
            panel.localScale = Vector3.one * (1f + .045f * punch);
            impactFlash.color = new Color(1, .8f, .82f, .65f * punch);
            borderGraphic.color = Color.Lerp(new Color(.17f, .24f, .30f, .95f), new Color(.95f, .2f, .28f), punch);
            float ratio = Mathf.Clamp01(health / maximum);
            displayed = Mathf.MoveTowards(displayed, ratio, Time.unscaledDeltaTime * 3f);
            if (Time.unscaledTime - lastDamageAt > .3f)
                delayed = Mathf.MoveTowards(delayed, ratio, Time.unscaledDeltaTime * .8f);
            delayed = Mathf.Max(delayed, displayed);
            fill.sizeDelta = new Vector2(208 * displayed, 12);
            trail.sizeDelta = new Vector2(208 * delayed, 12);
            fillEdge.anchoredPosition = new Vector2(-104 + 208 * displayed - 1, 0);
            float warning = ratio < .25f ? .16f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 6f)) : 0;
            healthGraphic.color = Color.Lerp(new Color(.92f, .13f, .24f), new Color(1f, .4f, .35f), warning);
            valueLabel.text = Mathf.CeilToInt(health) + " / " + Mathf.CeilToInt(maximum);
        }

        private void OnDestroy() { if (panel != null) Destroy(panel.gameObject); }
    }

    public enum DamageKind { Normal, Burn, Bleed, Poison, Heal }

    // Floating damage numbers: pop on spawn, arc to alternating sides, hold steady, fade.
    // Size is relative to the recent average hit, crits are a big yellow moment, hits on the
    // same enemy merge while the number is still in the air, and never more than 50 on screen.
    public sealed class EnemyDamagePopup : MonoBehaviour
    {
        private const int MaxOnScreen = 50;
        private const float Life = 1.1f, MergeWindow = .55f, PopTime = .12f;
        private static readonly List<EnemyDamagePopup> live = new List<EnemyDamagePopup>();
        private static float averageHit = -1f;
        private static int sideToggle;

        private object owner;
        private DamageKind kind;
        private bool critical;
        private float total, born, side, size;
        private Vector3 origin;
        private RectTransform rect, canvas;
        private TextMeshProUGUI label, critLabel;
        private float flash;
        private VertexGradient baseGradient;
        private CanvasGroup fade;

        public static void Show(object owner, float amount, Vector3 position, DamageKind kind, bool critical)
        {
            if (!Application.isPlaying || amount <= 0 || GameJam.Multiplayer.SceneTransitionVideo.IsPresent) return;
            if (kind != DamageKind.Heal)
                averageHit = averageHit < 0 ? amount : Mathf.Lerp(averageHit, amount, .12f);

            // Still in the air on the same enemy and same type: add to it instead of spawning another.
            if (owner != null)
                for (int i = live.Count - 1; i >= 0; i--)
                {
                    var p = live[i];
                    if (p != null && p.owner == owner && p.kind == kind
                        && Time.unscaledTime - p.born < MergeWindow)
                    {
                        p.total += amount;
                        p.critical |= critical;
                        p.origin = position;
                        p.born = Time.unscaledTime;
                        p.Refresh();
                        return;
                    }
                }

            while (live.Count >= MaxOnScreen)
            {
                var oldest = live[0];
                live.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            var root = EnemyCombatUI.PopupRoot();
            var container = EnemyCombatUI.PopupRect("DamagePopup", root, new Vector2(260, 140), Vector2.zero);
            var popup = container.gameObject.AddComponent<EnemyDamagePopup>();
            popup.Build(owner, amount, position, kind, critical, root);
        }

        public static void ShowCallout(Vector3 position, string calloutText, string badgeText, Color tint, float customScale = 1.25f)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(calloutText) || GameJam.Multiplayer.SceneTransitionVideo.IsPresent) return;

            while (live.Count >= MaxOnScreen)
            {
                var oldest = live[0];
                live.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            var root = EnemyCombatUI.PopupRoot();
            var container = EnemyCombatUI.PopupRect("TrollCalloutPopup", root, new Vector2(380, 140), Vector2.zero);
            var popup = container.gameObject.AddComponent<EnemyDamagePopup>();
            popup.BuildCallout(position, calloutText, badgeText, tint, customScale, root);
        }

        private void BuildCallout(Vector3 position, string calloutText, string badgeText, Color tint, float customScale, RectTransform root)
        {
            owner = null; total = 0f; origin = position; kind = DamageKind.Normal; critical = true;
            canvas = root; rect = (RectTransform)transform;
            born = Time.unscaledTime;
            side = (sideToggle++ & 1) == 0 ? -1f : 1f;
            fade = gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = fade.interactable = false;

            label = EnemyCombatUI.PopupText("Callout", transform, new Vector2(420, 110), Vector2.zero, 30);
            StyleNumber(label);
            label.text = calloutText;
            label.color = Color.white;
            label.enableVertexGradient = true;
            label.colorGradient = baseGradient = Gradient(Color.Lerp(tint, Color.white, .45f), tint * .85f);

            critLabel = EnemyCombatUI.PopupText("Badge", transform, new Vector2(280, 60), new Vector2(-8, 34), 20);
            StyleNumber(critLabel);
            critLabel.text = string.IsNullOrEmpty(badgeText) ? "TROLL!" : badgeText;
            critLabel.fontStyle = FontStyles.UpperCase;
            critLabel.characterSpacing = 3f;
            critLabel.color = Color.white;
            critLabel.enableVertexGradient = true;
            critLabel.colorGradient = Gradient(new Color(1f, .97f, .6f), new Color(1f, .62f, .08f));
            critLabel.transform.localRotation = Quaternion.Euler(0, 0, 7);
            critLabel.gameObject.SetActive(!string.IsNullOrEmpty(badgeText));

            flash = 1f;
            size = Mathf.Clamp(customScale, .85f, 1.85f);
            live.Add(this);
            transform.SetAsLastSibling();
            LateUpdate();
        }

        private void Build(object who, float amount, Vector3 position, DamageKind type, bool crit, RectTransform root)
        {
            owner = who; total = amount; origin = position; kind = type; critical = crit;
            canvas = root; rect = (RectTransform)transform;
            born = Time.unscaledTime;
            side = (sideToggle++ & 1) == 0 ? -1f : 1f;
            fade = gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = fade.interactable = false;

            label = EnemyCombatUI.PopupText("Damage", transform, new Vector2(300, 110), Vector2.zero, 38);
            StyleNumber(label);

            critLabel = EnemyCombatUI.PopupText("Crit", transform, new Vector2(220, 60), new Vector2(-10, 36), 26);
            StyleNumber(critLabel);
            critLabel.text = "CRIT!";
            critLabel.fontStyle = FontStyles.UpperCase;
            critLabel.characterSpacing = 4f;
            critLabel.color = Color.white;
            critLabel.enableVertexGradient = true;
            critLabel.colorGradient = Gradient(new Color(1f, .97f, .6f), new Color(1f, .62f, .08f));
            critLabel.transform.localRotation = Quaternion.Euler(0, 0, 8);

            live.Add(this);
            Refresh();
            LateUpdate();
        }

        private void Refresh()
        {
            string number = Mathf.CeilToInt(total).ToString();
            label.text = kind == DamageKind.Heal ? "+" + number : number;
            label.color = Color.white;
            label.enableVertexGradient = true;
            var tint = critical ? new Color(1f, .8f, .1f) : ColorFor(kind);
            // Light top, richer bottom: reads as a solid "chunky" number instead of flat text.
            label.colorGradient = baseGradient = kind == DamageKind.Normal && !critical
                ? Gradient(Color.white, new Color(.78f, .82f, .9f))
                : Gradient(Color.Lerp(tint, Color.white, .45f), tint * .85f);
            flash = 1f;
            critLabel.gameObject.SetActive(critical);
            float relative = averageHit > 0 ? total / averageHit : 1f;
            size = Mathf.Clamp(.8f + .32f * (relative - 1f), .7f, 1.7f);
            if (critical) size = Mathf.Max(size, 1.6f);
            transform.SetAsLastSibling();
        }

        private static Material numberMaterial;
        private static TMP_FontAsset numberFont;

        // Thick dark outline + hard drop shadow baked into one shared TMP material (no extra text objects).
        private static void StyleNumber(TextMeshProUGUI text)
        {
            if (numberFont == null)
            {
                // Silkscreen (SIL OFL) lives in Resources/UI/Fonts; built as a dynamic TMP font at runtime.
                var source = Resources.Load<Font>("UI/Fonts/Silkscreen-Bold");
                if (source != null) numberFont = TMP_FontAsset.CreateFontAsset(source, 96, 12,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            }
            if (numberFont != null) text.font = numberFont;
            if (numberMaterial == null)
            {
                numberMaterial = new Material(text.fontSharedMaterial) { name = "DamageNumberMaterial" };
                numberMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, .05f);
                numberMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, .22f);
                numberMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(14, 10, 18, 255));
                numberMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                numberMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, .9f));
                numberMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .35f);
                numberMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.5f);
                numberMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, .22f);
                numberMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            }
            text.fontSharedMaterial = numberMaterial;
            // The pixel font is already bold; faux-bold would blur its pixels.
            text.fontStyle = numberFont != null ? FontStyles.Normal : FontStyles.Bold;
            text.characterSpacing = numberFont != null ? 3f : -4f;
        }

        private static VertexGradient Gradient(Color top, Color bottom) => new VertexGradient(top, top, bottom, bottom);

        private static Color ColorFor(DamageKind type)
        {
            switch (type)
            {
                case DamageKind.Burn: return new Color(1f, .55f, .15f);
                case DamageKind.Bleed: return new Color(.95f, .15f, .2f);
                case DamageKind.Poison: return new Color(.55f, .9f, .25f);
                case DamageKind.Heal: return new Color(.35f, 1f, .5f);
                default: return Color.white;
            }
        }

        private void LateUpdate()
        {
            float age = Time.unscaledTime - born;
            // label is null if this popup survived a domain reload: it is stale, never show it.
            if (age >= Life || age < 0f || label == null) { Destroy(gameObject); return; }
            var camera = Camera.main;
            if (camera == null || label == null) return;
            var screen = camera.WorldToScreenPoint(origin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var point);

            // Arc: shoots up and out to one side, then settles (firm) instead of drifting forever.
            float t = Mathf.Clamp01(age / .45f);
            float ease = 1f - (1f - t) * (1f - t);
            float x = side * 55f * ease;
            float y = 40f + 95f * ease - 30f * t * t;
            float shake = critical ? Mathf.Sin(age * 70f) * 4f * Mathf.Exp(-age * 9f) : 0f;
            rect.anchoredPosition = point + new Vector2(x + shake, y);

            // Pop on spawn (or on merge): overshoot wide-and-flat, then spring back to its size.
            float pop = age < PopTime ? Mathf.Lerp(1.8f, 1f, Mathf.SmoothStep(0, 1, age / PopTime))
                : 1f + .08f * Mathf.Sin((age - PopTime) * 30f) * Mathf.Exp(-(age - PopTime) * 14f);
            float squash = age < PopTime ? Mathf.Lerp(1.2f, 1f, age / PopTime) : 1f;
            rect.localScale = new Vector3(size * pop * squash, size * pop / squash, 1f);
            rect.localRotation = Quaternion.Euler(0, 0, -side * 6f * Mathf.Exp(-age * 6f));

            // Brief white flash on hit/merge, then settles into its color.
            if (flash > 0f)
            {
                flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 8f);
                label.colorGradient = new VertexGradient(
                    Color.Lerp(baseGradient.topLeft, Color.white, flash), Color.Lerp(baseGradient.topRight, Color.white, flash),
                    Color.Lerp(baseGradient.bottomLeft, Color.white, flash), Color.Lerp(baseGradient.bottomRight, Color.white, flash));
            }

            bool visible = screen.z > 0 && !GameJam.Multiplayer.SceneTransitionVideo.IsPresent;
            fade.alpha = visible ? 1f - Mathf.SmoothStep(0, 1, (age - .75f) / (Life - .75f)) : 0f;
        }

        private void OnDestroy() => live.Remove(this);
    }
}
