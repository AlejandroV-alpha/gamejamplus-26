using GameJam.Collectibles;
using GameJam.Inventory;
using GameJam.Localization;
using GameJam.Multiplayer;
using GameJam.UI;
using UnityEngine;
using PlayerInventory = GameJam.Inventory.Inventory;

namespace GameJam.Gameplay
{
    /// <summary>
    /// HUD local: tarjeta "[E] Recoger" sobre el objeto mas cercano del suelo y
    /// aviso animado al soltar un objeto del inventario con G.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PickupPromptFeedback : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float searchRadius = 2.5f;

        private static PickupPromptFeedback local;
        private static readonly Collider[] Buffer = new Collider[48];

        private PlayerCollector collector;
        private MarsFusionAvatar avatar;
        private PlayerInventory inventory;
        private GameJam.Survival.SurvivalRevive revive;
        private Component target;          // MaterialPickup o HealthPickup
        private Component shown;
        private float nextLookup, alpha;
        private readonly InteractionPromptCard.Row[] rows = new InteractionPromptCard.Row[1];

        private string dropName;
        private Texture dropIcon;
        private float dropTime = -10f;
        private GUIStyle toastStyle, toastNameStyle;
        private const float DropToastSeconds = 2.6f;

        private void Awake()
        {
            collector = GetComponent<PlayerCollector>();
            avatar = GetComponent<MarsFusionAvatar>();
            inventory = GetComponent<PlayerInventory>();
            revive = GetComponent<GameJam.Survival.SurvivalRevive>();
        }

        private bool IsLocal => collector != null && collector.Object != null && collector.Object.IsValid && collector.HasStateAuthority;

        /// <summary>Lo llama el HUD justo despues de soltar un objeto con exito.</summary>
        public static void NotifyDropped(MaterialData material, string displayName)
        {
            if (local == null || material == null) return;
            local.dropName = displayName;
            local.dropIcon = material.icon != null ? material.icon.texture : null;
            local.dropTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (!IsLocal) return;
            local = this;
            if (Time.unscaledTime < nextLookup) return;
            nextLookup = Time.unscaledTime + .12f;
            target = FindNearest();
        }

        private Component FindNearest()
        {
            if (avatar == null || !avatar.IsAlive || avatar.TabletOpen) return null;
            if (revive != null && revive.NearbyTeammate != null) return null; // E es para reanimar
            int count = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, Buffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Component best = null;
            float bestDistance = searchRadius * searchRadius;
            for (int i = 0; i < count; i++)
            {
                var c = Buffer[i];
                if (c == null) continue;
                Component candidate = null;
                var health = c.GetComponentInParent<HealthPickup>();
                if (health != null && collector != null && health.CanCollect(collector.Object)) candidate = health;
                else
                {
                    var pickup = c.GetComponentInParent<MaterialPickup>();
                    if (pickup != null && pickup.IsAvailable() && pickup.PickupMaterial != null) candidate = pickup;
                }
                if (candidate == null) continue;
                float d = (candidate.transform.position - transform.position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = candidate; }
            }
            return best;
        }

        private void OnGUI()
        {
            if (!IsLocal) return;
            if (Event.current.type == EventType.Repaint)
            {
                if (target != null) shown = target;
                alpha = Mathf.MoveTowards(alpha, target != null ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            }
            DrawPickupCard();
            DrawDropToast();
        }

        private void DrawPickupCard()
        {
            var cam = Camera.main;
            if (alpha <= .01f || shown == null || cam == null) return;

            string header, name;
            Texture icon = null;
            string iconSprite = null;
            bool fits = true;
            Color accent;
            if (shown is MaterialPickup pickup)
            {
                var data = pickup.PickupMaterial;
                if (data == null) return;
                name = GameLocalization.Get("material." + data.id + ".name", data.materialName);
                if (pickup.Amount > 1) name += "  ×" + pickup.Amount;
                bool gear = data.kind != CarriedItemKind.Material;
                header = gear ? "EQUIPO" : "MATERIAL";
                accent = gear ? new Color(1f, .68f, .25f) : new Color(.35f, .95f, .85f);
                icon = data.icon != null ? data.icon.texture : null;
                if (icon == null) iconSprite = "collect";
                fits = pickup.FitsInventory(inventory);
            }
            else
            {
                header = "SUMINISTRO";
                name = "Kit médico";
                accent = new Color(1f, .4f, .45f);
                iconSprite = "health";
            }

            Bounds bounds = new Bounds(shown.transform.position, Vector3.zero);
            foreach (var r in shown.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            Vector3 screen = cam.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y + .25f, bounds.center.z));
            rows[0] = new InteractionPromptCard.Row
            {
                Key = "E",
                Action = fits ? "Recoger" : "Inventario lleno",
                Enabled = fits
            };
            InteractionPromptCard.Draw(screen, header, name, accent, rows, alpha, iconSprite, icon);
        }

