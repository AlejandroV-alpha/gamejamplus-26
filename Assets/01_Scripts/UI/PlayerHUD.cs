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
using UnityEngine.SceneManagement;
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
        private bool showEnvironment;
        private RectTransform safeRoot;
        private CanvasGroup hud;
        private Image eyeImage, mouthImage;
        private PlayerHelmetGraphic helmet;
        private TMP_Text equipped, quickbarCaption, selectedName, carryFeedback;
        private RectTransform quickbarRoot;
        private int selectedSlot;
        private float carryFeedbackUntil;
        private Bar healthBar, staminaBar, environmentBar, actionBar;
        private Image actionIcon;
        private TMP_FontAsset font;
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Notice> notices = new List<Notice>();
        private readonly Dictionary<MaterialData, int> previous = new Dictionary<MaterialData, int>();
        private bool sampled;
        private float nextInventoryPoll;
        private float nextStatusPoll;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        public bool IsInventoryOpen => false;

        private sealed class Bar
        {
            public RectTransform Root;
            public Image Fill;
            public TMP_Text Label, Value;
            public float Target = float.NaN;
        }
        private sealed class Slot
        {
            public MaterialData Material;
            public Image Icon, Background;
            public TMP_Text Name;
            public GameObject Selection;
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
            showEnvironment = cold != null && cold.isActiveAndEnabled &&
                cold.IsColdEnvironment;
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
            BuildHUD();
            BuildQuickbar();
            GameLocalization.OnLanguageChanged += RefreshLanguage;
            UpdateSafeArea();
            PollInventory();
            RefreshLanguage();
            if (SciFiGameMenuManager.IsLobbyScene)
                gameObject.AddComponent<LobbyGameplayTutorial>().Build(avatar, safeRoot, font);
            else if (marsEnvironment || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Mars"))
                gameObject.AddComponent<MarsDemoWalkthroughHUD>().Build(avatar, safeRoot, font);
            BuildCrewActionsHint();
        }

        private void BuildHUD()
        {
            var tablet = Panel("Tablet", hud.transform, new Vector2(1, 1), new Vector2(-135, -70), new Vector2(220, 60));
            ExpeditionSprites.Skin(tablet.GetComponent<Image>(), "button");
            ExpeditionSprites.Icon(tablet, "tablet", new Vector2(-78, 0), new Vector2(32, 32));
            LocalLabel(tablet, "hud.tablet", new Vector2(28, 0), new Vector2(155, 44), 19, "TAB  ·  ");
            Button(tablet, () => { if (CanInteract()) { CloseInventory(); avatar.ToggleTablet(); } });

            var status = Panel("Player status", hud.transform, new Vector2(0, 0), new Vector2(285, showEnvironment ? 106 : 90), new Vector2(520, showEnvironment ? 164 : 132));
            ExpeditionSprites.Skin(status.GetComponent<Image>(), "panel");
            var portrait = Rect("Helmet silhouette", status, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-185, 0), new Vector2(120, 132));
            helmet = portrait.gameObject.AddComponent<PlayerHelmetGraphic>();
            helmet.raycastTarget = false;
            helmet.color = Cream;
            eyeImage = Icon(portrait, new Vector2(0, 1), new Vector2(76, 76));
            mouthImage = Icon(portrait, new Vector2(0, 1), new Vector2(76, 76));
            healthBar = CreateBar(status, new Vector2(65, showEnvironment ? 0 : 24), "hud.health", new Color32(239, 83, 88, 255));
            staminaBar = CreateBar(status, new Vector2(65, -24 - (showEnvironment ? 24 : 0)), "hud.stamina", Accent);
            environmentBar = CreateBar(status, new Vector2(65, 48), "hud.environment", new Color32(89, 185, 255, 255));
            environmentBar.Root.gameObject.SetActive(showEnvironment);

            var equipment = Panel("Equipment", hud.transform, new Vector2(1, .5f), new Vector2(-135, 0), new Vector2(220, 112));
            ExpeditionSprites.Skin(equipment.GetComponent<Image>(), "panel");
            LocalLabel(equipment, "hud.equipped", new Vector2(0, 33), new Vector2(198, 30), 17);
            equipped = Label(equipment, "", new Vector2(0,-14), new Vector2(194, 50), 23);
            actionBar = CreateBar(hud.transform, Vector2.zero, "hud.collecting", Accent);
            actionIcon = ExpeditionSprites.Icon(actionBar.Root, "collect", new Vector2(-147, 10), new Vector2(24, 24));
            actionBar.Root.sizeDelta = new Vector2(390, 82);
            var actionBackground = actionBar.Root.gameObject.AddComponent<Image>();
            actionBackground.color = Ink;
            ExpeditionSprites.Skin(actionBackground, "panel");
            actionBackground.raycastTarget = false;
            actionBar.Root.anchorMin = actionBar.Root.anchorMax = new Vector2(.5f, .32f);
            actionBar.Root.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                var row = Panel("Pickup " + i, hud.transform, new Vector2(0, 1), new Vector2(185, -140 - i * 60), new Vector2(320, 50));
                ExpeditionSprites.Skin(row.GetComponent<Image>(), "panel");
                var group = row.gameObject.AddComponent<CanvasGroup>();
                notices.Add(new Notice { Group = group, Text = Label(row, "", Vector2.zero, new Vector2(294, 44), 20) });
                Show(group, false);
            }
        }

        private void BuildCrewActionsHint()
        {
            var box = Panel("Crew actions hint", hud.transform, new Vector2(1, 0), new Vector2(-225, 58), new Vector2(410, 82));
            ExpeditionSprites.Skin(box.GetComponent<Image>(), "panel");
            var title = Label(box, "FÍSICA CO-OP & TROLL (LETHAL / REPO)", new Vector2(0, 24), new Vector2(390, 20), 13);
            title.color = Accent;
            title.fontStyle = FontStyles.Bold;
            var row1 = Label(box, "[F] Empujón Físico (Ragdoll)  ·  [Y] Flop / Buceo", new Vector2(0, 2), new Vector2(390, 18), 12);
            row1.color = Cream;
            var row2 = Label(box, "[E] Reanimar  ·  [Clic] Arrastrar (manos vacías)  ·  [T] Saludo", new Vector2(0, -18), new Vector2(390, 18), 12);
            row2.color = Cream;
        }

        private void BuildQuickbar()
        {
            if (quickbarRoot != null) { quickbarRoot.gameObject.SetActive(false); Destroy(quickbarRoot.gameObject); }
            slots.Clear();
            int count = inventory != null ? inventory.SlotCount : 6;
            float step = Mathf.Min(92, 680f / count);
            quickbarRoot = Panel("Quickbar", hud.transform, new Vector2(.5f,0), new Vector2(0,91), new Vector2(step*count+20,112));
            ExpeditionSprites.Skin(quickbarRoot.GetComponent<Image>(), "panel");
            quickbarCaption = Label(quickbarRoot,"",new Vector2(0,-70),new Vector2(720,30),17);
            selectedName = Label(quickbarRoot,"",new Vector2(0,78),new Vector2(600,32),19);
            carryFeedback = Label(quickbarRoot,"",new Vector2(0,116),new Vector2(720,34),21);
            carryFeedback.color = new Color32(255,178,102,255);
            for (int i=0;i<count;i++)
            {
                int index=i;
                var tile=Panel("Carry slot "+(i+1),quickbarRoot,new Vector2(.5f,.5f),new Vector2((i-(count-1)*.5f)*step,0),new Vector2(step-8,96));
                var marker=Panel("Selected",tile,new Vector2(.5f,0),new Vector2(0,4),new Vector2(step-16,4));
                marker.GetComponent<Image>().color=Accent;
                var slot=new Slot{Background=tile.GetComponent<Image>(),Icon=Icon(tile,new Vector2(0,7),new Vector2(step-24,54)),Name=Label(tile,i==9?"0":(i+1).ToString(),new Vector2(0,-32),new Vector2(step-18,22),16),Selection=marker.gameObject};
                slots.Add(slot);
                Button(tile,()=>{if(CanInteract() && inventory.SelectSlot(index)){RefreshInventory();}});
            }
            selectedSlot=Mathf.Clamp(selectedSlot,0,count-1);
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
            foreach (var slot in slots)
                slot.Icon.rectTransform.localScale = Vector3.MoveTowards(slot.Icon.rectTransform.localScale, Vector3.one, Time.unscaledDeltaTime * 2.5f);
            AnimateBar(healthBar);
            AnimateBar(staminaBar);
            AnimateBar(environmentBar);
            AnimateBar(actionBar);
            if (!blocked && Keyboard.current != null)
            {
                if (CanInteract() && !avatar.TabletOpen && !VoicePreferences.CapturingBinding)
                {
                    for(int i=0;i<slots.Count;i++)
                        if(Keyboard.current[i==9 ? Key.Digit0 : Key.Digit1+i].wasPressedThisFrame) {inventory.SelectSlot(i);RefreshInventory();}
                    if(Mouse.current != null && Mathf.Abs(Mouse.current.scroll.ReadValue().y)>.1f)
                    {inventory.SelectSlot((selectedSlot+(Mouse.current.scroll.ReadValue().y>0?-1:1)+slots.Count)%slots.Count);RefreshInventory();}
                    if(Keyboard.current.gKey.wasPressedThisFrame) DropSelectedMaterial();
                }
            }
            if (Time.unscaledTime >= nextInventoryPoll)
            {
                nextInventoryPoll = Time.unscaledTime + .2f;
                PollInventory();
            }
            if(carryFeedback != null && Time.unscaledTime > carryFeedbackUntil) carryFeedback.text="";
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
            bool hasCold = showEnvironment && cold != null && cold.isActiveAndEnabled;
            SetBar(environmentBar, hasCold ? cold.GetColdPercentage() / 100 : 0,
                hasCold ? $"{cold.GetColdPercentage():0}%" : GameLocalization.Get("hud.no_sensor"));
            string environmentKey = hasCold ? "hud.cold" : marsEnvironment ? "hud.heat" : "hud.environment";
            environmentBar.Label.text = GameLocalization.Get(environmentKey);
            environmentBar.Fill.color = hasCold ? new Color32(89, 185, 255, 255) : new Color32(255, 150, 69, 255);
            var selectedItem = inventory != null ? inventory.SelectedItem : null;
            equipped.text = avatar.TabletOpen ? GameLocalization.Get("hud.tablet") : selectedItem != null && selectedItem.kind != CarriedItemKind.Material ? MaterialName(selectedItem) : GameLocalization.Get("hud.empty_hands");
            bool busy = action != null && action.IsBusy;
            actionBar.Root.gameObject.SetActive(busy && hud.alpha > 0);
            if (busy)
            {
                if (actionIcon != null) actionIcon.sprite = ExpeditionSprites.Get(action.ActionKind == 1 ? "collect" : "repair");
                actionBar.Label.text = GameLocalization.Get(action.ActionKind == 1 ? "hud.collecting" : "hud.repairing");
                SetBar(actionBar, action.Progress, $"{action.Progress * 100:0}%");
            }
        }

        private bool CanInteract() => avatar != null && avatar.IsNetworkReady && avatar.IsAlive &&
            (session == null || !session.MenuOpen) && !SceneTransitionVideo.IsBlockingInput && !PortalWhiteTransition.IsBlockingInput &&
            !LobbyDepartureEffects.IsBlockingInput && !GameJam.Crafting.CraftingBox.IsBlockingInput && (action == null || !action.IsBusy);

        // Kept for existing tablet/menu callers; the quickbar never captures the cursor.
        public void CloseInventory() { }
        public bool ConsumeEscape() => false;

        public void NotifyCarryFull()
        {
            if(carryFeedback==null)return;
            carryFeedback.text=GameLocalization.Get("hud.carry_full");carryFeedbackUntil=Time.unscaledTime+3;
        }

        private void PollInventory()
        {
            if (inventory == null || !inventory.StateBufferIsValid) return;
            var contents = inventory.GetContents();
            foreach (var pair in contents)
            {
                previous.TryGetValue(pair.Key, out int old);
                if (sampled && pair.Value > old) Notify(pair.Key, pair.Value - old);
            }
            previous.Clear();
            foreach (var pair in contents) previous.Add(pair.Key, pair.Value);
            sampled = true;
            RefreshInventory();
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
            if(inventory==null)return;
            selectedSlot = inventory.SelectedSlot;
            if(slots.Count != inventory.SlotCount)BuildQuickbar();
            for(int i=0;i<slots.Count;i++)
            {
                var slot=slots[i];slot.Material=inventory.GetSlotMaterial(i);
                var icon = slot.Material != null ? slot.Material.icon : null;
                if (icon != null && icon != slot.Icon.sprite) slot.Icon.rectTransform.localScale = Vector3.one * 1.18f;
                SetIcon(slot.Icon, icon);
                bool active=i==selectedSlot;
                slot.Selection.SetActive(active);
                slot.Background.color=active?new Color32(37,77,77,255):Ink;
                ExpeditionSprites.Skin(slot.Background, active ? "slot_active" : "slot");
                slot.Name.color=active?Accent:Cream;
            }
            var material=inventory.GetSlotMaterial(selectedSlot);
            var gear=avatar != null ? avatar.GetComponent<PlayerEquipment>() : null;
            if(material != null && material.kind == CarriedItemKind.Radio)
                selectedName.text=GameLocalization.Get(gear != null && gear.IsRadioChannelOpen ? "radio.channel_open" : "radio.channel_closed");
            else selectedName.text=material!=null?MaterialName(material):GameLocalization.Get("hud.carry_empty");
            quickbarCaption.text=string.Format(GameLocalization.Get("hud.carry_help"),inventory.UsedSlots,inventory.SlotCount);
        }

        private void DropSelectedMaterial()
        {
            if(!CanInteract() || avatar.TabletOpen || materialDrop==null || inventory==null)return;
            if(inventory.GetSlotMaterial(selectedSlot)==null)return;
            if(materialDrop.TryDropSlot(selectedSlot))RefreshInventory();
            else { carryFeedback.text=GameLocalization.Get("hud.carry_drop_blocked");carryFeedbackUntil=Time.unscaledTime+3; }
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
            var track = Panel("Track", root, new Vector2(.5f, .5f), new Vector2(0, -13), new Vector2(320, 10));
            ExpeditionSprites.Skin(track.GetComponent<Image>(), "bar_track");
            var fill = Rect("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            fill.raycastTarget = false;
            fill.color = color;
            if (key == "hud.stamina")
            {
                ExpeditionSprites.Skin(fill, "bar_fill");
                fill.type = Image.Type.Simple;
            }
            var iconName = key == "hud.health" ? "health" : key == "hud.stamina" ? "stamina" : null;
            ExpeditionSprites.Icon(root, iconName, new Vector2(-147, 10), new Vector2(24, 24));
            var label = LocalLabel(root, key, new Vector2(-43, 10), new Vector2(166, 24), 17);
            label.alignment = TextAlignmentOptions.Left;
            var value = Label(root, "", new Vector2(114, 10), new Vector2(92, 24), 18);
            value.alignment = TextAlignmentOptions.Right;
            value.fontStyle = FontStyles.Bold;
            return new Bar { Root = root, Fill = fill, Label = label, Value = value };
        }

        private static void SetBar(Bar bar, float fraction, string value)
        {
            bool firstSample = float.IsNaN(bar.Target);
            bar.Target = Mathf.Clamp01(fraction);
            if (firstSample) AnimateBar(bar, 1f);
            if (bar.Value.text != value) bar.Value.text = value;
        }

        private static void AnimateBar(Bar bar, float maximumDelta = -1f)
        {
            if (float.IsNaN(bar.Target)) return;
            var rect = bar.Fill.rectTransform;
            var anchor = rect.anchorMax;
            anchor.x = Mathf.MoveTowards(anchor.x, bar.Target, maximumDelta < 0 ? Time.unscaledDeltaTime * 4f : maximumDelta);
            rect.anchorMax = anchor;
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
