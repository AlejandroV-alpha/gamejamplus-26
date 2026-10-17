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
        private readonly List<Button> dockButtons = new();
        private readonly List<TextMeshProUGUI> dockLabels = new();
        private float progress;
        private int planetIndex = 1;
        private string page = "Inicio";

        // Pixel Sci-Fi Palette:
        private static readonly Color CyanAccent = new(0.00f, 0.92f, 0.88f, 1f);     // Vibrant cyan
        private static readonly Color InkWhite = new(0.96f, 0.98f, 1.00f, 1f);       // Ultra-crisp reading white
        private static readonly Color MutedCyan = new(0.45f, 0.66f, 0.78f, 1f);      // Soft technical secondary
        private static readonly Color DimCyan = new(0.24f, 0.38f, 0.48f, 1f);        // Muted labels
        private static readonly Color CardBg = new(0.045f, 0.075f, 0.12f, 0.97f);    // High-contrast solid card
        private static readonly Color InsetBg = new(0.02f, 0.035f, 0.055f, 0.95f);   // High-contrast sunken box
        private static readonly Color SlotBg = new(0.08f, 0.13f, 0.20f, 0.95f);     // Item slot tint

        // Physical Tablet Chassis Palette:
        private static readonly Color ChassisAlloy = new(0.07f, 0.09f, 0.12f, 1f);    // Rugged space-grade matte body
        private static readonly Color ChassisBevel = new(0.16f, 0.21f, 0.28f, 1f);    // Outer frame chamfer rim
        private static readonly Color ChassisGrip = new(0.04f, 0.055f, 0.075f, 1f);   // Tactile grip ribs
        private static readonly Color HardwareStamp = new(0.24f, 0.32f, 0.42f, 1f); // Silk-screen model stamp

        private const float PPU = 0.5f; // 2x retro pixel-art density scaling

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
            var worldShade = CreateSlicedImage(root.transform, "World shade", new Vector2(5000, 5000), null, new Color(0.01f, 0.015f, 0.03f, 0.78f));
            worldShade.anchoredPosition = Vector2.zero;

            // ================= 1. PHYSICAL TABLET HARDWARE CHASSIS =================
            // Sized like a true tactical field datapad (1280 x 810)
            chassis = CreateSlicedImage(root.transform, "Physical Tablet Chassis", new Vector2(1280, 810), skin != null ? skin.panel : null, ChassisAlloy);
            chassis.anchoredPosition = Vector2.zero;

            // Outer metallic rim outline
            var chassisOutline = chassis.gameObject.AddComponent<Outline>();
            chassisOutline.effectColor = ChassisBevel;
            chassisOutline.effectDistance = new Vector2(2, -2);

            // Tactile side grip ribs (Left and Right edges of chassis)
            CreateChassisGrips(chassis);

            // Top bezel hardware (Camera sensor, Status LED, Speaker)
            CreateTopBezelHardware(chassis);

            // Bottom bezel hardware (Silk-screen stamp, Home touch indicator)
            CreateBottomBezelHardware(chassis);

            // ================= 2. RECESSED GLASS DISPLAY SCREEN =================
            // Screen area inset within the physical bezel
            screen = CreateSlicedImage(chassis, "Glass Screen Area", new Vector2(1190, 715), skin != null ? skin.window : null, Color.white);
            screen.anchoredPosition = new Vector2(0, -2);

            // Screen edge shadow / inner bevel
            var screenMask = screen.gameObject.AddComponent<RectMask2D>();

            // Expedition selfie wallpaper layer (with strong dark glass contrast tint)
            AddWallpaper();

            // ================= 3. STREAMLINED TABLET OS INTERFACE =================
            // Single clean Top OS Bar (Title, Planet Tabs, Clock, Close button)
            CreateTopOSBar(close);

            // Central dynamic workspace for pages (Inicio, Tareas, Recetas, Minerales)
            workspace = new GameObject("Widget workspace", typeof(RectTransform)).GetComponent<RectTransform>();
            workspace.transform.SetParent(screen, false);
            workspace.anchorMin = new Vector2(0.5f, 0.5f);
            workspace.anchorMax = new Vector2(0.5f, 0.5f);
            workspace.sizeDelta = new Vector2(1170, 570);
            workspace.anchoredPosition = new Vector2(0, -18);

            // Bottom Navigation Dock (Inicio, Tareas, Recetas, Minerales)
            CreateBottomNavigationDock();

            RenderPage();
        }

        private void CreateEquipmentHint()
        {
            var hint = CreateSlicedImage(transform, "Equipment hint", new Vector2(280, 42), skin != null ? skin.inset : null, InsetBg);
            hint.anchorMin = hint.anchorMax = new Vector2(1, 0);
            hint.anchoredPosition = new Vector2(-155, 30);

            if (skin != null && skin.objectives != null)
            {
                CreateIcon(hint, skin.objectives, new Vector2(-110, 0), new Vector2(20, 20), CyanAccent);
            }

            var label = CreateLabel(hint, "01  TABLET EQUIPADA  [TAB]", 14, new Vector2(14, 0), new Vector2(230, 26), CyanAccent);
            label.alignment = TextAlignmentOptions.Center;
        }

        private void CreateChassisGrips(RectTransform parent)
        {
            // Tactile ribbed rubber grips on left and right borders of the tablet body
            float[] yOffsets = { -120, -70, -20, 30, 80, 130 };
            foreach (float y in yOffsets)
            {
                // Left grip rib
                var leftRib = CreateSlicedImage(parent, "Grip_L", new Vector2(8, 28), skin != null ? skin.inset : null, ChassisGrip);
                leftRib.anchoredPosition = new Vector2(-630, y);

                // Right grip rib
                var rightRib = CreateSlicedImage(parent, "Grip_R", new Vector2(8, 28), skin != null ? skin.inset : null, ChassisGrip);
                rightRib.anchoredPosition = new Vector2(630, y);
            }

            // Power button tab on right outer frame
            var pwrBtn = CreateSlicedImage(parent, "Pwr_Button", new Vector2(6, 46), skin != null ? skin.inset : null, ChassisBevel);
            pwrBtn.anchoredPosition = new Vector2(642, 220);
        }

        private void CreateTopBezelHardware(RectTransform parent)
        {
            var cluster = new GameObject("Sensor Cluster", typeof(RectTransform)).GetComponent<RectTransform>();
            cluster.transform.SetParent(parent, false);
            cluster.anchoredPosition = new Vector2(0, 385);
            cluster.sizeDelta = new Vector2(160, 20);

            // Front optical lens
            var lensRim = CreateSlicedImage(cluster, "LensRim", new Vector2(12, 12), skin != null ? skin.slot : null, new Color(0.02f, 0.03f, 0.05f));
            lensRim.anchoredPosition = new Vector2(-25, 0);
            var lensIris = CreateSlicedImage(lensRim, "LensIris", new Vector2(4, 4), null, new Color(0.12f, 0.45f, 0.75f));
            lensIris.anchoredPosition = Vector2.zero;

            // Technical speaker grille slot
            var grille = CreateSlicedImage(cluster, "SpeakerGrille", new Vector2(38, 4), skin != null ? skin.inset : null, ChassisGrip);
            grille.anchoredPosition = new Vector2(5, 0);

            // Operational status LED
            var led = CreateSlicedImage(cluster, "StatusLED", new Vector2(5, 5), null, new Color(0.00f, 0.96f, 0.74f, 0.90f));
            led.anchoredPosition = new Vector2(35, 0);
        }

        private void CreateBottomBezelHardware(RectTransform parent)
        {
            // Silk-screen model stamp
            var stamp = CreateLabel(parent, "ORBIT AEROSPACE // EXP-PAD 07  -  MIL-SPEC DATAPAD", 11,
                new Vector2(0, -388), new Vector2(450, 16), HardwareStamp);
            stamp.alignment = TextAlignmentOptions.Center;

            // Touch home indicator bar
            var homeBar = CreateSlicedImage(parent, "HomeBar", new Vector2(110, 4), skin != null ? skin.inset : null, new Color(0.00f, 0.92f, 0.88f, 0.35f));
            homeBar.anchoredPosition = new Vector2(0, -374);
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

            // Deep dark glass tint (0.72) to balance rich sci-fi artwork with perfect UI text readability
            var tintGo = new GameObject("Wallpaper Tint", typeof(RectTransform), typeof(Image));
            tintGo.transform.SetParent(viewport.transform, false);
            var tRect = (RectTransform)tintGo.transform;
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = tRect.offsetMax = Vector2.zero;
            var tintImg = tintGo.GetComponent<Image>();
            tintImg.color = new Color(0.015f, 0.022f, 0.038f, 0.72f);
            tintImg.raycastTarget = false;
        }

        private void CreateTopOSBar(UnityAction close)
        {
            var bar = CreateSlicedImage(screen, "OS Top Bar", new Vector2(1170, 36), skin != null ? skin.inset : null, InsetBg);
            bar.anchoredPosition = new Vector2(0, 325);

            // OS Brand & Version
            var osLabel = CreateLabel(bar, "ORBIT OS v3.2", 14, new Vector2(-490, 0), new Vector2(160, 24), CyanAccent);
            osLabel.alignment = TextAlignmentOptions.Left;

            // Planet Selector Tabs (Embedded seamlessly in the top bar)
            var planets = catalog != null ? catalog.planets : null;
            if (planets != null)
            {
                planetTabButtons.Clear();
                planetTabLabels.Clear();

                float startX = -320f;
                float spacing = 135f;
                for (int i = 0; i < planets.Length; i++)
                {
                    int index = i;
                    string planetName = Clean(planets[i].name.ToUpperInvariant());
                    var (btn, lbl) = CreateTabButton(bar, planetName, new Vector2(startX + i * spacing, 0), new Vector2(125, 28),
                        () => SelectPlanet(index));
                    planetTabButtons.Add(btn);
                    planetTabLabels.Add(lbl);
                }
            }

            // Sol Date & Telemetry
            osClockText = CreateLabel(bar, "SOL 42 - 14:38  [PWR 98%]", 12, new Vector2(360, 0), new Vector2(230, 20), MutedCyan);
            osClockText.alignment = TextAlignmentOptions.Right;

            // Close Button [X]
            var closeBtn = CreateIconButton(bar, skin != null ? skin.closeIcon : null, "X",
                new Vector2(560, 0), new Vector2(28, 26), () => close());
        }

        private void CreateBottomNavigationDock()
        {
            var dock = CreateSlicedImage(screen, "Bottom Dock", new Vector2(680, 44), skin != null ? skin.inset : null, InsetBg);
            dock.anchoredPosition = new Vector2(0, -325);

            dockButtons.Clear();
            dockLabels.Clear();

            var sections = new[] { "Inicio", "Tareas", "Recetas", "Minerales" };
            float startX = -240f;
            float spacing = 160f;

            for (int i = 0; i < sections.Length; i++)
            {
                string sec = sections[i];
                var (btn, lbl) = CreateNavButton(dock, sec.ToUpperInvariant(), new Vector2(startX + i * spacing, 0), new Vector2(150, 32),
                    () => ShowPage(sec));
                dockButtons.Add(btn);
                dockLabels.Add(lbl);
            }
        }

        public void ShowTasks() => ShowPage("Tareas");
        public void ShowMinerals() => ShowPage("Minerales");

        public void SelectPlanet(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.planets.Length) return;
            planetIndex = index;
            RenderPage();
        }

        public void ShowPage(string section)
        {
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

            // Update planet selector tabs
            for (int i = 0; i < planetTabButtons.Count; i++)
            {
                bool active = (i == planetIndex);
                var btnImg = planetTabButtons[i].GetComponent<Image>();
                btnImg.sprite = active && skin != null && skin.selectedTab != null ? skin.selectedTab : (skin != null ? skin.tab : null);
                btnImg.color = active ? Color.white : new Color(0.70f, 0.85f, 0.95f, 0.85f);
                planetTabLabels[i].color = active ? CyanAccent : MutedCyan;
            }

            // Update bottom navigation buttons
            var sections = new[] { "Inicio", "Tareas", "Recetas", "Minerales" };
            for (int i = 0; i < dockButtons.Count; i++)
            {
                bool active = (sections[i] == page);
                var btnImg = dockButtons[i].GetComponent<Image>();
                btnImg.sprite = active && skin != null && skin.selectedTab != null ? skin.selectedTab : (skin != null ? skin.button : null);
                btnImg.color = active ? Color.white : new Color(0.85f, 0.92f, 1.00f, 0.90f);
                dockLabels[i].color = active ? CyanAccent : InkWhite;
            }

            if (page == "Inicio") RenderHome();
            else if (page == "Tareas") RenderTasks();
            else if (page == "Recetas") RenderRecipes();
            else if (page == "Minerales") RenderMinerals();
        }

        // ================= PAGE 1: INICIO (PRIMARY MISSION FOCUSED) =================
        private void RenderHome()
        {
            // ----------------- LEFT HERO COLUMN: PRIMARY MISSION (DOMINANT FOCUS) -----------------
            var heroCard = CreateSlicedImage(workspace, "Hero_Mission", new Vector2(700, 470), skin != null ? skin.panel : null, CardBg);
            heroCard.anchoredPosition = new Vector2(-225, 0);

            // Top Header Line
            CreateIcon(heroCard, skin != null ? skin.ship : null, new Vector2(-305, 205), new Vector2(26, 26), CyanAccent);
            var mTitle = CreateLabel(heroCard, $"MISION PRINCIPAL  -  SECTOR {Clean(Planet.name).ToUpperInvariant()}", 18,
                new Vector2(-15, 205), new Vector2(530, 26), CyanAccent);
            mTitle.alignment = TextAlignmentOptions.Left;

            var prioBadge = CreateSlicedImage(heroCard, "PrioBadge", new Vector2(100, 26), skin != null ? skin.inset : null, InsetBg);
            prioBadge.anchoredPosition = new Vector2(285, 205);
            var prioText = CreateLabel(prioBadge, "[PRIORIDAD 1]", 11, Vector2.zero, new Vector2(95, 20), new Color(1f, 0.45f, 0.35f));
            prioText.alignment = TextAlignmentOptions.Center;

            // Big Objective Statement Box
            var objBox = CreateSlicedImage(heroCard, "ObjectiveBox", new Vector2(660, 95), skin != null ? skin.inset : null, InsetBg);
            objBox.anchoredPosition = new Vector2(0, 130);
            var objSub = CreateLabel(objBox, "OBJETIVO DE EXPEDICION:", 11, new Vector2(0, 32), new Vector2(620, 18), DimCyan);
            objSub.alignment = TextAlignmentOptions.Left;
            var objMain = CreateLabel(objBox, Clean(Planet.objective), 16, new Vector2(0, -10), new Vector2(620, 56), InkWhite);
            objMain.alignment = TextAlignmentOptions.Left;

            // Operational Checklist Preview (Direct actionable steps inside the hero card)
            var checkCard = CreateSlicedImage(heroCard, "ChecklistCard", new Vector2(660, 160), skin != null ? skin.inset : null, InsetBg);
            checkCard.anchoredPosition = new Vector2(0, -15);

            var checkHeader = CreateLabel(checkCard, "ESTADO DEL PROTOCOLO DE REPARACION:", 12, new Vector2(0, 60), new Vector2(620, 20), MutedCyan);
            checkHeader.alignment = TextAlignmentOptions.Left;

            string[] stepTexts = {
                $"[x] 01  Diagnostico inicial de telemetria completado",
                $"[>] 02  Recolectar suministros en {Clean(Planet.name)}",
                $"[ ] 03  Fabricar e instalar componentes averiados",
                $"[ ] 04  Secuencia de ignicion y retorno a la orbita"
            };

            for (int s = 0; s < stepTexts.Length; s++)
            {
                bool inProgress = (s == 1);
                Color stepColor = inProgress ? CyanAccent : (s == 0 ? InkWhite : DimCyan);
                var sLabel = CreateLabel(checkCard, stepTexts[s], 13, new Vector2(0, 26 - s * 26), new Vector2(620, 22), stepColor);
                sLabel.alignment = TextAlignmentOptions.Left;
            }

            // Primary Call to Action Button
            var (heroBtn, _) = CreateActionButton(heroCard, "CONTINUAR PROTOCOLO DE MISION  >",
                new Vector2(0, -185), new Vector2(660, 42), () => ShowTasks());

            // ----------------- RIGHT COLUMN: COMPLEMENTARY TELEMETRY WIDGETS -----------------

            // Widget 1: Próxima Reparación Crítica (Top-Right)
            var widgetRepair = CreateSlicedImage(workspace, "Widget_Repair", new Vector2(445, 225), skin != null ? skin.panel : null, CardBg);
            widgetRepair.anchoredPosition = new Vector2(360, 122);

            CreateIcon(widgetRepair, skin != null ? skin.repair : null, new Vector2(-185, 82), new Vector2(22, 22), CyanAccent);
            var wrTitle = CreateLabel(widgetRepair, "PROXIMA REPARACION", 14, new Vector2(10, 82), new Vector2(350, 22), CyanAccent);
            wrTitle.alignment = TextAlignmentOptions.Left;

            var wrInset = CreateSlicedImage(widgetRepair, "Repair_Inset", new Vector2(415, 85), skin != null ? skin.inset : null, InsetBg);
            wrInset.anchoredPosition = new Vector2(0, 16);

            if (Planet.recipes != null && Planet.recipes.Length > 0)
            {
                var r0 = Planet.recipes[0];
                var cLabel = CreateLabel(wrInset, Clean(r0.component).ToUpperInvariant(), 15, new Vector2(0, 20), new Vector2(390, 24), InkWhite);
                cLabel.alignment = TextAlignmentOptions.Left;

                var ingLabel = CreateLabel(wrInset, Ingredients(r0), 13, new Vector2(0, -18), new Vector2(390, 36), CyanAccent);
                ingLabel.alignment = TextAlignmentOptions.Left;
            }

            var (wrBtn, _) = CreateActionButton(widgetRepair, "VER RECETAS  >", new Vector2(0, -68), new Vector2(415, 34), () => ShowPage("Recetas"));

            // Widget 2: Minerales Detectados (Bottom-Right)
            var widgetMin = CreateSlicedImage(workspace, "Widget_Minerals", new Vector2(445, 225), skin != null ? skin.panel : null, CardBg);
            widgetMin.anchoredPosition = new Vector2(360, -122);

            CreateIcon(widgetMin, skin != null ? skin.mine : null, new Vector2(-185, 82), new Vector2(22, 22), CyanAccent);
            var wmTitle = CreateLabel(widgetMin, "RECURSOS DETECTADOS", 14, new Vector2(10, 82), new Vector2(350, 22), CyanAccent);
            wmTitle.alignment = TextAlignmentOptions.Left;

            var wmInset = CreateSlicedImage(widgetMin, "Min_Inset", new Vector2(415, 85), skin != null ? skin.inset : null, InsetBg);
            wmInset.anchoredPosition = new Vector2(0, 16);

            string minList = string.Join("  -  ", Array.ConvertAll(Planet.minerals, m => Clean(m).ToUpperInvariant()));
            var minText = CreateLabel(wmInset, minList, 14, new Vector2(0, 12), new Vector2(390, 34), InkWhite);
            minText.alignment = TextAlignmentOptions.Center;

            var minSub = CreateLabel(wmInset, $"{Planet.minerals.Length} minerales registrados en el sector", 11, new Vector2(0, -22), new Vector2(390, 18), DimCyan);
            minSub.alignment = TextAlignmentOptions.Center;

            var (wmBtn, _) = CreateActionButton(widgetMin, "CONSULTAR MINERALES  >", new Vector2(0, -68), new Vector2(415, 34), () => ShowMinerals());
        }

        // ================= PAGE 2: TAREAS (MISSION OBJECTIVES) =================
        private void RenderTasks()
        {
            var header = CreateLabel(workspace, $"SEGUIMIENTO DE PROTOCOLO  -  SECTOR {Clean(Planet.name).ToUpperInvariant()}", 18,
                new Vector2(0, 225), new Vector2(1150, 28), CyanAccent);
            header.alignment = TextAlignmentOptions.Left;

            var steps = new[] {
                $"Localizar zona de impacto y diagnosticar sistemas de {Clean(Planet.name)}",
                "Recolectar los minerales y recursos especificados en el plano tecnico",
                "Reparar los componentes en la estacion de ensamblaje",
                "Completar la secuencia de ignicion y retorno a la nave central"
            };

            for (int i = 0; i < steps.Length; i++)
            {
                var row = CreateSlicedImage(workspace, $"Task_Row_{i}", new Vector2(1150, 88), skin != null ? skin.panel : null, CardBg);
                row.anchoredPosition = new Vector2(0, 140 - i * 105);

                // Slot with check icon
                var slot = CreateSlicedImage(row, "Slot", new Vector2(42, 42), skin != null ? skin.slot : null, SlotBg);
                slot.anchoredPosition = new Vector2(-525, 0);
                if (skin != null && skin.checkIcon != null)
                {
                    CreateIcon(slot, skin.checkIcon, Vector2.zero, new Vector2(24, 24), CyanAccent);
                }

                // Step index
                var numLabel = CreateLabel(row, (i + 1).ToString("00"), 18, new Vector2(-470, 0), new Vector2(45, 36), CyanAccent);
                numLabel.alignment = TextAlignmentOptions.Center;

                // Step description
                var stepDesc = CreateLabel(row, steps[i], 15, new Vector2(50, 0), new Vector2(850, 50), InkWhite);
                stepDesc.alignment = TextAlignmentOptions.Left;

                // Status tag inset
                var tag = CreateSlicedImage(row, "Tag", new Vector2(120, 30), skin != null ? skin.inset : null, InsetBg);
                tag.anchoredPosition = new Vector2(495, 0);
                var tagLabel = CreateLabel(tag, i == 1 ? "[EN CURSO]" : (i == 0 ? "[COMPLETO]" : "[PENDIENTE]"), 11,
                    Vector2.zero, new Vector2(115, 22), i == 1 ? CyanAccent : (i == 0 ? InkWhite : DimCyan));
                tagLabel.alignment = TextAlignmentOptions.Center;
            }

            var note = CreateLabel(workspace, "ESTADO OPERATIVO // ACTUALIZACION TELEMETRICA EN TIEMPO REAL", 11,
                new Vector2(0, -245), new Vector2(1150, 20), DimCyan);
            note.alignment = TextAlignmentOptions.Center;
        }

        // ================= PAGE 3: RECETAS (ENGINEERING BLUEPRINTS) =================
        private void RenderRecipes()
        {
            var header = CreateLabel(workspace, $"PLANOS TECNICOS DE REPARACION  -  SECTOR {Clean(Planet.name).ToUpperInvariant()}", 18,
                new Vector2(0, 225), new Vector2(1150, 28), CyanAccent);
            header.alignment = TextAlignmentOptions.Left;

            for (int i = 0; i < Planet.recipes.Length; i++)
            {
                var recipe = Planet.recipes[i];
                var row = CreateSlicedImage(workspace, $"Recipe_{i}", new Vector2(1150, 95), skin != null ? skin.panel : null, CardBg);
                row.anchoredPosition = new Vector2(0, 135 - i * 115);

                // Slot with repair gear icon
                var slot = CreateSlicedImage(row, "Slot", new Vector2(46, 46), skin != null ? skin.slot : null, SlotBg);
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
                var ingInset = CreateSlicedImage(row, "Ing_Inset", new Vector2(500, 55), skin != null ? skin.inset : null, InsetBg);
                ingInset.anchoredPosition = new Vector2(230, 0);
                var ingLabel = CreateLabel(ingInset, Ingredients(recipe), 14, Vector2.zero, new Vector2(480, 44), CyanAccent);
                ingLabel.alignment = TextAlignmentOptions.Center;

                // Status tag
                var tag = CreateSlicedImage(row, "Tag", new Vector2(95, 30), skin != null ? skin.inset : null, InsetBg);
                tag.anchoredPosition = new Vector2(505, 0);
                var tagLabel = CreateLabel(tag, "[REQUERIDO]", 11, Vector2.zero, new Vector2(90, 22), new Color(1f, 0.45f, 0.35f));
                tagLabel.alignment = TextAlignmentOptions.Center;
            }

            var note = CreateLabel(workspace, "LOS COMPONENTES REQUIEREN SUMINISTROS ESPECIFICOS DEL SECTOR PLANETARIO", 11,
                new Vector2(0, -245), new Vector2(1150, 20), DimCyan);
            note.alignment = TextAlignmentOptions.Center;
        }

        // ================= PAGE 4: MINERALES (SPECTROMETRY CATALOG) =================
        private void RenderMinerals()
        {
            var header = CreateLabel(workspace, $"CATALOGO ESPECTROMETRICO DE RECURSOS  -  SECTOR {Clean(Planet.name).ToUpperInvariant()}", 18,
                new Vector2(0, 225), new Vector2(1150, 28), CyanAccent);
            header.alignment = TextAlignmentOptions.Left;

            float cardWidth = 360f;
            float spacing = 390f;
            float startX = -(Planet.minerals.Length - 1) * 0.5f * spacing;

            for (int i = 0; i < Planet.minerals.Length; i++)
            {
                string mineral = Planet.minerals[i];
                var card = CreateSlicedImage(workspace, $"Mineral_{i}", new Vector2(cardWidth, 350), skin != null ? skin.panel : null, CardBg);
                card.anchoredPosition = new Vector2(startX + i * spacing, 5);

                // Mineral slot / bracket container
                var bracket = CreateSlicedImage(card, "Bracket", new Vector2(120, 120), skin != null ? skin.bracket : null, SlotBg);
                bracket.anchoredPosition = new Vector2(0, 80);

                // Crystal visual shard
                var crystalGo = new GameObject("Crystal Shard", typeof(RectTransform), typeof(TabletCrystalIcon));
                crystalGo.transform.SetParent(bracket.transform, false);
                var cRect = (RectTransform)crystalGo.transform;
                cRect.sizeDelta = new Vector2(70, 90);
                cRect.anchoredPosition = Vector2.zero;
                var crystal = crystalGo.GetComponent<TabletCrystalIcon>();
                crystal.color = (i == 1 && Planet != null) ? Planet.accent : CyanAccent;
                crystal.raycastTarget = false;

                // Mineral Name
                var nameLabel = CreateLabel(card, Clean(mineral).ToUpperInvariant(), 18, new Vector2(0, -15), new Vector2(330, 28), InkWhite);
                nameLabel.alignment = TextAlignmentOptions.Center;

                // Inset stats box
                var statsInset = CreateSlicedImage(card, "Stats", new Vector2(320, 60), skin != null ? skin.inset : null, InsetBg);
                statsInset.anchoredPosition = new Vector2(0, -70);

                int usedCount = CountRecipes(mineral);
                var statsLabel = CreateLabel(statsInset, $"UTILIZADO EN {usedCount} RECETAS\nCOMPOSICION GEOLOGICA CLASE A", 12,
                    Vector2.zero, new Vector2(300, 48), CyanAccent);
                statsLabel.alignment = TextAlignmentOptions.Center;

                // Analysis tag
                var aTag = CreateSlicedImage(card, "AnalysisTag", new Vector2(260, 26), skin != null ? skin.inset : null, InsetBg);
                aTag.anchoredPosition = new Vector2(0, -130);
                var aLabel = CreateLabel(aTag, "ESPECTRO: ANALIZADO Y DISPONIBLE", 11, Vector2.zero, new Vector2(250, 20), MutedCyan);
                aLabel.alignment = TextAlignmentOptions.Center;
            }

            var note = CreateLabel(workspace, "BASE DE DATOS ESPECTROMETRICA ORBITAL  -  DATAPAD MIL-SPEC", 11,
                new Vector2(0, -245), new Vector2(1150, 20), DimCyan);
            note.alignment = TextAlignmentOptions.Center;
        }

        // ================= UI HELPER BUILDERS (UGUI BEST PRACTICES) =================

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
            img.raycastTarget = false; // By default non-interactive
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

        private (Button button, TextMeshProUGUI label) CreateActionButton(Transform parent, string text, Vector2 position, Vector2 size, UnityAction action)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var img = go.GetComponent<Image>();
            img.sprite = skin != null ? skin.button : null;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = PPU;
            img.color = Color.white;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.SpriteSwap;
            var ss = btn.spriteState;
            ss.highlightedSprite = skin != null && skin.hover != null ? skin.hover : img.sprite;
            ss.pressedSprite = skin != null && skin.pressed != null ? skin.pressed : img.sprite;
            btn.spriteState = ss;
            btn.onClick.AddListener(action);

            var lbl = CreateLabel(rect, text, 14, Vector2.zero, size - new Vector2(16, 4), InkWhite);
            lbl.alignment = TextAlignmentOptions.Center;
            return (btn, lbl);
        }

        private (Button button, TextMeshProUGUI label) CreateTabButton(Transform parent, string text, Vector2 position, Vector2 size, UnityAction action)
        {
            var go = new GameObject("Tab_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var img = go.GetComponent<Image>();
            img.sprite = skin != null ? skin.tab : null;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = PPU;
            img.color = Color.white;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.SpriteSwap;
            var ss = btn.spriteState;
            ss.highlightedSprite = skin != null && skin.tabHover != null ? skin.tabHover : (skin != null ? skin.hover : null);
            ss.pressedSprite = skin != null && skin.pressed != null ? skin.pressed : null;
            btn.spriteState = ss;
            btn.onClick.AddListener(action);

            var lbl = CreateLabel(rect, text, 13, Vector2.zero, size - new Vector2(8, 4), MutedCyan);
            lbl.alignment = TextAlignmentOptions.Center;
            return (btn, lbl);
        }

        private (Button button, TextMeshProUGUI label) CreateNavButton(Transform parent, string text, Vector2 position, Vector2 size, UnityAction action)
        {
            var go = new GameObject("Nav_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var img = go.GetComponent<Image>();
            img.sprite = skin != null ? skin.button : null;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = PPU;
            img.color = Color.white;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.SpriteSwap;
            var ss = btn.spriteState;
            ss.highlightedSprite = skin != null && skin.hover != null ? skin.hover : null;
            ss.pressedSprite = skin != null && skin.pressed != null ? skin.pressed : null;
            btn.spriteState = ss;
            btn.onClick.AddListener(action);

            var lbl = CreateLabel(rect, text, 14, Vector2.zero, size - new Vector2(10, 4), InkWhite);
            lbl.alignment = TextAlignmentOptions.Center;
            return (btn, lbl);
        }

        private Button CreateIconButton(Transform parent, Sprite icon, string fallbackText, Vector2 position, Vector2 size, UnityAction action)
        {
            var go = new GameObject("IconBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var img = go.GetComponent<Image>();
            img.sprite = skin != null ? skin.button : null;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = PPU;
            img.color = Color.white;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.SpriteSwap;
            var ss = btn.spriteState;
            ss.highlightedSprite = skin != null && skin.hover != null ? skin.hover : null;
            ss.pressedSprite = skin != null && skin.pressed != null ? skin.pressed : null;
            btn.spriteState = ss;
            btn.onClick.AddListener(action);

            if (icon != null)
            {
                CreateIcon(rect, icon, Vector2.zero, size * 0.55f, CyanAccent);
            }
            else
            {
                var lbl = CreateLabel(rect, fallbackText, 14, Vector2.zero, size, InkWhite);
                lbl.alignment = TextAlignmentOptions.Center;
            }

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
