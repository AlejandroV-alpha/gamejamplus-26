using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameJam.Tablet
{
    public sealed class TabletGuideUI : MonoBehaviour
    {
        private RectTransform chassis, screen, workspace;
        private CanvasGroup overlay;
        private TabletRecipeCatalog catalog;
        private TabletUISkin skin;
        private TMP_FontAsset tmpFont;
        private TextMeshProUGUI osClockText;
        private readonly List<Button> planetTabButtons = new();
        private readonly List<TextMeshProUGUI> planetTabLabels = new();
        private readonly List<TabletRoundedPanel> planetTabPanels = new();
        private readonly List<Button> dockButtons = new();
        private readonly List<TextMeshProUGUI> dockLabels = new();
        private readonly List<TabletRoundedPanel> dockPanels = new();
        private float progress;
        private int planetIndex = 0;
        private string page = "Bitacora";

        // Android Sci-Fi Theme Palette:
        private static readonly Color CyanAccent = new(0.00f, 0.92f, 0.88f, 1f);     // Vibrant Android cyan
        private static readonly Color InkWhite = new(0.96f, 0.98f, 1.00f, 1f);       // Ultra-crisp reading white
        private static readonly Color MutedCyan = new(0.48f, 0.68f, 0.80f, 1f);      // Soft technical secondary
        private static readonly Color DimCyan = new(0.26f, 0.40f, 0.50f, 1f);        // Muted labels
        private static readonly Color CardBg = new(0.045f, 0.075f, 0.12f, 0.96f);    // Material dark card
        private static readonly Color InsetBg = new(0.02f, 0.035f, 0.055f, 0.95f);   // High-contrast sunken box
        private static readonly Color SlotBg = new(0.08f, 0.13f, 0.20f, 0.95f);     // Item slot tint

        // Physical Rugged Tablet Hardware Palette:
        private static readonly Color ChassisRim = new(0.18f, 0.24f, 0.32f, 1f);     // Outer titanium chamfer rim
        private static readonly Color ChassisMatte = new(0.065f, 0.085f, 0.115f, 1f);// Rugged dark matte alloy body
        private static readonly Color ChassisBezel = new(0.035f, 0.045f, 0.065f, 1f);// Inner screen bezel border
        private static readonly Color ArmorBumper = new(0.12f, 0.16f, 0.22f, 1f);   // Reinforced rubberized corner armor
        private static readonly Color HexBolt = new(0.26f, 0.34f, 0.46f, 1f);       // Dark titanium hardware bolts
        private static readonly Color GoldPin = new(0.85f, 0.70f, 0.25f, 1f);       // Gold magnetic dock pogo pins
        private static readonly Color HardwareStamp = new(0.28f, 0.38f, 0.50f, 1f); // Silk-screen technical markings

        private const float PPU = 0.5f;

        public bool TargetOpen { get; set; }
        public bool IsVisible => progress > 0;
        public float Progress => progress;
        public string CurrentPage => page;
        public string SelectedPlanet => Planet == null ? "" : Planet.name;
        private TabletRecipeCatalog.Planet Planet => catalog != null && catalog.planets != null && catalog.planets.Length > 0
            ? catalog.planets[Mathf.Clamp(planetIndex, 0, catalog.planets.Length - 1)] : null;

        public void Build(UnityAction close, TabletRecipeCatalog data = null, TabletUISkin appearance = null)
        {
            catalog = data;
            skin = appearance;
            tmpFont = (skin != null && skin.tmpFont != null) ? skin.tmpFont : null;
            planetIndex = GetMarsIndex();
            page = "Bitacora";

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            canvas.pixelPerfect = true;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Bottom-right equipment hint badge: "[TAB] TABLET"
            CreateEquipmentHint();

            // Fullscreen backdrop overlay root
            var root = new GameObject("Local Tablet Overlay", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            overlay = root.AddComponent<CanvasGroup>();
            overlay.alpha = 0;
            overlay.blocksRaycasts = false;

            // Ambient dark space shade behind tablet
            var worldShade = CreateSlicedImage(root.transform, "World shade", new Vector2(5000, 5000), null, new Color(0.01f, 0.015f, 0.03f, 0.80f));
            worldShade.anchoredPosition = Vector2.zero;

            // ================= 1. PHYSICAL TABLET HARDWARE CHASSIS (RUGGED FIELD DATAPAD) =================
            // Modern 16:10 rounded tablet chassis (1300 x 820)
            var chassisRimGo = CreateRoundedPanel(root.transform, "Tablet Titanium Rim", new Vector2(1300, 820), 36, ChassisRim);
            chassis = (RectTransform)chassisRimGo.transform;
            chassis.anchoredPosition = Vector2.zero;

            // Inner matte body casing (1292 x 812)
            var chassisBody = CreateRoundedPanel(chassis, "Tablet Matte Body", new Vector2(1292, 812), 33, ChassisMatte);
            chassisBody.anchoredPosition = Vector2.zero;

            // 1A. Four Reinforced Corner Armor Bumpers with Hex Screws (Field drop protection)
            CreateArmorCornerBumpers(chassisBody);

            // 1B. Left Edge: Weather-sealed Data Port Door & Core Expansion Bay
            CreateLeftDataPort(chassisBody);

            // 1C. Right Edge: Tactile Power Switch, Volume Rocker, Magnetic Stylus Groove
            CreateRightHardwareControls(chassisBody);

            // 1D. Top Bezel Hardware: Multispectral Camera, Atmospheric Intake Grille, Comms LED
            CreateTopSensorArray(chassisBody);

            // 1E. Bottom Bezel Hardware: Gold Pogo Dock Connector Pins, Model Markings, Android Gesture Bar
            CreateBottomDockHardware(chassisBody);

            // ================= 2. RECESSED ROUNDED GLASS DISPLAY SCREEN =================
            // Screen area (1210 x 730) with 20px rounded display corners
            var screenRim = CreateRoundedPanel(chassisBody, "Screen Recessed Rim", new Vector2(1210, 730), 22, ChassisBezel);
            screenRim.anchoredPosition = new Vector2(0, -2);

            screen = CreateRoundedPanel(screenRim, "Glass Display Area", new Vector2(1202, 722), 20, new Color(0.03f, 0.045f, 0.07f, 1f));
            screen.anchoredPosition = Vector2.zero;

            // Clip display contents to rounded screen edges
            screen.gameObject.AddComponent<RectMask2D>();

            // Expedition selfie wallpaper layer (with strong dark glass contrast tint)
            AddWallpaper();

            // ================= 3. ANDROID TABLET OS INTERFACE =================
            // Top Android Status Bar (Clock, Network, Planet Chips, Battery, Close)
            CreateAndroidTopStatusBar(close);

            // Central dynamic workspace for pages (Inicio, Tareas, Recetas, Minerales)
            workspace = new GameObject("Widget workspace", typeof(RectTransform)).GetComponent<RectTransform>();
            workspace.transform.SetParent(screen, false);
            workspace.anchorMin = new Vector2(0.5f, 0.5f);
            workspace.anchorMax = new Vector2(0.5f, 0.5f);
            workspace.sizeDelta = new Vector2(1170, 570);
            workspace.anchoredPosition = new Vector2(0, -18);

            // Bottom Android Taskbar / Dock (Inicio, Tareas, Recetas, Minerales)
            CreateAndroidTaskbarDock();

            RenderPage();
        }

        private void CreateEquipmentHint()
        {
            var hint = CreateRoundedPanel(transform, "Equipment hint", new Vector2(280, 42), 12, InsetBg);
            hint.anchorMin = hint.anchorMax = new Vector2(1, 0);
            hint.anchoredPosition = new Vector2(-155, 30);

            var outline = hint.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.35f);
            outline.effectDistance = new Vector2(1, -1);

            if (skin != null && skin.objectives != null)
            {
                CreateIcon(hint, skin.objectives, new Vector2(-110, 0), new Vector2(20, 20), CyanAccent);
            }

            var label = CreateLabel(hint, "01  TABLET EQUIPADA  [TAB]", 14, new Vector2(14, 0), new Vector2(230, 26), CyanAccent);
            label.alignment = TextAlignmentOptions.Center;
        }

        // 1A. Heavy-Duty Protective Corner Bumpers (Makes total sense on an astronaut field tablet)
        private void CreateArmorCornerBumpers(RectTransform parent)
        {
            Vector2[] corners = {
                new(-628, 388), new(628, 388),
                new(-628, -388), new(628, -388)
            };

            foreach (var pos in corners)
            {
                // Rubberized protective armor plate
                var plate = CreateRoundedPanel(parent, "CornerArmor", new Vector2(34, 34), 8, ArmorBumper);
                plate.anchoredPosition = pos;

                // Dark titanium hex fastener bolt
                var boltRim = CreateRoundedPanel(plate, "HexBolt", new Vector2(10, 10), 5, HexBolt);
                boltRim.anchoredPosition = Vector2.zero;
                var boltCenter = CreateRoundedPanel(boltRim, "Pin", new Vector2(4, 4), 2, new Color(0.08f, 0.12f, 0.16f));
                boltCenter.anchoredPosition = Vector2.zero;
            }
        }

        // 1B. Left Edge: Weather-sealed High-Speed Data Cartridge Bay
        private void CreateLeftDataPort(RectTransform parent)
        {
            // Recessed port housing on left bezel
            var bay = CreateRoundedPanel(parent, "DataBayHousing", new Vector2(10, 140), 4, new Color(0.03f, 0.045f, 0.07f));
            bay.anchoredPosition = new Vector2(-637, 20);

            // Sealed rubber protective cover
            var door = CreateRoundedPanel(bay, "SealFlap", new Vector2(6, 120), 3, ChassisRim);
            door.anchoredPosition = Vector2.zero;

            // Grip ridges on door
            for (int r = -2; r <= 2; r++)
            {
                var ridge = CreateRoundedPanel(door, "Ridge", new Vector2(4, 3), 1, new Color(0.08f, 0.12f, 0.16f));
                ridge.anchoredPosition = new Vector2(0, r * 16);
            }

            // Port indicator LED
            var led = CreateRoundedPanel(parent, "PortLED", new Vector2(4, 8), 2, new Color(0.00f, 0.92f, 0.88f, 0.70f));
            led.anchoredPosition = new Vector2(-637, -65);
        }

        // 1C. Right Edge: Sealed Power Switch, Tactile Volume Rocker, Magnetic Stylus Dock
        private void CreateRightHardwareControls(RectTransform parent)
        {
            // Power switch with subtle red accent line
            var pwrBtn = CreateRoundedPanel(parent, "Pwr_Button", new Vector2(6, 44), 3, ChassisRim);
            pwrBtn.anchoredPosition = new Vector2(648, 220);
            var pwrAccent = CreateRoundedPanel(pwrBtn, "Accent", new Vector2(2, 20), 1, new Color(0.9f, 0.3f, 0.3f, 0.9f));
            pwrAccent.anchoredPosition = Vector2.zero;

            // Volume rocker with center separator
            var volBtn = CreateRoundedPanel(parent, "Vol_Button", new Vector2(6, 80), 3, ChassisRim);
            volBtn.anchoredPosition = new Vector2(648, 140);
            var volDiv = CreateRoundedPanel(volBtn, "Divider", new Vector2(4, 2), 1, new Color(0.08f, 0.12f, 0.16f));
            volDiv.anchoredPosition = Vector2.zero;

            // Magnetic Stylus docking slot on the right bezel
            var stylusSlot = CreateRoundedPanel(parent, "StylusDock", new Vector2(8, 200), 4, new Color(0.03f, 0.045f, 0.07f));
            stylusSlot.anchoredPosition = new Vector2(637, -30);
            var stylusBody = CreateRoundedPanel(stylusSlot, "StylusPen", new Vector2(4, 180), 2, new Color(0.14f, 0.19f, 0.26f));
            stylusBody.anchoredPosition = Vector2.zero;
        }

        // 1D. Top Bezel: Multispectral Camera, Atmospheric Intake Grille, Comms Status LED
        private void CreateTopSensorArray(RectTransform parent)
        {
            var cluster = new GameObject("Sensor Array", typeof(RectTransform)).GetComponent<RectTransform>();
            cluster.transform.SetParent(parent, false);
            cluster.anchoredPosition = new Vector2(0, 390);
            cluster.sizeDelta = new Vector2(340, 20);

            // Atmospheric & radiation intake micro-grille
            var atmosGrille = CreateRoundedPanel(cluster, "AtmosSensorGrille", new Vector2(56, 6), 3, new Color(0.02f, 0.035f, 0.05f));
            atmosGrille.anchoredPosition = new Vector2(-75, 0);
            for (int g = -3; g <= 3; g++)
            {
                var slit = CreateRoundedPanel(atmosGrille, "Slit", new Vector2(2, 4), 1, new Color(0.12f, 0.16f, 0.22f));
                slit.anchoredPosition = new Vector2(g * 7, 0);
            }

            // Primary visual spectrum camera
            var lensRim = CreateRoundedPanel(cluster, "VisualLensRim", new Vector2(14, 14), 7, new Color(0.02f, 0.03f, 0.05f));
            lensRim.anchoredPosition = new Vector2(-22, 0);
            var lensIris = CreateRoundedPanel(lensRim, "LensIris", new Vector2(6, 6), 3, new Color(0.10f, 0.40f, 0.70f));
            lensIris.anchoredPosition = Vector2.zero;

            // Infrared / multispectral scanner dot
            var irLens = CreateRoundedPanel(cluster, "IRSensor", new Vector2(8, 8), 4, new Color(0.25f, 0.08f, 0.12f));
            irLens.anchoredPosition = new Vector2(-2, 0);

            // Comms micro speaker grille
            var speaker = CreateRoundedPanel(cluster, "SpeakerGrille", new Vector2(40, 4), 2, new Color(0.02f, 0.03f, 0.05f));
            speaker.anchoredPosition = new Vector2(38, 0);

            // Operational status LED with technical label
            var led = CreateRoundedPanel(cluster, "StatusLED", new Vector2(5, 5), 2, new Color(0.00f, 0.96f, 0.74f, 0.95f));
            led.anchoredPosition = new Vector2(80, 0);
        }

        // 1E. Bottom Bezel: Gold Pogo Dock Connector Pins, Authentic Aerospace Markings, Android Gesture Bar
        private void CreateBottomDockHardware(RectTransform parent)
        {
            // Left in-universe specification stamping
            var leftStamp = CreateLabel(parent, "ORBIT AEROSPACE // EXP-DATAPAD MK-VII", 11,
                new Vector2(-360, -394), new Vector2(380, 16), HardwareStamp);
            leftStamp.alignment = TextAlignmentOptions.Left;

            // Right in-universe serial & certification stamping
            var rightStamp = CreateLabel(parent, "S/N: 8492-EXP-07  -  HAZMAT CLASS-1", 11,
                new Vector2(360, -394), new Vector2(380, 16), HardwareStamp);
            rightStamp.alignment = TextAlignmentOptions.Right;

            // Center Magnetic Pogo Dock Connector (Where the tablet docks into the spaceship console!)
            var dockSlot = CreateRoundedPanel(parent, "PogoDockConnector", new Vector2(110, 10), 4, new Color(0.02f, 0.035f, 0.05f));
            dockSlot.anchoredPosition = new Vector2(0, -394);

            // 5 Gold Contact Pins
            for (int p = -2; p <= 2; p++)
            {
                var pin = CreateRoundedPanel(dockSlot, $"Pin_{p}", new Vector2(5, 5), 2.5f, GoldPin);
                pin.anchoredPosition = new Vector2(p * 14, 0);
            }

            // Modern Android navigation gesture pill (━) sitting right above the dock port
            var gesturePill = CreateRoundedPanel(parent, "GesturePill", new Vector2(120, 3), 1.5f, new Color(0.00f, 0.92f, 0.88f, 0.40f));
            gesturePill.anchoredPosition = new Vector2(0, -378);
        }

        private void AddWallpaper()
        {
            if (skin == null || skin.wallpaper == null) return;

            var viewport = new GameObject("Wallpaper Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(screen, false);
            var vRect = (RectTransform)viewport.transform;
            vRect.anchorMin = Vector2.zero;
            vRect.anchorMax = Vector2.one;
            vRect.offsetMin = vRect.offsetMax = Vector2.zero;

            var pic = new GameObject("Wallpaper Image", typeof(RectTransform), typeof(Image));
            pic.transform.SetParent(viewport.transform, false);
            var img = pic.GetComponent<Image>();
            img.sprite = skin.wallpaper;
            img.raycastTarget = false;

            var fit = pic.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = skin.wallpaper.rect.width / skin.wallpaper.rect.height;

            // Deep dark glass tint (0.74) to balance rich sci-fi artwork with perfect UI text readability
            var tintGo = new GameObject("Wallpaper Tint", typeof(RectTransform), typeof(Image));
            tintGo.transform.SetParent(viewport.transform, false);
            var tRect = (RectTransform)tintGo.transform;
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = tRect.offsetMax = Vector2.zero;
            var tintImg = tintGo.GetComponent<Image>();
            tintImg.color = new Color(0.015f, 0.022f, 0.038f, 0.74f);
            tintImg.raycastTarget = false;
        }

        private int GetMarsIndex()
        {
            if (catalog == null || catalog.planets == null) return 0;
            for (int i = 0; i < catalog.planets.Length; i++)
            {
                if (catalog.planets[i].name != null &&
                    catalog.planets[i].name.Equals("Marte", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return 0;
        }

        private void CreateAndroidTopStatusBar(UnityAction close)
        {
            // Sleek Android Material Top Status Bar
            var bar = CreateRoundedPanel(screen, "Android Status Bar", new Vector2(1170, 38), 14, InsetBg);
            bar.anchoredPosition = new Vector2(0, 325);

            var barOutline = bar.gameObject.AddComponent<Outline>();
            barOutline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.25f);
            barOutline.effectDistance = new Vector2(1, -1);

            // Left: Clock & Network in-universe telemetry
            var clockLabel = CreateLabel(bar, "14:38", 15, new Vector2(-540, 0), new Vector2(70, 24), InkWhite);
            clockLabel.alignment = TextAlignmentOptions.Left;

            var osLabel = CreateLabel(bar, "ORBIT OS 14 // SAT-LINK: NOMINAL", 12, new Vector2(-360, 0), new Vector2(270, 24), MutedCyan);
            osLabel.alignment = TextAlignmentOptions.Left;

            // Center: Active Sector Pill (Only MARTE shown - others hidden)
            var sectorPill = CreateRoundedPanel(bar, "SectorPill", new Vector2(230, 26), 13, new Color(0.00f, 0.35f, 0.45f, 0.95f));
            sectorPill.anchoredPosition = new Vector2(-15, 0);

            var secText = CreateLabel(sectorPill, "SECTOR ACTIVO: MARTE", 12, Vector2.zero, new Vector2(210, 20), CyanAccent);
            secText.alignment = TextAlignmentOptions.Center;

            // Right: System Indicators (Satellite Sync, Battery Pill, Close Chip)
            var syncLabel = CreateLabel(bar, "[SYNC]", 11, new Vector2(370, 0), new Vector2(70, 20), CyanAccent);
            syncLabel.alignment = TextAlignmentOptions.Right;

            // Battery Indicator Pill
            var batPill = CreateRoundedPanel(bar, "BatteryPill", new Vector2(85, 22), 8, new Color(0.05f, 0.10f, 0.16f, 0.95f));
            batPill.anchoredPosition = new Vector2(460, 0);
            var batFill = CreateRoundedPanel(batPill, "BatteryFill", new Vector2(75, 14), 5, new Color(0.00f, 0.92f, 0.88f, 0.35f));
            batFill.anchoredPosition = Vector2.zero;
            var batText = CreateLabel(batPill, "98%", 11, Vector2.zero, new Vector2(80, 20), InkWhite);
            batText.alignment = TextAlignmentOptions.Center;

            // Close Chip Button (X)
            var closeBtn = CreateCircularChipButton(bar, "X", new Vector2(545, 0), new Vector2(28, 26), () => close());
        }

        private void CreateAndroidTaskbarDock()
        {
            // Floating Android 14 Tablet Taskbar Pill (510 x 44)
            var dock = CreateRoundedPanel(screen, "Android Taskbar", new Vector2(510, 44), 22, new Color(0.05f, 0.08f, 0.14f, 0.95f));
            dock.anchoredPosition = new Vector2(0, -325);

            var dockOutline = dock.gameObject.AddComponent<Outline>();
            dockOutline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.35f);
            dockOutline.effectDistance = new Vector2(1, -1);

            dockButtons.Clear();
            dockLabels.Clear();
            dockPanels.Clear();

            var sections = new[] { "Bitacora", "Checklist", "Recetas" };
            var displayNames = new[] { "BITACORA", "CHECKLIST", "RECETAS" };
            float startX = -170f;
            float spacing = 170f;

            for (int i = 0; i < sections.Length; i++)
            {
                string sec = sections[i];
                var (btn, lbl, pnl) = CreateDockItem(dock, displayNames[i], new Vector2(startX + i * spacing, 0), new Vector2(150, 32),
                    () => ShowPage(sec));
                dockButtons.Add(btn);
                dockLabels.Add(lbl);
                dockPanels.Add(pnl);
            }
        }

        public void ShowBitacora() => ShowPage("Bitacora");
        public void ShowChecklist() => ShowPage("Checklist");
        public void ShowRecipes() => ShowPage("Recetas");
        public void ShowTasks() => ShowPage("Checklist");
        public void ShowMinerals() => ShowPage("Recetas");

        public void SelectPlanet(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.planets.Length) return;
            planetIndex = index;
            RenderPage();
        }

        public void ShowPage(string section)
        {
            if (section == "Inicio") section = "Bitacora";
            if (section == "Tareas") section = "Checklist";
            if (section == "Minerales") section = "Recetas";
            page = section;
            RenderPage();
        }

        private void RenderPage()
        {
            // Clear workspace
            for (int i = workspace.childCount - 1; i >= 0; i--)
            {
                var child = workspace.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            if (Planet == null)
            {
                var emptyMsg = CreateLabel(workspace, "NO HAY ARCHIVO PLANETARIO DISPONIBLE", 20, Vector2.zero, new Vector2(800, 40), InkWhite);
                emptyMsg.alignment = TextAlignmentOptions.Center;
                return;
            }

            // Update bottom Android taskbar items (Bitacora, Checklist, Recetas)
            var sections = new[] { "Bitacora", "Checklist", "Recetas" };
            for (int i = 0; i < dockButtons.Count; i++)
            {
                bool active = (sections[i] == page);
                dockPanels[i].color = active ? new Color(0.00f, 0.45f, 0.50f, 0.90f) : Color.clear;
                dockLabels[i].color = active ? CyanAccent : InkWhite;
            }

            if (page == "Bitacora") RenderBitacora();
            else if (page == "Checklist") RenderChecklist();
            else if (page == "Recetas") RenderRecipes();
            else RenderBitacora();
        }

        // ================= PAGE 1: BITACORA (EXPEDITION LOGBOOK & OVERVIEW - MARS FOCUSED) =================
        private void RenderBitacora()
        {
            // ----------------- LEFT HERO COLUMN: BITACORA DE EXPEDICION (700 x 470) -----------------
            var heroCard = CreateRoundedPanel(workspace, "Hero_Bitacora", new Vector2(700, 470), 16, CardBg);
            heroCard.anchoredPosition = new Vector2(-225, 0);

            var heroOutline = heroCard.gameObject.AddComponent<Outline>();
            heroOutline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.30f);
            heroOutline.effectDistance = new Vector2(1, -1);

            // Left vertical accent strip
            var accentStrip = CreateRoundedPanel(heroCard, "AccentStrip", new Vector2(4, 450), 2, CyanAccent);
            accentStrip.anchoredPosition = new Vector2(-344, 0);

            // Top Header Line
            CreateIcon(heroCard, skin != null ? skin.ship : null, new Vector2(-305, 205), new Vector2(26, 26), CyanAccent);
            var mTitle = CreateLabel(heroCard, $"BITACORA DE EXPEDICION  -  SECTOR {Clean(Planet.name).ToUpperInvariant()}", 17,
                new Vector2(-15, 205), new Vector2(530, 26), CyanAccent);
            mTitle.alignment = TextAlignmentOptions.Left;

            // Android Material Pill Badge: [ PRIORIDAD 1 ]
            var prioBadge = CreateRoundedPanel(heroCard, "PrioBadge", new Vector2(110, 26), 13, new Color(0.35f, 0.10f, 0.10f, 0.90f));
            prioBadge.anchoredPosition = new Vector2(280, 205);
            var prioDot = CreateRoundedPanel(prioBadge, "Dot", new Vector2(6, 6), 3, new Color(1f, 0.45f, 0.35f));
            prioDot.anchoredPosition = new Vector2(-38, 0);
            var prioText = CreateLabel(prioBadge, "PRIORIDAD 1", 11, new Vector2(8, 0), new Vector2(85, 20), new Color(1f, 0.85f, 0.80f));
            prioText.alignment = TextAlignmentOptions.Center;

            // Objective Box
            var objBox = CreateRoundedPanel(heroCard, "ObjectiveBox", new Vector2(660, 85), 12, InsetBg);
            objBox.anchoredPosition = new Vector2(0, 135);
            var objSub = CreateLabel(objBox, "OBJETIVO PRINCIPAL DE MISION:", 11, new Vector2(0, 26), new Vector2(620, 18), DimCyan);
            objSub.alignment = TextAlignmentOptions.Left;
            var objMain = CreateLabel(objBox, Clean(Planet.objective), 16, new Vector2(0, -12), new Vector2(620, 45), InkWhite);
            objMain.alignment = TextAlignmentOptions.Left;

            // Mission Log Entries Box (Field notes & telemetry log)
            var logBox = CreateRoundedPanel(heroCard, "LogBox", new Vector2(660, 175), 12, InsetBg);
            logBox.anchoredPosition = new Vector2(0, -12);

            var logHeader = CreateLabel(logBox, "REGISTRO TELEMETRICO Y ENTRADAS DE CAMPO:", 12, new Vector2(0, 68), new Vector2(620, 20), MutedCyan);
            logHeader.alignment = TextAlignmentOptions.Left;

            string[] logEntries = {
                "[SOL 14 // 08:30] Aterrizaje forzoso en Marte tras tormenta de arena.",
                "[SOL 14 // 10:15] Danos criticos: Propulsor y soporte vital averiados.",
                "[SOL 14 // 12:45] Detectados depositos de metal y mineral rojo en el sector.",
                "[SOL 14 // ACTUAL] Protocolo activo: En espera de piezas de recambio."
            };

            for (int e = 0; e < logEntries.Length; e++)
            {
                bool isCurrent = (e == logEntries.Length - 1);
                Color entryColor = isCurrent ? CyanAccent : (e == 0 ? InkWhite : MutedCyan);
                var entryLabel = CreateLabel(logBox, logEntries[e], 12, new Vector2(0, 36 - e * 26), new Vector2(620, 22), entryColor);
                entryLabel.enableWordWrapping = false;
                entryLabel.alignment = TextAlignmentOptions.Left;
            }

            // Primary Call to Action: Go to Checklist
            var (heroBtn, _) = CreatePillActionButton(heroCard, "ABRIR CHECKLIST DE REPARACION  >",
                new Vector2(0, -185), new Vector2(660, 42), 14, () => ShowChecklist());

            // ----------------- RIGHT COLUMN: COMPLEMENTARY WIDGETS (445 x 470) -----------------

            // Widget 1: Próxima Reparación Crítica (Top-Right)
            var widgetRepair = CreateRoundedPanel(workspace, "Widget_Repair", new Vector2(445, 225), 16, CardBg);
            widgetRepair.anchoredPosition = new Vector2(360, 122);

            var wrOutline = widgetRepair.gameObject.AddComponent<Outline>();
            wrOutline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.20f);
            wrOutline.effectDistance = new Vector2(1, -1);

            CreateIcon(widgetRepair, skin != null ? skin.repair : null, new Vector2(-185, 82), new Vector2(22, 22), CyanAccent);
            var wrTitle = CreateLabel(widgetRepair, "PROXIMA FABRICACION", 14, new Vector2(10, 82), new Vector2(350, 22), CyanAccent);
            wrTitle.alignment = TextAlignmentOptions.Left;

            var wrInset = CreateRoundedPanel(widgetRepair, "Repair_Inset", new Vector2(415, 85), 10, InsetBg);
            wrInset.anchoredPosition = new Vector2(0, 16);

            if (Planet.recipes != null && Planet.recipes.Length > 0)
            {
                var r0 = Planet.recipes[0];
                var cLabel = CreateLabel(wrInset, Clean(r0.component).ToUpperInvariant(), 15, new Vector2(0, 20), new Vector2(390, 24), InkWhite);
                cLabel.alignment = TextAlignmentOptions.Left;

                var ingLabel = CreateLabel(wrInset, Ingredients(r0), 13, new Vector2(0, -18), new Vector2(390, 36), CyanAccent);
                ingLabel.alignment = TextAlignmentOptions.Left;
            }

            var (wrBtn, _) = CreatePillActionButton(widgetRepair, "VER TODAS LAS RECETAS  >", new Vector2(0, -68), new Vector2(415, 34), 12, () => ShowRecipes());

            // Widget 2: Telemetría Ambiental y Estado de Supervivencia (Bottom-Right)
            var widgetTelem = CreateRoundedPanel(workspace, "Widget_Telemetry", new Vector2(445, 225), 16, CardBg);
            widgetTelem.anchoredPosition = new Vector2(360, -122);

            var wtOutline = widgetTelem.gameObject.AddComponent<Outline>();
            wtOutline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.20f);
            wtOutline.effectDistance = new Vector2(1, -1);

            CreateIcon(widgetTelem, skin != null ? skin.ship : null, new Vector2(-185, 82), new Vector2(22, 22), CyanAccent);
            var wtTitle = CreateLabel(widgetTelem, "TELEMETRIA DE SUPERFICIE", 14, new Vector2(10, 82), new Vector2(350, 22), CyanAccent);
            wtTitle.alignment = TextAlignmentOptions.Left;

            var wtInset = CreateRoundedPanel(widgetTelem, "Telem_Inset", new Vector2(415, 85), 10, InsetBg);
            wtInset.anchoredPosition = new Vector2(0, 16);

            var tLine1 = CreateLabel(wtInset, "ATMOSFERA: 95% CO2  //  PRESION: 0.6 kPa", 12, new Vector2(0, 22), new Vector2(390, 20), InkWhite);
            tLine1.alignment = TextAlignmentOptions.Center;

            var tLine2 = CreateLabel(wtInset, "GRAVEDAD: 0.38G  //  TEMPERATURA: -63 C", 12, new Vector2(0, -2), new Vector2(390, 20), CyanAccent);
            tLine2.alignment = TextAlignmentOptions.Center;

            var tLine3 = CreateLabel(wtInset, "RADIACION: 0.24 mSv/h  (TRAJE ESTABLE)", 11, new Vector2(0, -26), new Vector2(390, 18), DimCyan);
            tLine3.alignment = TextAlignmentOptions.Center;

            var (wtBtn, _) = CreatePillActionButton(widgetTelem, "IR AL CHECKLIST OPERATIVO  >", new Vector2(0, -68), new Vector2(415, 34), 12, () => ShowChecklist());
        }

        // ================= PAGE 2: CHECKLIST (OPERATIONAL STEPS FOR MARS) =================
        private void RenderChecklist()
        {
            var header = CreateLabel(workspace, $"CHECKLIST DE PROTOCOLO  -  SECTOR {Clean(Planet.name).ToUpperInvariant()}", 18,
                new Vector2(0, 225), new Vector2(1150, 28), CyanAccent);
            header.alignment = TextAlignmentOptions.Left;

            var steps = new[] {
                $"Localizar zona de impacto y diagnosticar sistemas de {Clean(Planet.name)}",
                "Recolectar suministros en Marte: Metal, Mineral rojo y Circuitos",
                "Reparar componentes en la estacion de ensamblaje (Propulsor, Filtros, Radar)",
                "Completar la secuencia de ignicion y retorno seguro a la orbita"
            };

            for (int i = 0; i < steps.Length; i++)
            {
                var row = CreateRoundedPanel(workspace, $"Checklist_Row_{i}", new Vector2(1150, 88), 14, CardBg);
                row.anchoredPosition = new Vector2(0, 140 - i * 105);

                var rowOutline = row.gameObject.AddComponent<Outline>();
                rowOutline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.20f);
                rowOutline.effectDistance = new Vector2(1, -1);

                // Slot with check icon
                var slot = CreateRoundedPanel(row, "Slot", new Vector2(42, 42), 10, SlotBg);
                slot.anchoredPosition = new Vector2(-525, 0);
                if (skin != null && skin.checkIcon != null)
                {
                    CreateIcon(slot, skin.checkIcon, Vector2.zero, new Vector2(24, 24), i == 0 ? CyanAccent : DimCyan);
                }

                // Step index
                var numLabel = CreateLabel(row, (i + 1).ToString("00"), 18, new Vector2(-470, 0), new Vector2(45, 36), i == 1 ? CyanAccent : InkWhite);
                numLabel.alignment = TextAlignmentOptions.Center;

                // Step description
                var stepDesc = CreateLabel(row, steps[i], 15, new Vector2(50, 0), new Vector2(850, 50), i == 1 ? InkWhite : (i == 0 ? MutedCyan : DimCyan));
                stepDesc.alignment = TextAlignmentOptions.Left;

                // Status tag inset (Material pill)
                var tag = CreateRoundedPanel(row, "Tag", new Vector2(120, 30), 10, InsetBg);
                tag.anchoredPosition = new Vector2(495, 0);
                var tagLabel = CreateLabel(tag, i == 1 ? "[EN CURSO]" : (i == 0 ? "[COMPLETO]" : "[PENDIENTE]"), 11,
                    Vector2.zero, new Vector2(115, 22), i == 1 ? CyanAccent : (i == 0 ? InkWhite : DimCyan));
                tagLabel.alignment = TextAlignmentOptions.Center;
            }

            var note = CreateLabel(workspace, "SEGUIMIENTO OPERATIVO // ACTUALIZACION TELEMETRICA EN TIEMPO REAL", 11,
                new Vector2(0, -245), new Vector2(1150, 20), DimCyan);
            note.alignment = TextAlignmentOptions.Center;
        }

        // ================= PAGE 3: RECETAS (ENGINEERING BLUEPRINTS - MARS) =================
        private void RenderRecipes()
        {
            var header = CreateLabel(workspace, $"RECETAS DE FABRICACION  -  SECTOR {Clean(Planet.name).ToUpperInvariant()}", 18,
                new Vector2(0, 225), new Vector2(1150, 28), CyanAccent);
            header.alignment = TextAlignmentOptions.Left;

            for (int i = 0; i < Planet.recipes.Length; i++)
            {
                var recipe = Planet.recipes[i];
                var row = CreateRoundedPanel(workspace, $"Recipe_{i}", new Vector2(1150, 95), 14, CardBg);
                row.anchoredPosition = new Vector2(0, 135 - i * 115);

                var rowOutline = row.gameObject.AddComponent<Outline>();
                rowOutline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.20f);
                rowOutline.effectDistance = new Vector2(1, -1);

                // Slot with repair gear icon
                var slot = CreateRoundedPanel(row, "Slot", new Vector2(46, 46), 10, SlotBg);
                slot.anchoredPosition = new Vector2(-520, 0);
                if (skin != null && skin.repair != null)
                {
                    CreateIcon(slot, skin.repair, Vector2.zero, new Vector2(26, 26), CyanAccent);
                }

                // Component index & name
                var nameLabel = CreateLabel(row, $"[0{i + 1}]  {Clean(recipe.component).ToUpperInvariant()}", 16,
                    new Vector2(-260, 0), new Vector2(430, 38), InkWhite);
                nameLabel.alignment = TextAlignmentOptions.Left;

                // Inset container with materials
                var ingInset = CreateRoundedPanel(row, "Ing_Inset", new Vector2(500, 55), 10, InsetBg);
                ingInset.anchoredPosition = new Vector2(230, 0);
                var ingLabel = CreateLabel(ingInset, Ingredients(recipe), 14, Vector2.zero, new Vector2(480, 44), CyanAccent);
                ingLabel.alignment = TextAlignmentOptions.Center;

                // Status tag
                var tag = CreateRoundedPanel(row, "Tag", new Vector2(110, 30), 10, InsetBg);
                tag.anchoredPosition = new Vector2(495, 0);
                var tagLabel = CreateLabel(tag, i < 2 ? "[CRITICO]" : "[REQUERIDO]", 11, Vector2.zero, new Vector2(105, 22),
                    i < 2 ? new Color(1f, 0.45f, 0.35f) : CyanAccent);
                tagLabel.alignment = TextAlignmentOptions.Center;
            }

            var note = CreateLabel(workspace, "LOS COMPONENTES SE FABRICAN CON SUMINISTROS EN LA MESA DE CRAFTEO", 11,
                new Vector2(0, -245), new Vector2(1150, 20), DimCyan);
            note.alignment = TextAlignmentOptions.Center;
        }

        // ================= UI HELPER BUILDERS (ANDROID MATERIAL & UGUI BEST PRACTICES) =================

        private RectTransform CreateRoundedPanel(Transform parent, string name, Vector2 size, float radius, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TabletRoundedPanel));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            var panel = go.GetComponent<TabletRoundedPanel>();
            panel.radius = radius;
            panel.color = color;
            panel.raycastTarget = false;
            return rect;
        }

        private RectTransform CreateSlicedImage(Transform parent, string name, Vector2 size, Sprite sprite, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = tint;
            img.pixelsPerUnitMultiplier = PPU;
            img.raycastTarget = false;
            return rect;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float fontSize, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmpFont != null) tmp.font = tmpFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.raycastTarget = false; // Skill rule: disable raycastTarget on text
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        private Image CreateIcon(Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color tint)
        {
            if (sprite == null) return null;
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private (Button button, TextMeshProUGUI label) CreatePillActionButton(Transform parent, string text, Vector2 position, Vector2 size, float radius, UnityAction action)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(TabletRoundedPanel), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var pnl = go.GetComponent<TabletRoundedPanel>();
            pnl.radius = radius;
            pnl.color = new Color(0.00f, 0.35f, 0.45f, 0.95f);
            pnl.raycastTarget = true;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = CyanAccent;
            outline.effectDistance = new Vector2(1, -1);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = pnl;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.35f);
            colors.pressedColor = new Color(0.85f, 0.90f, 1.0f);
            btn.colors = colors;
            btn.onClick.AddListener(action);

            var lbl = CreateLabel(rect, text, 14, Vector2.zero, size - new Vector2(16, 4), InkWhite);
            lbl.alignment = TextAlignmentOptions.Center;
            return (btn, lbl);
        }

        private (Button button, TextMeshProUGUI label, TabletRoundedPanel panel) CreatePillTab(Transform parent, string text, Vector2 position, Vector2 size, UnityAction action)
        {
            var go = new GameObject("Tab_" + text, typeof(RectTransform), typeof(TabletRoundedPanel), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var pnl = go.GetComponent<TabletRoundedPanel>();
            pnl.radius = 12;
            pnl.color = new Color(0.03f, 0.05f, 0.08f, 0.80f);
            pnl.raycastTarget = true;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.35f);
            outline.effectDistance = new Vector2(1, -1);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = pnl;
            btn.onClick.AddListener(action);

            var lbl = CreateLabel(rect, text, 13, Vector2.zero, size - new Vector2(8, 4), MutedCyan);
            lbl.alignment = TextAlignmentOptions.Center;
            return (btn, lbl, pnl);
        }

        private (Button button, TextMeshProUGUI label, TabletRoundedPanel panel) CreateDockItem(Transform parent, string text, Vector2 position, Vector2 size, UnityAction action)
        {
            var go = new GameObject("Dock_" + text, typeof(RectTransform), typeof(TabletRoundedPanel), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var pnl = go.GetComponent<TabletRoundedPanel>();
            pnl.radius = 14;
            pnl.color = Color.clear;
            pnl.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = pnl;
            btn.onClick.AddListener(action);

            var lbl = CreateLabel(rect, text, 14, Vector2.zero, size - new Vector2(10, 4), InkWhite);
            lbl.alignment = TextAlignmentOptions.Center;
            return (btn, lbl, pnl);
        }

        private Button CreateCircularChipButton(Transform parent, string text, Vector2 position, Vector2 size, UnityAction action)
        {
            var go = new GameObject("CloseChip", typeof(RectTransform), typeof(TabletRoundedPanel), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var pnl = go.GetComponent<TabletRoundedPanel>();
            pnl.radius = size.y * 0.5f; // Pure circular pill
            pnl.color = new Color(0.35f, 0.10f, 0.12f, 0.85f);
            pnl.raycastTarget = true;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.40f, 0.45f, 0.50f);
            outline.effectDistance = new Vector2(1, -1);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = pnl;
            btn.onClick.AddListener(action);

            var lbl = CreateLabel(rect, text, 14, Vector2.zero, size, InkWhite);
            lbl.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        // ================= HELPERS & UPDATES =================

        private int CountRecipes(string mineral)
        {
            int count = 0;
            if (Planet == null || Planet.recipes == null) return count;
            foreach (var recipe in Planet.recipes)
                foreach (var ingredient in recipe.ingredients)
                    if (ingredient.mineral == mineral) { count++; break; }
            return count;
        }

        private static string Ingredients(TabletRecipeCatalog.Recipe recipe)
        {
            var result = new StringBuilder();
            foreach (var ingredient in recipe.ingredients)
            {
                if (result.Length > 0) result.Append("   +   ");
                result.Append(ingredient.amount).Append(" x ").Append(Clean(ingredient.mineral).ToUpperInvariant());
            }
            return result.ToString();
        }

        private static string Clean(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input
                .Replace('á', 'a').Replace('Á', 'A')
                .Replace('é', 'e').Replace('É', 'E')
                .Replace('í', 'i').Replace('Í', 'I')
                .Replace('ó', 'o').Replace('Ó', 'O')
                .Replace('ú', 'u').Replace('Ú', 'U')
                .Replace('ñ', 'n').Replace('Ñ', 'N')
                .Replace('·', '-')
                .Replace('→', '>')
                .Replace('➔', '>')
                .Replace('×', 'x');
        }

        private void Update()
        {
            progress = Mathf.MoveTowards(progress, TargetOpen ? 1 : 0, Time.unscaledDeltaTime / 0.28f);
            float eased = progress * progress * (3 - 2 * progress); // Smoothstep easing
            overlay.alpha = eased;
            overlay.blocksRaycasts = progress > 0;
            overlay.interactable = TargetOpen && progress > 0.95f;

            // Physical handheld tablet rise and slight angle animation
            chassis.anchoredPosition = new Vector2(0, Mathf.Lerp(-960, 0, eased));
            chassis.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, eased);
            chassis.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-3.5f, 0, eased));
        }
    }
}
