using System.Collections.Generic;
using GameJam.Environment.Neptune;
using GameJam.Inventory;
using GameJam.Localization;
using GameJam.Multiplayer;
using GameJam.Survival;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using PlayerInventory = GameJam.Inventory.Inventory;

namespace GameJam.UI
{
    /// <summary>Local presentation only. Reads the owner's replicated gameplay state.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerHUD : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(13, 23, 29, 235);
        private static readonly Color Accent = new Color32(104, 224, 198, 255);
        private static readonly Color Cream = new Color32(239, 228, 200, 255);
        private MarsFusionAvatar avatar;
        private MarsFusionSession session;
        private PlayerInventory inventory;
        private PlayerMaterialDrop materialDrop;
        private PlayerCold cold;
        private PlayerInteractionAction action;
        private bool marsEnvironment;
        private RectTransform safeRoot;
        private CanvasGroup hud, modal;
        private Image eyeImage, mouthImage, detailIcon;
        private PlayerHelmetGraphic helmet;
        private TMP_Text equipped, detailName, detailDescription, detailAmount, emptyText;
        private RectTransform dropButton;
        private Bar healthBar, staminaBar, environmentBar, actionBar;
        private TMP_FontAsset font;
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Notice> notices = new List<Notice>();
        private readonly Dictionary<MaterialData, int> previous = new Dictionary<MaterialData, int>();
        private MaterialData selected;
        private bool sampled;
        private float nextInventoryPoll;
        private float nextStatusPoll;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        public bool IsInventoryOpen { get; private set; }

        private sealed class Bar
        {
            public RectTransform Root;
            public Image Fill;
            public TMP_Text Label, Value;
        }
        private sealed class Slot
        {
            public MaterialData Material;
            public Image Icon, Background;
            public TMP_Text Name, Amount;
        }
        private sealed class Notice
        {
            public MaterialData Material;
            public int Amount;
            public float Until;
            public CanvasGroup Group;
            public TMP_Text Text;
        }

