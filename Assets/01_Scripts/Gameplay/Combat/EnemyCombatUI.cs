using System;
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

        public void Damage(float amount)
        {
            lastDamageAt = lastDamageAtForAnimation = Time.unscaledTime;
            ShowDamage(amount, DamagePosition, damageSequence++ % 5);
        }

        public static void ShowDamage(float amount, Vector3 position, int lane = 0)
        {
            if (amount <= 0 || GameJam.Multiplayer.SceneTransitionVideo.IsPresent) return;
            var container = Rect("DamagePopup", Overlay(), new Vector2(240, 100), Vector2.zero);
            var text = Text("Damage", container, new Vector2(240, 100), Vector2.zero, 56);
            text.text = "<size=65%>−</size>" + Mathf.CeilToInt(amount);
            text.fontStyle = FontStyles.Bold | FontStyles.Italic;
            text.characterSpacing = -1f;
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(new Color(1f, .38f, .4f), new Color(1f, .38f, .4f),
                new Color(.8f, .025f, .09f), new Color(.8f, .025f, .09f));
            text.outlineWidth = .28f;
            text.outlineColor = new Color32(18, 3, 8, 255);
            var shadow = Text("NumberDepth", container, new Vector2(240, 100), new Vector2(3, -4), 56);
            shadow.font = text.font;
            shadow.text = text.text;
            shadow.fontStyle = text.fontStyle;
            shadow.characterSpacing = text.characterSpacing;
            shadow.color = new Color(.055f, .005f, .015f, .95f);
            shadow.transform.SetAsFirstSibling();
            var popup = container.gameObject.AddComponent<EnemyDamagePopup>();
            popup.Initialize(text, position, overlay, lane);
        }

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

    public sealed class EnemyDamagePopup : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private RectTransform canvas, rect;
        private Vector3 origin;
        private float born, sideways, tilt, laneOffset;
        private CanvasGroup fade;
        private RectTransform[] sparks;
        public void Initialize(TextMeshProUGUI text, Vector3 point, RectTransform parent, int lane)
        {
            label = text; canvas = parent; rect = (RectTransform)transform;
            origin = point; born = Time.unscaledTime;
            // Five deterministic lanes separate consecutive shots without changing gameplay randomness.
            laneOffset = lane == 0 ? 0 : lane == 1 ? -48 : lane == 2 ? 48 : lane == 3 ? -24 : 24;
            sideways = laneOffset < 0 ? -26f : 26f;
            tilt = laneOffset < 0 ? 5f : -5f;
            fade = gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            fade.interactable = false;
            sparks = new RectTransform[3];
            for (int i = 0; i < sparks.Length; i++)
            {
                var go = new GameObject("ImpactSpark", typeof(RectTransform));
                var spark = (RectTransform)go.transform;
                spark.SetParent(transform, false);
                spark.anchorMin = spark.anchorMax = new Vector2(.5f, .5f);
                spark.sizeDelta = new Vector2(12, 2);
                spark.localRotation = Quaternion.Euler(0, 0, i * 120f + 20f);
                var graphic = go.AddComponent<TabletRoundedPanel>();
                graphic.radius = 1;
                graphic.color = new Color(1, .2f, .25f);
                graphic.raycastTarget = false;
                sparks[i] = spark;
            }
            LateUpdate();
        }
        private void LateUpdate()
        {
            float age = Time.unscaledTime - born;
            if (age >= 1.35f) { Destroy(gameObject); return; }
            var camera = Camera.main;
            if (camera == null || label == null) return;
            var screen = camera.WorldToScreenPoint(origin);
            bool visible = screen.z > 0 && !GameJam.Multiplayer.SceneTransitionVideo.IsPresent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var point);
            float rise = 1f - Mathf.Exp(-age * 4f);
            rect.anchoredPosition = point + new Vector2(laneOffset + sideways * rise,
                32f + rise * 60f + age * 15f);
            float scale = age < .075f ? Mathf.Lerp(.65f, 1.32f, 1f - Mathf.Pow(1f - age / .075f, 3f))
                : age < .22f ? Mathf.Lerp(1.32f, 1f, Mathf.SmoothStep(0, 1, (age - .075f) / .145f))
                : 1f - .06f * Mathf.Clamp01((age - 1f) / .35f);
            rect.localScale = Vector3.one * scale;
            rect.localRotation = Quaternion.Euler(0, 0, tilt * Mathf.Exp(-age * 7f));
            fade.alpha = visible ? Mathf.Clamp01(age / .025f)
                * (1f - Mathf.SmoothStep(0, 1, (age - .75f) / .6f)) : 0f;
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = (i * 120f + 20f) * Mathf.Deg2Rad;
                sparks[i].anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (25 + age * 85);
                sparks[i].localScale = Vector3.one * Mathf.Clamp01(1f - age / .28f);
            }
        }
    }
}
