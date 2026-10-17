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
        private RectTransform frame, screen, workspace;
        private CanvasGroup overlay;
        private TabletRecipeCatalog catalog;
        private TabletUISkin skin;
        private TMP_FontAsset tmpFont;
        private TextMeshProUGUI planetHeaderLabel, osClockText, statusBannerLeft, statusBannerRight;
        private readonly List<Button> planetTabButtons = new();
        private readonly List<TextMeshProUGUI> planetTabLabels = new();
        private readonly List<Button> dockButtons = new();
        private readonly List<TextMeshProUGUI> dockLabels = new();
        private float progress;
        private int planetIndex = 1;
        private string page = "Inicio";

        // Pixel Sci-Fi Cyan Theme Palette:
        private static readonly Color CyanAccent = new(0.00f, 0.92f, 0.88f, 1f);     // Vibrant cyan
        private static readonly Color CyanHover = new(0.35f, 0.96f, 1.00f, 1f);      // Bright electric hover
        private static readonly Color InkWhite = new(0.95f, 0.98f, 1.00f, 1f);       // Crisp white
        private static readonly Color MutedCyan = new(0.42f, 0.65f, 0.78f, 1f);      // Technical secondary
        private static readonly Color DimCyan = new(0.24f, 0.40f, 0.52f, 1f);        // Subtle labels
        private static readonly Color PanelTint = new(0.05f, 0.08f, 0.13f, 0.94f);   // 9-slice panel tint
        private static readonly Color InsetTint = new(0.02f, 0.035f, 0.06f, 0.92f);  // Sunken readout tint
        private static readonly Color SlotTint = new(0.08f, 0.14f, 0.22f, 0.95f);   // Item / crystal slot tint

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
            var worldShade = CreateSlicedImage(root.transform, "World shade", new Vector2(5000, 5000), null, new Color(0.01f, 0.015f, 0.03f, 0.75f));
            worldShade.anchoredPosition = Vector2.zero;

            // Main physical tablet window chassis (Using authentic 9-sliced window.png)
            frame = CreateSlicedImage(root.transform, "Tablet Window Frame", new Vector2(1360, 830), skin != null ? skin.window : null, Color.white);
            frame.anchoredPosition = Vector2.zero;

            // Interior screen container within window bezel
            screen = new GameObject("Screen Area", typeof(RectTransform)).GetComponent<RectTransform>();
            screen.transform.SetParent(frame, false);
            screen.anchorMin = Vector2.zero;
            screen.anchorMax = Vector2.one;
            screen.offsetMin = new Vector2(16, 16);
            screen.offsetMax = new Vector2(-16, -16);

            // Expedition selfie wallpaper layer (with dark tint for high text contrast)
            AddWallpaper();

            // OS Status Bar (Top edge)
            CreateTopOSBar();

            // Window Header (Terminal brand, Planet tabs, Close button)
            CreateWindowHeader(close);

            // Sub-header Banner (Active Sector Spectrometry Readout)
            CreateSubHeaderBanner();

            // Central dynamic workspace for pages (Inicio, Tareas, Recetas, Minerales)
            workspace = new GameObject("Widget workspace", typeof(RectTransform)).GetComponent<RectTransform>();
            workspace.transform.SetParent(screen, false);
            workspace.anchorMin = new Vector2(0.5f, 0.5f);
            workspace.anchorMax = new Vector2(0.5f, 0.5f);
            workspace.sizeDelta = new Vector2(1280, 500);
            workspace.anchoredPosition = new Vector2(0, -22);

            // Bottom Navigation Dock (Inicio, Tareas, Recetas, Minerales)
            CreateBottomNavigationDock();

            RenderPage();
        }

        private void CreateEquipmentHint()
        {
            var hint = CreateSlicedImage(transform, "Equipment hint", new Vector2(280, 42), skin != null ? skin.inset : null, InsetTint);
            hint.anchorMin = hint.anchorMax = new Vector2(1, 0);
            hint.anchoredPosition = new Vector2(-155, 30);

            if (skin != null && skin.objectives != null)
            {
                var icon = CreateIcon(hint, skin.objectives, new Vector2(-110, 0), new Vector2(20, 20), CyanAccent);
            }

            var label = CreateLabel(hint, "01  TABLET EQUIPADA  [TAB]", 14, new Vector2(14, 0), new Vector2(230, 26), CyanAccent);
            label.alignment = TextAlignmentOptions.Center;
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

            // Ambient dark glass tint to balance vibrancy of illustration with text contrast
            var tintGo = new GameObject("Wallpaper Tint", typeof(RectTransform), typeof(Image));
            tintGo.transform.SetParent(viewport.transform, false);
            var tRect = (RectTransform)tintGo.transform;
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = tRect.offsetMax = Vector2.zero;
            var tintImg = tintGo.GetComponent<Image>();
            tintImg.color = new Color(0.012f, 0.020f, 0.038f, skin.wallpaperDim);
            tintImg.raycastTarget = false;
        }

        private void CreateTopOSBar()
        {
            var bar = CreateSlicedImage(screen, "OS Status Bar", new Vector2(1280, 26), skin != null ? skin.inset : null, InsetTint);
            bar.anchoredPosition = new Vector2(0, 368);

            var osLeft = CreateLabel(bar, "SYS // EXP-NET: ONLINE  *  SYNC 100%", 12, new Vector2(-460, 0), new Vector2(320, 20), CyanAccent);
            osLeft.alignment = TextAlignmentOptions.Left;

            osClockText = CreateLabel(bar, "SOL 42 - 14:38:00 EXP-PAD", 12, Vector2.zero, new Vector2(260, 20), MutedCyan);
            osClockText.alignment = TextAlignmentOptions.Center;

            var osRight = CreateLabel(bar, "[PWR: 98%]  [ANTENNA: NOMINAL]", 12, new Vector2(460, 0), new Vector2(300, 20), MutedCyan);
            osRight.alignment = TextAlignmentOptions.Right;
        }

        private void CreateWindowHeader(UnityAction close)
        {
            // Title terminal branding
            var titleLabel = CreateLabel(screen, "ORBIT // EXP-PAD 07  -  GUIA DE CAMPO", 16, new Vector2(-410, 332), new Vector2(380, 26), InkWhite);
            titleLabel.alignment = TextAlignmentOptions.Left;

            // Planet Selector Tabs
            var planets = catalog != null ? catalog.planets : null;
            if (planets != null)
            {
                planetTabButtons.Clear();
                planetTabLabels.Clear();

                float startX = -120f;
                float spacing = 150f;
                for (int i = 0; i < planets.Length; i++)
                {
                    int index = i;
                    string planetName = Clean(planets[i].name.ToUpperInvariant());
                    var (btn, lbl) = CreateTabButton(screen, planetName, new Vector2(startX + i * spacing, 332), new Vector2(138, 34),
                        () => SelectPlanet(index));
                    planetTabButtons.Add(btn);
                    planetTabLabels.Add(lbl);
                }
            }

            // Close Button [X]
            var closeBtn = CreateIconButton(screen, skin != null ? skin.closeIcon : null, "X",
                new Vector2(618, 332), new Vector2(34, 30), () => close());
        }

        private void CreateSubHeaderBanner()
        {
            var banner = CreateSlicedImage(screen, "SubHeader Banner", new Vector2(1280, 32), skin != null ? skin.inset : null, InsetTint);
            banner.anchoredPosition = new Vector2(0, 290);

            statusBannerLeft = CreateLabel(banner, "", 14, new Vector2(-360, 0), new Vector2(520, 24), CyanAccent);
            statusBannerLeft.alignment = TextAlignmentOptions.Left;

            statusBannerRight = CreateLabel(banner, "ESTADO: EN CURSO  -  PRIORIDAD 01", 12, new Vector2(400, 0), new Vector2(440, 24), MutedCyan);
            statusBannerRight.alignment = TextAlignmentOptions.Right;
        }

        private void CreateBottomNavigationDock()
        {
            var dock = CreateSlicedImage(screen, "Bottom Dock", new Vector2(760, 48), skin != null ? skin.inset : null, InsetTint);
            dock.anchoredPosition = new Vector2(0, -355);

            dockButtons.Clear();
            dockLabels.Clear();

            var sections = new[] { "Inicio", "Tareas", "Recetas", "Minerales" };
            float startX = -270f;
            float spacing = 180f;

            for (int i = 0; i < sections.Length; i++)
            {
                string sec = sections[i];
                var (btn, lbl) = CreateNavButton(dock, sec.ToUpperInvariant(), new Vector2(startX + i * spacing, 0), new Vector2(165, 36),
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

            // Update sub-header banner
            statusBannerLeft.text = $"ARCHIVO ORBITAL // {Clean(Planet.name.ToUpperInvariant())}  -  BASE ESPECTROMETRICA";

            if (page == "Inicio") RenderHome();
            else if (page == "Tareas") RenderTasks();
            else if (page == "Recetas") RenderRecipes();
            else if (page == "Minerales") RenderMinerals();
        }

        // ================= PAGE 1: INICIO (OVERVIEW DASHBOARD) =================
        private void RenderHome()
        {
            // Card 1: Misión Principal (Top-Left)
            var cardMission = CreateCard(workspace, "Card_Mision", new Vector2(-320, 125), new Vector2(600, 230), () => ShowTasks());
            CreateIcon(cardMission, skin != null ? skin.ship : null, new Vector2(-260, 85), new Vector2(24, 24), CyanAccent);
            var mTitle = CreateLabel(cardMission, $"MISION PRINCIPAL // {Clean(Planet.name.ToUpperInvariant())}", 16, new Vector2(20, 85), new Vector2(500, 24), CyanAccent);
            mTitle.alignment = TextAlignmentOptions.Left;

            var mInset = CreateSlicedImage(cardMission, "Mission_Inset", new Vector2(560, 105), skin != null ? skin.inset : null, InsetTint);
            mInset.anchoredPosition = new Vector2(0, 10);
            var mText = CreateLabel(mInset, Clean(Planet.objective), 16, Vector2.zero, new Vector2(530, 85), InkWhite);
            mText.alignment = TextAlignmentOptions.TopLeft;

            var (mBtn, _) = CreateActionButton(cardMission, "VER OBJETIVOS DETALLADOS  >", new Vector2(0, -78), new Vector2(560, 32), () => ShowTasks());

            // Card 2: Recetario Técnico (Top-Right)
            var cardRecipes = CreateCard(workspace, "Card_Recetas", new Vector2(320, 125), new Vector2(600, 230), () => ShowPage("Recetas"));
            CreateIcon(cardRecipes, skin != null ? skin.repair : null, new Vector2(-260, 85), new Vector2(24, 24), CyanAccent);
            var rTitle = CreateLabel(cardRecipes, "RECETARIO TECNICO DE COMPONENTES", 16, new Vector2(20, 85), new Vector2(500, 24), CyanAccent);
            rTitle.alignment = TextAlignmentOptions.Left;

            var rInset = CreateSlicedImage(cardRecipes, "Recipe_Inset", new Vector2(560, 105), skin != null ? skin.inset : null, InsetTint);
            rInset.anchoredPosition = new Vector2(0, 10);

            if (Planet.recipes != null && Planet.recipes.Length > 0)
            {
                var r0 = Planet.recipes[0];
                var cLabel = CreateLabel(rInset, $"[01] {Clean(r0.component).ToUpperInvariant()}", 16, new Vector2(0, 26), new Vector2(530, 24), InkWhite);
                cLabel.alignment = TextAlignmentOptions.Left;

                var ingLabel = CreateLabel(rInset, Ingredients(r0), 14, new Vector2(0, -18), new Vector2(530, 40), CyanAccent);
                ingLabel.alignment = TextAlignmentOptions.Left;
            }

            var (rBtn, _) = CreateActionButton(cardRecipes, $"{Planet.recipes.Length} RECETAS DISPONIBLES - ABRIR CATALOGO  >",
                new Vector2(0, -78), new Vector2(560, 32), () => ShowPage("Recetas"));

            // Card 3: Recursos Minerales (Bottom-Left)
            var cardMinerals = CreateCard(workspace, "Card_Minerales", new Vector2(-320, -135), new Vector2(600, 210), () => ShowMinerals());
            CreateIcon(cardMinerals, skin != null ? skin.mine : null, new Vector2(-260, 75), new Vector2(24, 24), CyanAccent);
            var minTitle = CreateLabel(cardMinerals, "RECURSOS Y MINERALES ANALIZADOS", 16, new Vector2(20, 75), new Vector2(500, 24), CyanAccent);
            minTitle.alignment = TextAlignmentOptions.Left;

            var minInset = CreateSlicedImage(cardMinerals, "Mineral_Inset", new Vector2(560, 85), skin != null ? skin.inset : null, InsetTint);
            minInset.anchoredPosition = new Vector2(0, 8);
            string minList = string.Join("   -   ", Array.ConvertAll(Planet.minerals, m => Clean(m).ToUpperInvariant()));
            var minText = CreateLabel(minInset, minList, 15, Vector2.zero, new Vector2(530, 60), InkWhite);
            minText.alignment = TextAlignmentOptions.Center;

            var (minBtn, _) = CreateActionButton(cardMinerals, "VER ESPECTROMETRIA  >", new Vector2(0, -68), new Vector2(560, 32), () => ShowMinerals());

            // Card 4: Protocolo de Misión (Bottom-Right)
            var cardTasks = CreateCard(workspace, "Card_Tareas", new Vector2(320, -135), new Vector2(600, 210), () => ShowTasks());
            CreateIcon(cardTasks, skin != null ? skin.objectives : null, new Vector2(-260, 75), new Vector2(24, 24), CyanAccent);
            var pTitle = CreateLabel(cardTasks, "PROTOCOLO OPERATIVO RECOMENDADO", 16, new Vector2(20, 75), new Vector2(500, 24), CyanAccent);
            pTitle.alignment = TextAlignmentOptions.Left;

            var pInset = CreateSlicedImage(cardTasks, "Protocol_Inset", new Vector2(560, 85), skin != null ? skin.inset : null, InsetTint);
            pInset.anchoredPosition = new Vector2(0, 8);
            var pText = CreateLabel(pInset, "01 RECOLECTAR MATERIALES  ->  02 REPARAR NAVE\n03 CHEQUEAR SISTEMAS Y DESPEGAR",
                14, Vector2.zero, new Vector2(530, 60), InkWhite);
            pText.alignment = TextAlignmentOptions.Center;

            var (pBtn, _) = CreateActionButton(cardTasks, "SEGUIMIENTO DE OBJETIVOS  >", new Vector2(0, -68), new Vector2(560, 32), () => ShowTasks());
        }

        // ================= PAGE 2: TAREAS (MISSION OBJECTIVES) =================
        private void RenderTasks()
        {
            var header = CreateLabel(workspace, $"SEGUIMIENTO DE PROTOCOLO  -  {Clean(Planet.name).ToUpperInvariant()}", 20,
                new Vector2(0, 210), new Vector2(1240, 32), CyanAccent);
            header.alignment = TextAlignmentOptions.Left;

            var steps = new[] {
                $"Reunir los materiales del sector de {Clean(Planet.name)}",
                "Consultar los planos tecnicos y reparar los componentes de la nave",
                "Completar la secuencia de chequeo y preparar la partida"
            };

            for (int i = 0; i < steps.Length; i++)
            {
                var row = CreateSlicedImage(workspace, $"Task_Row_{i}", new Vector2(1240, 95), skin != null ? skin.panel : null, PanelTint);
                row.anchoredPosition = new Vector2(0, 115 - i * 115);

                // Slot with check icon
                var slot = CreateSlicedImage(row, "Slot", new Vector2(44, 44), skin != null ? skin.slot : null, SlotTint);
                slot.anchoredPosition = new Vector2(-570, 0);
                if (skin != null && skin.checkIcon != null)
                {
                    CreateIcon(slot, skin.checkIcon, Vector2.zero, new Vector2(24, 24), CyanAccent);
                }

                // Step index
                var numLabel = CreateLabel(row, (i + 1).ToString("00"), 20, new Vector2(-510, 0), new Vector2(50, 40), CyanAccent);
                numLabel.alignment = TextAlignmentOptions.Center;

                // Step description
                var stepDesc = CreateLabel(row, steps[i], 16, new Vector2(40, 0), new Vector2(900, 60), InkWhite);
                stepDesc.alignment = TextAlignmentOptions.Left;

                // Status tag inset
                var tag = CreateSlicedImage(row, "Tag", new Vector2(130, 32), skin != null ? skin.inset : null, InsetTint);
                tag.anchoredPosition = new Vector2(535, 0);
                var tagLabel = CreateLabel(tag, i == 0 ? "[EN CURSO]" : "[PENDIENTE]", 12, Vector2.zero, new Vector2(120, 24), i == 0 ? CyanAccent : MutedCyan);
                tagLabel.alignment = TextAlignmentOptions.Center;
            }

            var note = CreateLabel(workspace, "ESTADO ORBITAL // ACTUALIZACION TELEMETRICA EN TIEMPO REAL", 12,
                new Vector2(0, -225), new Vector2(1240, 24), DimCyan);
            note.alignment = TextAlignmentOptions.Center;
        }

        // ================= PAGE 3: RECETAS (ENGINEERING BLUEPRINTS) =================
        private void RenderRecipes()
        {
            var header = CreateLabel(workspace, $"PLANOS TECNICOS DE REPARACION  -  {Clean(Planet.name).ToUpperInvariant()}", 20,
                new Vector2(0, 215), new Vector2(1240, 32), CyanAccent);
            header.alignment = TextAlignmentOptions.Left;

            for (int i = 0; i < Planet.recipes.Length; i++)
            {
                var recipe = Planet.recipes[i];
                var row = CreateSlicedImage(workspace, $"Recipe_{i}", new Vector2(1240, 100), skin != null ? skin.panel : null, PanelTint);
                row.anchoredPosition = new Vector2(0, 120 - i * 120);

                // Slot with repair gear icon
                var slot = CreateSlicedImage(row, "Slot", new Vector2(48, 48), skin != null ? skin.slot : null, SlotTint);
                slot.anchoredPosition = new Vector2(-565, 0);
                if (skin != null && skin.repair != null)
                {
                    CreateIcon(slot, skin.repair, Vector2.zero, new Vector2(26, 26), CyanAccent);
                }

                // Component index & name
                var nameLabel = CreateLabel(row, $"[0{i + 1}]  {Clean(recipe.component).ToUpperInvariant()}", 18,
                    new Vector2(-280, 0), new Vector2(460, 40), InkWhite);
                nameLabel.alignment = TextAlignmentOptions.Left;

                // Inset container with materials
                var ingInset = CreateSlicedImage(row, "Ing_Inset", new Vector2(530, 60), skin != null ? skin.inset : null, InsetTint);
                ingInset.anchoredPosition = new Vector2(250, 0);
                var ingLabel = CreateLabel(ingInset, Ingredients(recipe), 15, Vector2.zero, new Vector2(500, 48), CyanAccent);
                ingLabel.alignment = TextAlignmentOptions.Center;

                // Status tag
                var tag = CreateSlicedImage(row, "Tag", new Vector2(100, 32), skin != null ? skin.inset : null, InsetTint);
                tag.anchoredPosition = new Vector2(550, 0);
                var tagLabel = CreateLabel(tag, "[CRITICO]", 12, Vector2.zero, new Vector2(90, 24), new Color(1f, 0.45f, 0.35f));
                tagLabel.alignment = TextAlignmentOptions.Center;
            }

            var note = CreateLabel(workspace, "LOS COMPONENTES REQUIEREN SUMINISTROS ESPECIFICOS DEL SECTOR PLANETARIO", 12,
                new Vector2(0, -225), new Vector2(1240, 24), DimCyan);
            note.alignment = TextAlignmentOptions.Center;
        }

        // ================= PAGE 4: MINERALES (SPECTROMETRY CATALOG) =================
        private void RenderMinerals()
        {
            var header = CreateLabel(workspace, $"CATALOGO ESPECTROMETRICO DE RECURSOS  -  {Clean(Planet.name).ToUpperInvariant()}", 20,
                new Vector2(0, 215), new Vector2(1240, 32), CyanAccent);
            header.alignment = TextAlignmentOptions.Left;

            float cardWidth = 380f;
            float spacing = 410f;
            float startX = -(Planet.minerals.Length - 1) * 0.5f * spacing;

            for (int i = 0; i < Planet.minerals.Length; i++)
            {
                string mineral = Planet.minerals[i];
                var card = CreateSlicedImage(workspace, $"Mineral_{i}", new Vector2(cardWidth, 340), skin != null ? skin.panel : null, PanelTint);
                card.anchoredPosition = new Vector2(startX + i * spacing, 5);

                // Mineral slot / bracket container
                var bracket = CreateSlicedImage(card, "Bracket", new Vector2(130, 130), skin != null ? skin.bracket : null, SlotTint);
                bracket.anchoredPosition = new Vector2(0, 75);

                // Crystal visual shard
                var crystalGo = new GameObject("Crystal Shard", typeof(RectTransform), typeof(TabletCrystalIcon));
                crystalGo.transform.SetParent(bracket.transform, false);
                var cRect = (RectTransform)crystalGo.transform;
                cRect.sizeDelta = new Vector2(75, 95);
                cRect.anchoredPosition = Vector2.zero;
                var crystal = crystalGo.GetComponent<TabletCrystalIcon>();
                crystal.color = (i == 1 && Planet != null) ? Planet.accent : CyanAccent;
                crystal.raycastTarget = false;

                // Mineral Name
                var nameLabel = CreateLabel(card, Clean(mineral).ToUpperInvariant(), 20, new Vector2(0, -22), new Vector2(340, 30), InkWhite);
                nameLabel.alignment = TextAlignmentOptions.Center;

                // Inset stats box
                var statsInset = CreateSlicedImage(card, "Stats", new Vector2(340, 60), skin != null ? skin.inset : null, InsetTint);
                statsInset.anchoredPosition = new Vector2(0, -78);

                int usedCount = CountRecipes(mineral);
                var statsLabel = CreateLabel(statsInset, $"UTILIZADO EN {usedCount} RECETAS\nCOMPOSICION GEOLOGICA CLASE A", 13,
                    Vector2.zero, new Vector2(320, 50), CyanAccent);
                statsLabel.alignment = TextAlignmentOptions.Center;

                // Analysis tag
                var aTag = CreateSlicedImage(card, "AnalysisTag", new Vector2(260, 28), skin != null ? skin.inset : null, InsetTint);
                aTag.anchoredPosition = new Vector2(0, -135);
                var aLabel = CreateLabel(aTag, "ESPECTRO: ANALIZADO Y DISPONIBLE", 11, Vector2.zero, new Vector2(250, 22), MutedCyan);
                aLabel.alignment = TextAlignmentOptions.Center;
            }

            var note = CreateLabel(workspace, "BASE DE DATOS ESPECTROMETRICA ORBITAL  -  EXPEDICION MARCIANA", 12,
                new Vector2(0, -225), new Vector2(1240, 24), DimCyan);
            note.alignment = TextAlignmentOptions.Center;
        }

        // ================= UI HELPER BUILDERS (UGUI BEST PRACTICES) =================

        private RectTransform CreateCard(Transform parent, string name, Vector2 position, Vector2 size, UnityAction action)
        {
            var rect = CreateSlicedImage(parent, name, size, skin != null ? skin.panel : null, PanelTint);
            rect.anchoredPosition = position;
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

            var lbl = CreateLabel(rect, text, 14, Vector2.zero, size - new Vector2(8, 4), MutedCyan);
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

            // Physical tablet rise and tilt animation
            frame.anchoredPosition = new Vector2(0, Mathf.Lerp(-1000, 0, eased));
            frame.localScale = Vector3.one * Mathf.Lerp(0.70f, 1f, eased);
            frame.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-4, 0, eased));
        }
    }
}