        public void Build(MarsFusionAvatar source)
        {
            avatar = source;
            session = FindAnyObjectByType<MarsFusionSession>();
            inventory = source.GetComponent<PlayerInventory>();
            materialDrop = source.GetComponent<PlayerMaterialDrop>();
            cold = source.GetComponent<PlayerCold>();
            action = source.GetComponent<PlayerInteractionAction>();
            marsEnvironment = source.GetComponent<GameJam.Environment.Mars.PlayerSandstorm>() != null;
            font = source.tablet != null && source.tablet.skin != null ? source.tablet.skin.tmpFont : TMP_Settings.defaultFontAsset;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("HUD EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            safeRoot = Rect("Safe area", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hud = Group("HUD", safeRoot);
            modal = Group("Inventory overlay", safeRoot);
            BuildHUD();
            BuildInventory();
            Show(modal, false);
            GameLocalization.OnLanguageChanged += RefreshLanguage;
            UpdateSafeArea();
            PollInventory();
            RefreshLanguage();
        }

        private void BuildHUD()
        {
            var tablet = Panel("Tablet", hud.transform, new Vector2(1, 1), new Vector2(-135, -70), new Vector2(220, 60));
            var tabletIcon = Panel("Tablet icon", tablet, new Vector2(.5f, .5f), new Vector2(-78, 0), new Vector2(27, 35));
            Panel("Screen", tabletIcon, new Vector2(.5f, .5f), new Vector2(0, 2), new Vector2(17, 21)).GetComponent<Image>().color = Accent;
            LocalLabel(tablet, "hud.tablet", new Vector2(28, 0), new Vector2(155, 44), 19, "TAB  ·  ");
            Button(tablet, () => { if (CanInteract()) { CloseInventory(); avatar.ToggleTablet(); } });

            var status = Panel("Player status", hud.transform, new Vector2(0, 0), new Vector2(285, 106), new Vector2(520, 164));
            var portrait = Rect("Helmet silhouette", status, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-185, 0), new Vector2(120, 132));
            helmet = portrait.gameObject.AddComponent<PlayerHelmetGraphic>();
            helmet.raycastTarget = false;
            helmet.color = Cream;
            eyeImage = Icon(portrait, new Vector2(0, 1), new Vector2(76, 76));
            mouthImage = Icon(portrait, new Vector2(0, 1), new Vector2(76, 76));
            healthBar = CreateBar(status, new Vector2(65, 0), "hud.health", new Color32(239, 83, 88, 255));
            staminaBar = CreateBar(status, new Vector2(65, -48), "hud.stamina", Accent);
            environmentBar = CreateBar(status, new Vector2(65, 48), "hud.environment", new Color32(89, 185, 255, 255));

            var equipment = Panel("Equipment", hud.transform, new Vector2(1, .5f), new Vector2(-135, 0), new Vector2(220, 164));
            LocalLabel(equipment, "hud.equipped", new Vector2(0, 48), new Vector2(198, 30), 17);
            equipped = Label(equipment, "", Vector2.zero, new Vector2(194, 50), 23);
            var open = Panel("Open inventory", equipment, new Vector2(.5f, .5f), new Vector2(0, -51), new Vector2(200, 38));
            LocalLabel(open, "hud.inventory", Vector2.zero, new Vector2(190, 34), 18, "I  ·  ");
            Button(open, ToggleInventory);
            actionBar = CreateBar(hud.transform, Vector2.zero, "hud.collecting", Accent);
            actionBar.Root.anchorMin = actionBar.Root.anchorMax = new Vector2(.5f, .22f);
            actionBar.Root.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                var row = Panel("Pickup " + i, hud.transform, new Vector2(0, 1), new Vector2(185, -140 - i * 60), new Vector2(320, 50));
                var group = row.gameObject.AddComponent<CanvasGroup>();
                notices.Add(new Notice { Group = group, Text = Label(row, "", Vector2.zero, new Vector2(294, 44), 20) });
                Show(group, false);
            }
        }

        private void BuildInventory()
        {
            var dim = Rect("Dim", modal.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .62f);
            var window = Panel("Inventory", modal.transform, new Vector2(1, .5f), new Vector2(-550, 0), new Vector2(1050, 620));
            LocalLabel(window, "hud.inventory", new Vector2(-265, 266), new Vector2(460, 50), 30);
            var close = Panel("Close", window, new Vector2(.5f, .5f), new Vector2(413, 266), new Vector2(180, 42));
            LocalLabel(close, "hud.close", Vector2.zero, new Vector2(170, 38), 20, "I / ESC  ·  ");
            Button(close, CloseInventory);
            for (int i = 0; i < 8; i++)
            {
                var tile = Panel("Material " + i, window, new Vector2(.5f, .5f), new Vector2(-410 + i % 4 * 143, 108 - i / 4 * 191), new Vector2(132, 177));
                var slot = new Slot
                {
                    Background = tile.GetComponent<Image>(),
                    Icon = Icon(tile, new Vector2(0, 25), new Vector2(74, 74)),
                    Name = Label(tile, "", new Vector2(0, -40), new Vector2(116, 50), 18),
                    Amount = Label(tile, "", new Vector2(0, 68), new Vector2(112, 26), 20)
                };
                Button(tile, () => { if (slot.Material != null) { selected = slot.Material; RefreshInventory(); } });
                slots.Add(slot);
            }
            emptyText = LocalLabel(window, "hud.empty", new Vector2(-190, -225), new Vector2(590, 45), 22);
            var detail = Panel("Detail", window, new Vector2(.5f, .5f), new Vector2(320, -15), new Vector2(350, 475));
            detailIcon = Icon(detail, new Vector2(0, 138), new Vector2(130, 130));
            detailName = Label(detail, "", new Vector2(0, 34), new Vector2(320, 64), 27);
            if (materialDrop == null)
            {
                detailDescription = Label(detail, "", new Vector2(0, -80), new Vector2(308, 150), 21);
                detailAmount = Label(detail, "", new Vector2(0, -202), new Vector2(320, 42), 20);
            }
            else
            {
                detailDescription = Label(detail, "", new Vector2(0, -75), new Vector2(308, 130), 21);
                detailAmount = Label(detail, "", new Vector2(0, -160), new Vector2(320, 42), 20);
                dropButton = Panel("Drop one", detail, new Vector2(.5f, .5f), new Vector2(0, -210), new Vector2(260, 40));
                Label(dropButton, "Soltar 1", Vector2.zero, new Vector2(250, 36), 20);
                Button(dropButton, DropSelectedMaterial);
            }
        }

        private void Update()
        {
            if (avatar == null || !avatar.IsNetworkReady) return;
            UpdateSafeArea();
            bool blocked = (session != null && session.MenuOpen) ||
                SceneTransitionVideo.IsBlockingInput || PortalWhiteTransition.IsBlockingInput ||
                LobbyDepartureEffects.IsBlockingInput || GameJam.Crafting.CraftingBox.IsBlockingInput;
            if (blocked || !avatar.IsAlive || avatar.TabletOpen) CloseInventory();
            Show(hud, !blocked && !avatar.TabletOpen);
            if (!blocked && Keyboard.current != null)
            {
                if (Keyboard.current.iKey.wasPressedThisFrame) ToggleInventory();
                if (Keyboard.current.tabKey.wasPressedThisFrame) CloseInventory();
            }
            if (Time.unscaledTime >= nextInventoryPoll)
            {
                nextInventoryPoll = Time.unscaledTime + .2f;
                PollInventory();
            }
            foreach (var notice in notices)
            {
                float remaining = notice.Until - Time.unscaledTime;
                notice.Group.alpha = Mathf.Clamp01(remaining);
            }
        }

        private void LateUpdate()
        {
            if (avatar == null || !avatar.IsNetworkReady) return;
            // Copy the exact rendered face, including blink and voice animation frames.
            SetIcon(eyeImage, avatar.eyes != null ? avatar.eyes.sprite : avatar.neutralEyes);
            SetIcon(mouthImage, avatar.mouth != null ? avatar.mouth.sprite : avatar.smileMouth);
            helmet.color = avatar.HasAssignedColor ? MarsPlayerIdentityView.Palette[avatar.SuitColorIndex] : Cream;
            if (Time.unscaledTime < nextStatusPoll) return;
            nextStatusPoll = Time.unscaledTime + .1f;
            var health = avatar.HealthSource;
            SetBar(healthBar, health != null ? health.CurrentHealth / Mathf.Max(1, health.MaxHealth) : 0,
                health != null ? $"{health.CurrentHealth:0}/{health.MaxHealth:0}" : "—");
            float maximum = avatar is SurvivalAvatar survival ? survival.MaximumStamina : 100;
            SetBar(staminaBar, avatar.Stamina / maximum, $"{avatar.Stamina:0}/{maximum:0}");
            bool hasCold = cold != null && cold.isActiveAndEnabled;
            SetBar(environmentBar, hasCold ? cold.GetColdPercentage() / 100 : 0,
                hasCold ? $"{cold.GetColdPercentage():0}%" : GameLocalization.Get("hud.no_sensor"));
            string environmentKey = hasCold ? "hud.cold" : marsEnvironment ? "hud.heat" : "hud.environment";
            environmentBar.Label.text = GameLocalization.Get(environmentKey);
            environmentBar.Fill.color = hasCold ? new Color32(89, 185, 255, 255) : new Color32(255, 150, 69, 255);
            equipped.text = GameLocalization.Get(avatar.TabletOpen ? "hud.tablet" : action != null && action.weaponVisual != null && action.weaponVisual.gameObject.activeInHierarchy ? "hud.weapon" : "hud.empty_hands");
            bool busy = action != null && action.IsBusy;
            actionBar.Root.gameObject.SetActive(busy && hud.alpha > 0);
            if (busy)
            {
                actionBar.Label.text = GameLocalization.Get(action.ActionKind == 1 ? "hud.collecting" : "hud.repairing");
                SetBar(actionBar, action.Progress, $"{action.Progress * 100:0}%");
            }
        }

        private bool CanInteract() => avatar != null && avatar.IsNetworkReady && avatar.IsAlive &&
            (session == null || !session.MenuOpen) && !SceneTransitionVideo.IsBlockingInput && !PortalWhiteTransition.IsBlockingInput &&
            !LobbyDepartureEffects.IsBlockingInput && !GameJam.Crafting.CraftingBox.IsBlockingInput && (action == null || !action.IsBusy);

        private void ToggleInventory()
        {
            if (IsInventoryOpen) { CloseInventory(); return; }
            if (!CanInteract() || avatar.TabletOpen || (avatar.LocalUI != null && avatar.LocalUI.IsVisible)) return;
            IsInventoryOpen = true;
            RefreshInventory();
            Show(modal, true);
        }

        public void CloseInventory()
        {
            IsInventoryOpen = false;
            if (modal != null) Show(modal, false);
        }

        // Called by the pause handler so Escape closes the inventory before opening pause.
        public bool ConsumeEscape()
        {
            if (!IsInventoryOpen) return false;
            CloseInventory();
            return true;
        }

        private void PollInventory()
        {
            if (inventory == null || !inventory.StateBufferIsValid) return;
            var contents = inventory.GetContents();
            bool changed = contents.Count != previous.Count;
            foreach (var pair in contents)
            {
                previous.TryGetValue(pair.Key, out int old);
                if (old != pair.Value) changed = true;
                if (sampled && pair.Value > old) Notify(pair.Key, pair.Value - old);
            }
            previous.Clear();
            foreach (var pair in contents) previous.Add(pair.Key, pair.Value);
            sampled = true;
            if (changed && IsInventoryOpen) RefreshInventory();
        }

        private void Notify(MaterialData material, int amount)
        {
            Notice target = notices[0];
            foreach (var notice in notices) if (notice.Until < target.Until) target = notice;
            target.Material = material;
            target.Amount = amount;
            target.Until = Time.unscaledTime + 4;
            target.Text.text = string.Format(GameLocalization.Get("hud.pickup"), amount, MaterialName(material));
        }

        private static string MaterialName(MaterialData material) => GameLocalization.Get("material." + material.id + ".name", material.materialName);

        private void RefreshLanguage()
        {
            nextStatusPoll = 0;
            foreach (var notice in notices)
                if (notice.Material != null) notice.Text.text = string.Format(GameLocalization.Get("hud.pickup"), notice.Amount, MaterialName(notice.Material));
            RefreshInventory();
        }

        private void RefreshInventory()
        {
            var contents = inventory != null ? inventory.GetContents() : new Dictionary<MaterialData, int>();
            var entries = new List<MaterialData>(contents.Keys);
            entries.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            if (selected == null || !contents.ContainsKey(selected)) selected = entries.Count > 0 ? entries[0] : null;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                slot.Material = i < entries.Count ? entries[i] : null;
                SetIcon(slot.Icon, slot.Material != null ? slot.Material.icon : null);
                slot.Name.text = slot.Material != null ? MaterialName(slot.Material) : "—";
                slot.Amount.text = slot.Material != null ? "×" + contents[slot.Material] : "";
                slot.Background.color = slot.Material != null && slot.Material == selected ? new Color32(37, 77, 77, 255) : Ink;
            }
            emptyText.gameObject.SetActive(entries.Count == 0);
            SetIcon(detailIcon, selected != null ? selected.icon : null);
            detailName.text = selected != null ? MaterialName(selected) : GameLocalization.Get("hud.select");
            detailDescription.text = selected != null ? GameLocalization.Get("material." + selected.id + ".description", selected.description) : "";
            detailAmount.text = selected != null ? string.Format(GameLocalization.Get("hud.quantity"), contents[selected]) : "";
            if (dropButton != null)
            {
                dropButton.gameObject.SetActive(selected != null && contents.ContainsKey(selected) && contents[selected] > 0);
            }
        }

        /// <summary>
        /// Solicita soltar una unidad del material seleccionado.
        /// </summary>
        private void DropSelectedMaterial()
        {
            if (!IsInventoryOpen || selected == null || materialDrop == null || dropButton == null)
            {
                return;
            }

            Button button = dropButton.GetComponent<Button>();
            if (button == null || !button.interactable)
            {
                return;
            }

            button.interactable = false;
            bool dropped = materialDrop.TryDropOne(selected);
            if (dropped)
            {
                RefreshInventory();
            }

            button.interactable = dropButton.gameObject.activeSelf;
        }

        private void UpdateSafeArea()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (lastSafeArea == Screen.safeArea && lastScreen == size) return;
            lastSafeArea = Screen.safeArea;
            lastScreen = size;
            safeRoot.anchorMin = new Vector2(lastSafeArea.xMin / Mathf.Max(1, size.x), lastSafeArea.yMin / Mathf.Max(1, size.y));
            safeRoot.anchorMax = new Vector2(lastSafeArea.xMax / Mathf.Max(1, size.x), lastSafeArea.yMax / Mathf.Max(1, size.y));
        }

        private Bar CreateBar(Transform parent, Vector2 position, string key, Color color)
        {
            var root = Rect("Status bar", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, new Vector2(344, 42));
            var track = Panel("Track", root, new Vector2(.5f, .5f), new Vector2(24, -8), new Vector2(148, 12));
            var fill = Rect("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            fill.raycastTarget = false;
            fill.color = color;
            return new Bar { Root = root, Fill = fill,
                Label = LocalLabel(root, key, new Vector2(-109, 0), new Vector2(118, 38), 17),
                Value = Label(root, "", new Vector2(128, 0), new Vector2(82, 38), 17) };
        }

        private static void SetBar(Bar bar, float fraction, string value)
        {
            var rect = bar.Fill.rectTransform;
            var anchor = rect.anchorMax;
            anchor.x = Mathf.Clamp01(fraction);
            rect.anchorMax = anchor;
            if (bar.Value.text != value) bar.Value.text = value;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.anchoredPosition = pos; rect.sizeDelta = size;
            return rect;
        }

        private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rect = Rect(name, parent, anchor, anchor, pos, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Ink; image.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, .6f);
            outline.effectDistance = new Vector2(1, -1);
            return rect;
        }

        private TMP_Text Label(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize)
        {
            var rect = Rect("Text", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), pos, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text; label.fontSize = fontSize; label.color = Cream;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true; label.fontSizeMin = fontSize - 4; label.fontSizeMax = fontSize;
            label.raycastTarget = false; label.richText = false;
            return label;
        }

        private TMP_Text LocalLabel(Transform parent, string key, Vector2 pos, Vector2 size, int fontSize, string prefix = "")
        {
            var label = Label(parent, "", pos, size, fontSize);
            var localized = label.gameObject.AddComponent<LocalizedText>();
            localized.Prefix = prefix;
            localized.Key = key;
            return label;
        }

        private static Image Icon(Transform parent, Vector2 pos, Vector2 size)
        {
            var rect = Rect("Icon", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), pos, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false; image.preserveAspect = true;
            image.enabled = false;
            return image;
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image.sprite != sprite) image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private static void Button(RectTransform rect, UnityEngine.Events.UnityAction action)
        {
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
        }

        private static CanvasGroup Group(string name, Transform parent)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rect.gameObject.AddComponent<Canvas>();
            rect.gameObject.AddComponent<GraphicRaycaster>();
            return rect.gameObject.AddComponent<CanvasGroup>();
        }

        private static void Show(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1 : 0;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private void OnDestroy()
        {
            GameLocalization.OnLanguageChanged -= RefreshLanguage;
        }
    }
}
