using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GameJam.Tablet
{
    public sealed partial class TabletGuideUI : MonoBehaviour
    {
        private RectTransform chassis, screen, workspace;
        private CanvasGroup overlay;
        private TabletRecipeCatalog catalog;
        private TabletUISkin skin;
        private TMP_FontAsset tmpFont;
        private TextMeshProUGUI osClockText;
        private float progress;
        private int planetIndex = 0;
        private string page = "Inicio";
        private readonly Stack<string> pageHistory = new();
        private TextMeshProUGUI sectorLabel;
        private Button backButton;
        private CanvasGroup pageGroup;
        private float pageFade;
        private string clockMinute;

        // Tablet colors: warm accent and dark surfaces.
        private static readonly Color CyanAccent = new(1f, 0.65f, 0.35f, 1f);     // Vibrant warm accent
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
            EnsureInputEventSystem();
            catalog = data;
            skin = appearance;
            tmpFont = (skin != null && skin.tmpFont != null) ? skin.tmpFont : TMP_Settings.defaultFontAsset;
            planetIndex = GetMarsIndex();
            page = "Inicio";

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            canvas.pixelPerfect = true;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

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
            // Top Android Status Bar (Clock, Network, Sector, Close)
            CreateAndroidTopStatusBar(close);

            // Central dynamic workspace for pages (Inicio, Tareas, Recetas, Minerales, Planetas)
            workspace = new GameObject("Widget workspace", typeof(RectTransform)).GetComponent<RectTransform>();
            workspace.transform.SetParent(screen, false);
            workspace.anchorMin = new Vector2(0.5f, 0.5f);
            workspace.anchorMax = new Vector2(0.5f, 0.5f);
            workspace.sizeDelta = new Vector2(1170, 570);
            workspace.anchoredPosition = new Vector2(0, -18);

            // Bottom Android Taskbar / Dock (Atras, Inicio, Aplicaciones)
            CreateAndroidTaskbarDock();

            pageGroup = workspace.gameObject.AddComponent<CanvasGroup>();
            RenderPage();
            BuildMissionFeedback();
        }

        private void EnsureInputEventSystem()
        {
            // The lobby has no scene UI, but this runtime canvas still needs pointer routing.
            var events = EventSystem.current;
            if (events == null || !events.isActiveAndEnabled)
                events = FindFirstObjectByType<EventSystem>();
            if (events == null)
            {
                var host = new GameObject("Tablet EventSystem");
                host.transform.SetParent(transform, false);
                events = host.AddComponent<EventSystem>();
            }
            if (events.GetComponent<BaseInputModule>() == null)
            {
                var module = events.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        private TextMeshProUGUI statusFieldGuideLabel;
        private TextMeshProUGUI statusArchiveLabel;

        private void OnEnable()
        {
            GameJam.Localization.GameLocalization.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            GameJam.Localization.GameLocalization.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged()
        {
            if (statusFieldGuideLabel != null)
                statusFieldGuideLabel.text = Clean(GameJam.Localization.GameLocalization.Get("tablet.status_bar"));
            if (statusArchiveLabel != null)
                statusArchiveLabel.text = Clean(GameJam.Localization.GameLocalization.Get("tablet.local_archive"));
            if (workspace != null)
                RenderPage();
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
                var plate = CreateRoundedPanel(parent, "CornerArmor", new Vector2(34, 34), 8, ArmorBumper);
                plate.anchoredPosition = pos;

                var boltRim = CreateRoundedPanel(plate, "HexBolt", new Vector2(10, 10), 5, HexBolt);
                boltRim.anchoredPosition = Vector2.zero;
                var boltCenter = CreateRoundedPanel(boltRim, "Pin", new Vector2(4, 4), 2, new Color(0.08f, 0.12f, 0.16f));
                boltCenter.anchoredPosition = Vector2.zero;
            }
        }

        // 1B. Left Edge: Weather-sealed High-Speed Data Cartridge Bay
        private void CreateLeftDataPort(RectTransform parent)
        {
            var bay = CreateRoundedPanel(parent, "DataBayHousing", new Vector2(10, 140), 4, new Color(0.03f, 0.045f, 0.07f));
            bay.anchoredPosition = new Vector2(-637, 20);

            var door = CreateRoundedPanel(bay, "SealFlap", new Vector2(6, 120), 3, ChassisRim);
            door.anchoredPosition = Vector2.zero;

            for (int r = -2; r <= 2; r++)
            {
                var ridge = CreateRoundedPanel(door, "Ridge", new Vector2(4, 3), 1, new Color(0.08f, 0.12f, 0.16f));
                ridge.anchoredPosition = new Vector2(0, r * 16);
            }

            var led = CreateRoundedPanel(parent, "PortLED", new Vector2(4, 8), 2, new Color(0.00f, 0.92f, 0.88f, 0.70f));
            led.anchoredPosition = new Vector2(-637, -65);
        }

        // 1C. Right Edge: Sealed Power Switch, Tactile Volume Rocker, Magnetic Stylus Dock
        private void CreateRightHardwareControls(RectTransform parent)
        {
            var pwrBtn = CreateRoundedPanel(parent, "Pwr_Button", new Vector2(6, 44), 3, ChassisRim);
            pwrBtn.anchoredPosition = new Vector2(648, 220);
            var pwrAccent = CreateRoundedPanel(pwrBtn, "Accent", new Vector2(2, 20), 1, new Color(0.9f, 0.3f, 0.3f, 0.9f));
            pwrAccent.anchoredPosition = Vector2.zero;

            var volBtn = CreateRoundedPanel(parent, "Vol_Button", new Vector2(6, 80), 3, ChassisRim);
            volBtn.anchoredPosition = new Vector2(648, 140);
            var volDiv = CreateRoundedPanel(volBtn, "Divider", new Vector2(4, 2), 1, new Color(0.08f, 0.12f, 0.16f));
            volDiv.anchoredPosition = Vector2.zero;

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

            var atmosGrille = CreateRoundedPanel(cluster, "AtmosSensorGrille", new Vector2(56, 6), 3, new Color(0.02f, 0.035f, 0.05f));
            atmosGrille.anchoredPosition = new Vector2(-75, 0);
            for (int g = -3; g <= 3; g++)
            {
                var slit = CreateRoundedPanel(atmosGrille, "Slit", new Vector2(2, 4), 1, new Color(0.12f, 0.16f, 0.22f));
                slit.anchoredPosition = new Vector2(g * 7, 0);
            }

            var lensRim = CreateRoundedPanel(cluster, "VisualLensRim", new Vector2(14, 14), 7, new Color(0.02f, 0.03f, 0.05f));
            lensRim.anchoredPosition = new Vector2(-22, 0);
            var lensIris = CreateRoundedPanel(lensRim, "LensIris", new Vector2(6, 6), 3, new Color(0.10f, 0.40f, 0.70f));
            lensIris.anchoredPosition = Vector2.zero;

            var irLens = CreateRoundedPanel(cluster, "IRSensor", new Vector2(8, 8), 4, new Color(0.25f, 0.08f, 0.12f));
            irLens.anchoredPosition = new Vector2(-2, 0);

            var speaker = CreateRoundedPanel(cluster, "SpeakerGrille", new Vector2(40, 4), 2, new Color(0.02f, 0.03f, 0.05f));
            speaker.anchoredPosition = new Vector2(38, 0);

            var led = CreateRoundedPanel(cluster, "StatusLED", new Vector2(5, 5), 2, new Color(0.00f, 0.96f, 0.74f, 0.95f));
            led.anchoredPosition = new Vector2(80, 0);
        }

        // 1E. Bottom Bezel: Gold Pogo Dock Connector Pins, Authentic Aerospace Markings, Android Gesture Bar
        private void CreateBottomDockHardware(RectTransform parent)
        {
            var leftStamp = CreateLabel(parent, "ORBIT AEROSPACE // EXP-DATAPAD MK-VII", 11,
                new Vector2(-360, -394), new Vector2(380, 16), HardwareStamp);
            leftStamp.alignment = TextAlignmentOptions.Left;

            var rightStamp = CreateLabel(parent, "S/N: 8492-EXP-07  -  HAZMAT CLASS-1", 11,
                new Vector2(360, -394), new Vector2(380, 16), HardwareStamp);
            rightStamp.alignment = TextAlignmentOptions.Right;

            var dockSlot = CreateRoundedPanel(parent, "PogoDockConnector", new Vector2(110, 10), 4, new Color(0.02f, 0.035f, 0.05f));
            dockSlot.anchoredPosition = new Vector2(0, -394);

            for (int p = -2; p <= 2; p++)
            {
                var pin = CreateRoundedPanel(dockSlot, $"Pin_{p}", new Vector2(5, 5), 2.5f, GoldPin);
                pin.anchoredPosition = new Vector2(p * 14, 0);
            }

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

            var tintGo = new GameObject("Wallpaper Tint", typeof(RectTransform), typeof(Image));
            tintGo.transform.SetParent(viewport.transform, false);
            var tRect = (RectTransform)tintGo.transform;
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = tRect.offsetMax = Vector2.zero;
            var tintImg = tintGo.GetComponent<Image>();
            tintImg.color = new Color(0.015f, 0.022f, 0.038f, 0.65f);
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
            var bar = CreateRoundedPanel(screen, "b_Tablet_StatusBar", new Vector2(1202, 42), 0, InsetBg);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, 42);
            bar.anchoredPosition = Vector2.zero;
            osClockText = CreateLabel(bar, "", 16, new Vector2(-525, 0), new Vector2(90, 26), InkWhite);
            statusFieldGuideLabel = CreateLabel(bar, Clean(GameJam.Localization.GameLocalization.Get("tablet.status_bar")), 13, new Vector2(-325, 0), new Vector2(280, 26), MutedCyan);
            sectorLabel = CreateLabel(bar, "", 14, new Vector2(20, 0), new Vector2(300, 26), CyanAccent);
            statusArchiveLabel = CreateLabel(bar, Clean(GameJam.Localization.GameLocalization.Get("tablet.local_archive")), 12, new Vector2(405, 0), new Vector2(170, 26), MutedCyan);
            CreateNavigationButton(bar, "Cerrar tablet", TabletNavigationIcon.Symbol.Close, new Vector2(550, 0), close);
        }

        private void CreateAndroidTaskbarDock()
        {
            var dock = CreateRoundedPanel(screen, "b_Tablet_Navigation", new Vector2(500, 48), 24, InsetBg);
            dock.anchoredPosition = new Vector2(0, -325);
            backButton = CreateNavigationButton(dock, "Atras", TabletNavigationIcon.Symbol.Back, new Vector2(-150, 0), GoBack);
            CreateNavigationButton(dock, "Inicio", TabletNavigationIcon.Symbol.Home, Vector2.zero, GoHome);
            CreateNavigationButton(dock, "Aplicaciones", TabletNavigationIcon.Symbol.Apps, new Vector2(150, 0), GoHome);
        }

        private Button CreateNavigationButton(Transform parent, string label, TabletNavigationIcon.Symbol symbol,
            Vector2 position, UnityAction action)
        {
            var rect = CreateRoundedPanel(parent, label, new Vector2(56, 34), 12, new Color(1, 1, 1, 0.04f));
            rect.anchoredPosition = position;
            rect.GetComponent<TabletRoundedPanel>().raycastTarget = true;
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(TabletNavigationIcon));
            iconObject.transform.SetParent(rect, false);
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.sizeDelta = new Vector2(23, 23);
            var icon = iconObject.GetComponent<TabletNavigationIcon>();
            icon.symbol = symbol;
            icon.color = InkWhite;
            icon.raycastTarget = false;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = icon;
            var colors = button.colors;
            colors.highlightedColor = CyanAccent;
            colors.selectedColor = CyanAccent;
            colors.pressedColor = new Color(0.85f, 0.45f, 0.18f);
            colors.disabledColor = new Color(0.35f, 0.4f, 0.46f);
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }

        public void ShowBitacora() => GoHome();
        public void ShowChecklist() => ShowPage("Tareas");
        public void ShowRecipes() => ShowPage("Recetas");
        public void ShowTasks() => ShowPage("Tareas");
        public void ShowMinerals() => ShowPage("Minerales");

        public void SelectPlanet(int index)
        {
            if (catalog == null || catalog.planets == null || index < 0 || index >= catalog.planets.Length) return;
            planetIndex = index;
            GoHome();
        }

        public void ShowPage(string section)
        {
            if (section == "Bitacora") section = "Inicio";
            if (section == "Checklist") section = "Tareas";
            if (section != "Inicio" && section != "Tareas" && section != "Recetas" && section != "Minerales" && section != "Planetas" && section != "Registro") return;
            if (workspace == null || section == page) return;
            pageHistory.Push(page);
            page = section;
            RenderPage();
        }

        private void RenderPage()
        {
            missionBindings.Clear();
            for (int i = workspace.childCount - 1; i >= 0; i--)
            {
                var child = workspace.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            pageFade = 0f;
            if (pageGroup != null) pageGroup.alpha = 0f;
            if (backButton != null) backButton.interactable = pageHistory.Count > 0;
            if (sectorLabel != null) sectorLabel.text = Planet != null ? "SECTOR / " + Clean(Planet.name).ToUpperInvariant() : "SIN CATALOGO";
            if (Planet == null)
            {
                CreateLabel(workspace, "No hay un catalogo planetario asignado.", 24, Vector2.zero, new Vector2(1000, 60), InkWhite);
                return;
            }
            if (page != "Inicio")
                CreateRoundedPanel(workspace, "b_Tablet_AppSurface", new Vector2(1150, 530), 24, new Color(0.025f, 0.045f, 0.075f, 0.97f));
            if (page == "Inicio") RenderBitacora();
            else if (page == "Tareas") RenderChecklist();
            else if (page == "Recetas") RenderRecipes();
            else if (page == "Minerales") RenderMinerals();
            else if (page == "Registro") RenderMissionJournal();
            else RenderPlanets();
        }

        // ================= PAGE 1: BITACORA (EXPEDITION LOGBOOK & OVERVIEW - MARS FOCUSED) =================
        private void RenderBitacora()
        {
            var title = CreateLabel(workspace, Clean(GameJam.Localization.GameLocalization.Get("tablet.title")), 36, new Vector2(-190, 210), new Vector2(710, 58), InkWhite);
            title.alignment = TextAlignmentOptions.Left;
            var subtitle = CreateLabel(workspace, Clean(Planet.name) + " / " + Clean(GameJam.Localization.GameLocalization.Get("tablet.tools")), 19, new Vector2(-190, 163), new Vector2(710, 35), MutedCyan);
            subtitle.alignment = TextAlignmentOptions.Left;
            var mission = CreateRoundedPanel(workspace, "b_Tablet_MissionPreview", new Vector2(1050, 66), 18, new Color(0.04f, 0.065f, 0.10f, 0.88f));
            mission.anchoredPosition = new Vector2(0, 95);
            CreateLabel(mission, Clean(GameJam.Localization.GameLocalization.Get("tablet.mission")), 12, new Vector2(-450, 0), new Vector2(100, 28), CyanAccent);
            var objective = CreateLabel(mission, Clean(Planet.objective), 20, new Vector2(45, 0), new Vector2(830, 48), InkWhite);
            objective.alignment = TextAlignmentOptions.Left;
            missionBindings.Add(() => objective.text = Clean(Planet.objective) + "\n" + MissionSummary);
            objective.fontSize = 17;
            missionBindings[missionBindings.Count - 1]();

            // En Marte mostramos las aplicaciones esenciales solicitadas: Checklist y Recetario
            App(Clean(GameJam.Localization.GameLocalization.Get("tablet.checklist")), "Tareas", skin != null ? skin.objectives : null, -320, new Color(0.88f,0.39f,0.20f), Clean(GameJam.Localization.GameLocalization.Get("tablet.mission_guide")));
            App(Clean(GameJam.Localization.GameLocalization.Get("tablet.recipes")), "Recetas", skin != null ? skin.repair : null, 0, new Color(0.18f,0.48f,0.56f), Clean(GameJam.Localization.GameLocalization.Get("tablet.recipes_subtitle")));
            App("Registro", "Registro", skin != null ? skin.objectives : null, 320, new Color(.25f,.55f,.4f), "Actividad del equipo en vivo");
            var latest = CreateLabel(workspace, "", 15, new Vector2(0, -225), new Vector2(1000, 28), MutedCyan);
            BindMissionLabel(() => latest.text = Clean(this.mission != null ? this.mission.LatestActivity : "Guía de campo · Sin misión conectada"));
        }

        // ================= PAGE 2: CHECKLIST (OPERATIONAL STEPS FOR MARS) =================
        private void RenderChecklist()
        {
            if (RenderMissionChecklist()) return;
            PageHeader(Clean(GameJam.Localization.GameLocalization.Get("tablet.checklist")), Clean(Planet.objective));
            string pName = Clean(Planet.name);
            string[] steps = {
                string.Format(Clean(GameJam.Localization.GameLocalization.Get("tablet.step1")), pName),
                Clean(GameJam.Localization.GameLocalization.Get("tablet.step2")),
                Clean(GameJam.Localization.GameLocalization.Get("tablet.step3"))
            };
            for (int i = 0; i < steps.Length; i++)
            {
                var row = CreateRoundedPanel(workspace, "b_Mision_" + i, new Vector2(1050, 85), 16, CardBg);
                row.anchoredPosition = new Vector2(0, 95 - i * 105);
                CreateLabel(row, (i+1).ToString("00"), 24, new Vector2(-465, 0), new Vector2(70, 50), CyanAccent);
                var label = CreateLabel(row, steps[i], 22, new Vector2(50, 0), new Vector2(880, 65), InkWhite);
                label.alignment = TextAlignmentOptions.Left;
            }
            PageNote(Clean(GameJam.Localization.GameLocalization.Get("tablet.mission_guide")) + " / " + pName);
        }

        // ================= PAGE 3: RECETAS (ENGINEERING BLUEPRINTS - MARS) =================
        private void RenderRecipes()
        {
            if (RenderMissionRecipes()) return;
            PageHeader(Clean(GameJam.Localization.GameLocalization.Get("tablet.recipes")), Clean(GameJam.Localization.GameLocalization.Get("tablet.recipes_subtitle")) + " / " + Clean(Planet.name));
            var recipes = Planet.recipes;
            if (recipes == null || recipes.Length == 0) { PageNote(Clean(GameJam.Localization.GameLocalization.Get("tablet.no_recipes"))); return; }
            var content = AppList("b_Recetas", recipes.Length, 110f);
            for (int i = 0; i < recipes.Length; i++)
            {
                var row = CreateRoundedPanel(content, "b_Receta_" + i, new Vector2(1035, 94), 16, CardBg);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0, -50 - i * 110);
                CreateIcon(row, skin != null ? skin.repair : null, new Vector2(-470, 0), new Vector2(32,32), CyanAccent);
                var name = CreateLabel(row, Clean(recipes[i].component), 22, new Vector2(-240, 0), new Vector2(370, 70), InkWhite);
                name.alignment = TextAlignmentOptions.Left;
                var ingredients = CreateLabel(row, Ingredients(recipes[i]), 19, new Vector2(245, 0), new Vector2(490, 75), MutedCyan);
                ingredients.alignment = TextAlignmentOptions.Left;
            }
            PageNote(Clean(GameJam.Localization.GameLocalization.Get("tablet.repair_guide")));
        }

        public void GoHome()
        {
            pageHistory.Clear();
            page = "Inicio";
            if (workspace != null) RenderPage();
        }

        public void GoBack()
        {
            if (pageHistory.Count == 0) return;
            page = pageHistory.Pop();
            RenderPage();
        }

        private TextMeshProUGUI PageHeader(string title, string subtitle)
        {
            var heading = CreateLabel(workspace, title, 30, new Vector2(0, 215), new Vector2(1050, 48), InkWhite);
            heading.alignment = TextAlignmentOptions.Left;
            var sub = CreateLabel(workspace, subtitle, 17, new Vector2(0, 169), new Vector2(1050, 38), MutedCyan);
            sub.alignment = TextAlignmentOptions.Left;
            return sub;
        }

        private void PageNote(string text) => CreateLabel(workspace, text, 13, new Vector2(0,-239), new Vector2(1050,30), DimCyan);

        private void App(string title, string destination, Sprite icon, float x, Color tint, string description)
        {
            var tile = CreateRoundedPanel(workspace, "b_App_" + title, new Vector2(220,210), 22, Color.clear);
            tile.anchoredPosition = new Vector2(x, -65);
            var graphic = tile.GetComponent<TabletRoundedPanel>();
            graphic.color = new Color(1f, 1f, 1f, 0.12f);
            graphic.raycastTarget = true;
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            var colors = button.colors;
            colors.normalColor = new Color(1f,1f,1f,0f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f,0.8f,0.8f,1f);
            button.colors = colors;
            button.onClick.AddListener(() => ShowPage(destination));
            var shadow = CreateRoundedPanel(tile, "App shadow", new Vector2(108,108), 28, new Color(0,0,0,0.25f));
            shadow.anchoredPosition = new Vector2(0,32);
            var iconPanel = CreateRoundedPanel(tile, "App icon", new Vector2(104,104), 26, tint);
            iconPanel.anchoredPosition = new Vector2(0,38);
            if (icon != null) CreateIcon(iconPanel, icon, Vector2.zero, new Vector2(54,54), InkWhite);
            else CreateLabel(iconPanel, title.Substring(0,1), 42, Vector2.zero, new Vector2(80,80), InkWhite);
            CreateLabel(tile, title, 23, new Vector2(0,-40), new Vector2(220,38), InkWhite);
            CreateLabel(tile, description, 14, new Vector2(0,-76), new Vector2(220,35), MutedCyan);
        }

        private RectTransform AppList(string name, int count, float rowHeight)
        {
            var viewport = CreateRoundedPanel(workspace, name, new Vector2(1070,350), 0, Color.clear);
            viewport.anchoredPosition = new Vector2(0,-32);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.GetComponent<TabletRoundedPanel>().raycastTarget = true;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0,1); content.anchorMax = new Vector2(1,1);
            content.pivot = new Vector2(0.5f,1);
            content.sizeDelta = new Vector2(0, Mathf.Max(350, count * rowHeight));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            return content;
        }

        private void RenderMinerals()
        {
            PageHeader("Minerales", "Recursos conocidos / " + Clean(Planet.name));
            var minerals = Planet.minerals;
            if (minerals == null || minerals.Length == 0) { PageNote("No hay minerales registrados."); return; }
            var content = AppList("b_Minerales", minerals.Length, 110);
            for (int i = 0; i < minerals.Length; i++)
            {
                var row = CreateRoundedPanel(content, "b_Mineral_" + i, new Vector2(1035, 94), 16, CardBg);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
                row.anchoredPosition = new Vector2(0, -50 - i * 110);
                CreateIcon(row, skin != null ? skin.mine : null, new Vector2(-470, 0), new Vector2(40, 40), Planet.accent);
                var label = CreateLabel(row, Clean(minerals[i]), 23, new Vector2(-200, 0), new Vector2(430, 60), InkWhite);
                label.alignment = TextAlignmentOptions.Left;
                CreateLabel(row, "Usado en " + CountRecipes(minerals[i]) + " recetas", 18, new Vector2(300, 0), new Vector2(360, 50), MutedCyan);
            }
            PageNote("Recursos del planeta / Consulta las recetas para conocer sus usos.");
        }

        private void RenderPlanets()
        {
            PageHeader("Planetas", "Selecciona el archivo que quieres consultar");
            var planets = catalog.planets;
            var content = AppList("b_Planetas", planets.Length, 110);
            for (int i = 0; i < planets.Length; i++)
            {
                int index = i;
                var row = CreateRoundedPanel(content, "b_Planeta_" + i, new Vector2(1035, 94), 18, CardBg);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
                row.anchoredPosition = new Vector2(0, -50 - i * 110);
                row.GetComponent<TabletRoundedPanel>().raycastTarget = true;
                var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = row.GetComponent<TabletRoundedPanel>();
                button.onClick.AddListener(() => SelectPlanet(index));
                var planet = CreateRoundedPanel(row, "Planet icon", new Vector2(48, 48), 24, planets[i].accent);
                planet.anchoredPosition = new Vector2(-465, 0);
                var label = CreateLabel(row, Clean(planets[i].name), 24, new Vector2(-220, 0), new Vector2(370, 60), InkWhite);
                label.alignment = TextAlignmentOptions.Left;
                CreateLabel(row, i == planetIndex ? "SELECCIONADO" : "ABRIR ARCHIVO", 14, new Vector2(345, 0), new Vector2(260, 40), i == planetIndex ? CyanAccent : MutedCyan);
            }
            PageNote("El archivo seleccionado se aplica a todas las apps de la guia.");
        }

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
            tmp.raycastTarget = false;
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

        private int CountRecipes(string mineral)
        {
            int count = 0;
            if (Planet == null || Planet.recipes == null) return count;
            foreach (var recipe in Planet.recipes)
                foreach (var ingredient in recipe.ingredients ?? Array.Empty<TabletRecipeCatalog.Ingredient>())
                    if (ingredient.mineral == mineral) { count++; break; }
            return count;
        }

        private static string Ingredients(TabletRecipeCatalog.Recipe recipe)
        {
            var result = new StringBuilder();
            foreach (var ingredient in recipe.ingredients ?? Array.Empty<TabletRecipeCatalog.Ingredient>())
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
                .Replace('ã', 'a').Replace('Ã', 'A')
                .Replace('õ', 'o').Replace('Õ', 'O')
                .Replace('ç', 'c').Replace('Ç', 'C')
                .Replace('ê', 'e').Replace('Ê', 'E')
                .Replace('ô', 'o').Replace('Ô', 'O')
                .Replace('â', 'a').Replace('Â', 'A')
                .Replace('à', 'a').Replace('À', 'A')
                .Replace('·', '-')
                .Replace('→', '>')
                .Replace('➔', '>')
                .Replace('×', 'x');
        }

        private void Update()
        {
            UpdateMissionFeedback();
            if (overlay == null || chassis == null) return;
            pageFade = Mathf.MoveTowards(pageFade, 1f, Time.unscaledDeltaTime * 7f);
            if (pageGroup != null) pageGroup.alpha = pageFade;
            string minute = DateTime.Now.ToString("HH:mm");
            if (osClockText != null && minute != clockMinute) { clockMinute = minute; osClockText.text = minute; }
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