        /// <summary>Pastilla inferior: "Soltaste Pistola · [E] para recogerlo".</summary>
        private void DrawDropToast()
        {
            float t = Time.unscaledTime - dropTime;
            if (t < 0f || t > DropToastSeconds || string.IsNullOrEmpty(dropName)) return;
            float s = InteractionPromptCard.Scale;
            if (toastStyle == null)
            {
                toastStyle = new GUIStyle(GameJam.UI.UiFont.Label()) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), wordWrap = false };
                toastNameStyle = new GUIStyle(toastStyle) { fontStyle = FontStyle.Bold };
            }
            toastStyle.fontSize = GameJam.UI.UiFont.Px(17 * s);
            toastNameStyle.fontSize = GameJam.UI.UiFont.Px(17 * s);

            // Entra deslizandose, se queda y se desvanece.
            float a = Mathf.Clamp01(t / .18f) * Mathf.Clamp01((DropToastSeconds - t) / .45f);
            float rise = (1f - Mathf.Clamp01(t / .25f)) * 14f * s;

            const string Prefix = "Soltaste ";
            const string Suffix = "Pulsa [E] cerca para recogerlo";
            float iconW = dropIcon != null ? 28 * s + 10 * s : 0f;
            float prefixW = toastStyle.CalcSize(new GUIContent(Prefix)).x;
            float nameW = toastNameStyle.CalcSize(new GUIContent(dropName)).x;
            float sepW = 22 * s;
            float suffixW = toastStyle.CalcSize(new GUIContent(Suffix.Replace("[", "").Replace("]", ""))).x + 30 * s;
            float h = 48 * s, padX = 20 * s;
            float w = padX * 2 + iconW + prefixW + nameW + sepW + suffixW;
            Rect safe = Screen.safeArea;
            float x = safe.x + (safe.width - w) * .5f;
            float y = Screen.height - safe.yMin - 300 * s + rise;

            var previous = GUI.color;
            try
            {
                var pill = new Rect(x, y, w, h);
                InteractionPromptCard.Fill(new Rect(pill.x, pill.y + 3 * s, pill.width, pill.height), new Color(0, 0, 0, .35f), a, h * .5f);
                InteractionPromptCard.Fill(pill, InteractionPromptCard.Panel, a, h * .5f);
                var orange = new Color(1f, .68f, .25f);
                InteractionPromptCard.Fill(new Rect(pill.x + 1, pill.y + 1, pill.width - 2, 2 * s), InteractionPromptCard.Fade(orange, .5f), a, s);

                float cx = x + padX;
                if (dropIcon != null)
                {
                    GUI.color = InteractionPromptCard.Fade(Color.white, a);
                    GUI.DrawTexture(new Rect(cx, y + (h - 28 * s) * .5f, 28 * s, 28 * s), dropIcon, ScaleMode.ScaleToFit, true);
                    GUI.color = Color.white;
                    cx += iconW;
                }
                toastStyle.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, a);
                GUI.Label(new Rect(cx, y, prefixW + 2, h), Prefix, toastStyle);
                cx += prefixW;
                toastNameStyle.normal.textColor = InteractionPromptCard.Fade(orange, a);
                GUI.Label(new Rect(cx, y, nameW + 2, h), dropName, toastNameStyle);
                cx += nameW;
                InteractionPromptCard.Fill(new Rect(cx + sepW * .5f - .5f * s, y + 12 * s, Mathf.Max(1f, s), h - 24 * s), new Color(1, 1, 1, .15f), a, 0);
                cx += sepW;
                InteractionPromptCard.DrawInline(new Rect(cx, y, suffixW, h), Suffix, toastStyle, InteractionPromptCard.TextSoft, a, s);
            }
            finally { GUI.color = previous; }
        }

        private void OnDestroy()
        {
            if (local == this) local = null;
        }
    }
}
