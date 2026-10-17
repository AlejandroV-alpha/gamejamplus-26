using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace GameJam.Tablet
{
    public sealed class TabletGuideUI : MonoBehaviour
    {
        private RectTransform frame, screen, workspace;
        private CanvasGroup overlay;
        private TabletRecipeCatalog catalog;
        private TabletUISkin skin;
        private Text planetLabel;
        private readonly List<Graphic> planetTabs = new();
        private readonly List<Graphic> navigation = new();
        private float progress;
        private int planetIndex = 1;
        private string page = "Inicio";
        private static Font font;
        private static readonly Color Ink = new(0.83f, 0.95f, 0.98f);
        private static readonly Color Muted = new(0.42f, 0.71f, 0.78f);
        private static readonly Color White = new(0.995f, 0.99f, 0.975f);
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
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var hint = Panel(transform, "Equipment hint", new Vector2(275, 42), new Color(0.10f, 0.12f, 0.15f, 0.85f), 15);
            hint.anchorMin = hint.anchorMax = new Vector2(1, 0);
            hint.anchoredPosition = new Vector2(-165, 32);
            Text(hint, "01  TABLET EQUIPADA    [TAB]", 16, Vector2.zero, new Vector2(245, 25), Color.white);
            var root = new GameObject("Local Tablet Overlay", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay = root.AddComponent<CanvasGroup>();
            overlay.alpha = 0; overlay.blocksRaycasts = false;
            Panel(root.transform, "World shade", new Vector2(5000, 5000), new Color(0.025f, 0.035f, 0.06f, 0.64f), 0);
            frame = Panel(root.transform, "Tablet outer shell", new Vector2(1320, 790), new Color(0.18f, 0.20f, 0.23f), 48);
            Panel(frame, "Inner bezel", new Vector2(1305, 775), new Color(0.045f, 0.06f, 0.075f), 42);
            screen = Panel(frame, "Display", new Vector2(1262, 730), new Color(0.91f, 0.92f, 0.955f), 28);
            AddWallpaper();
            Panel(frame, "Front camera", new Vector2(7, 7), new Color(0.29f, 0.34f, 0.39f), 4).anchoredPosition = new Vector2(-644, 0);
            Text(screen, "ORBIT  /  GUÍA DE CAMPO", 17, new Vector2(-385, 332), new Vector2(350, 28), Muted);
            Text(screen, "ENLACE LOCAL", 14, new Vector2(360, 332), new Vector2(150, 28), Muted);
            Button(screen, "×", new Vector2(560, 331), new Vector2(46, 36), () => close(), new Color(0.83f, 0.85f, 0.90f), Ink, 24);
            Text(screen, "Bitácora de expedición", 34, new Vector2(-165, 277), new Vector2(800, 48), Ink, true);
            planetLabel = Text(screen, "", 16, new Vector2(350, 276), new Vector2(410, 35), Muted);
            var planets = catalog != null ? catalog.planets : null;
            if (planets != null)
                for (int i = 0; i < planets.Length; i++)
                {
                    int index = i;
                    var planetTab = Button(screen, planets[i].name.ToUpperInvariant(), new Vector2(-438 + i * 181, 221), new Vector2(165, 40),
                        () => SelectPlanet(index), Color.white, Ink, 16);
                    planetTabs.Add(planetTab.GetComponent<Graphic>());
                }
            workspace = Panel(screen, "Widget workspace", new Vector2(1150, 480), Color.clear, 0);
            workspace.anchoredPosition = new Vector2(0, -40);
            workspace.GetComponent<Graphic>().raycastTarget = false;
            var dock = Panel(screen, "Widget navigation", new Vector2(650, 52), new Color(0.13f, 0.17f, 0.23f), 22);
            dock.anchoredPosition = new Vector2(0, -323);
            var sections = new[] { "Inicio", "Tareas", "Recetas", "Minerales" };
            for (int i = 0; i < sections.Length; i++)
            {
                string section = sections[i];
                var nav = Button(dock, section, new Vector2(-240 + i * 160, 0), new Vector2(150, 40), () => ShowPage(section),
                    new Color(0.13f, 0.17f, 0.23f), Color.white, 18);
                navigation.Add(nav.GetComponent<Graphic>());
            }
            Panel(frame, "Home indicator", new Vector2(110, 4), new Color(0.47f, 0.50f, 0.54f), 2).anchoredPosition = new Vector2(0, -382);
            RenderPage();
        }

        public void ShowTasks() => ShowPage("Tareas");
        public void ShowMinerals() => ShowPage("Minerales");
        public void SelectPlanet(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.planets.Length) return;
            planetIndex = index;
            RenderPage();
        }
        public void ShowPage(string section) { page = section; RenderPage(); }

        private void RenderPage()
        {
            for (int i = workspace.childCount - 1; i >= 0; i--)
            {
                var child = workspace.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            if (Planet == null)
            {
                Text(workspace, "No hay un catálogo planetario asignado.", 26, Vector2.zero, new Vector2(900, 70), Ink);
                return;
            }
            for (int i = 0; i < planetTabs.Count; i++)
                SetSelection(planetTabs[i], i == planetIndex);
            var sections = new[] { "Inicio", "Tareas", "Recetas", "Minerales" };
            for (int i = 0; i < navigation.Count; i++)
                SetSelection(navigation[i], sections[i] == page);
            planetLabel.text = "ARCHIVO PLANETARIO  /  " + Planet.name.ToUpperInvariant();
            if (page == "Inicio") Home();
            else if (page == "Recetas") Recipes();
            else if (page == "Minerales") Minerals();
            else Tasks();
        }

        private void Home()
        {
            var mission = Card("Misión", new Vector2(-325, 76), new Vector2(440, 282), Color.Lerp(Planet.accent, White, 0.75f), () => ShowTasks());
            Icon(mission, skin != null ? skin.ship : null, new Vector2(168, 100));
            Text(mission, "MISIÓN  /  " + Planet.name.ToUpperInvariant(), 16, new Vector2(-35, 103), new Vector2(320, 28), Ink, true);
            Text(mission, Planet.objective, 29, new Vector2(-35, 23), new Vector2(320, 115), Ink, true);
            Text(mission, "Ver objetivos  →", 18, new Vector2(-35, -104), new Vector2(320, 30), Ink);
            Crystal(mission, new Vector2(156, -59), new Vector2(70, 88), Planet.accent);
            var recipes = Card("Recetario", new Vector2(230, 76), new Vector2(630, 282), White, () => ShowPage("Recetas"));
            Icon(recipes, skin != null ? skin.repair : null, new Vector2(270, 104));
            Text(recipes, "RECETARIO", 16, new Vector2(-30, 103), new Vector2(500, 28), Muted, true);
            Text(recipes, Planet.recipes[0].component, 29, new Vector2(-45, 42), new Vector2(470, 55), Ink, true);
            Text(recipes, Ingredients(Planet.recipes[0]), 22, new Vector2(-45, -23), new Vector2(470, 65), Ink);
            Text(recipes, Planet.recipes.Length + " recetas de reparación  ·  Abrir →", 18, new Vector2(-30, -104), new Vector2(500, 30), Muted);
            Crystal(recipes, new Vector2(246, 31), new Vector2(76, 105), Planet.accent);
            var minerals = Card("Minerales conocidos", new Vector2(-325, -154), new Vector2(440, 144), White, () => ShowMinerals());
            Icon(minerals, skin != null ? skin.mine : null, new Vector2(188, 43));
            Text(minerals, "MINERALES CONOCIDOS", 16, new Vector2(-10, 43), new Vector2(370, 28), Muted, true);
            Text(minerals, string.Join("  ·  ", Planet.minerals), 21, new Vector2(-10, -13), new Vector2(370, 70), Ink);
            var tasks = Card("Objetivos", new Vector2(230, -154), new Vector2(630, 144), new Color(0.83f, 0.87f, 0.92f), () => ShowTasks());
            Icon(tasks, skin != null ? skin.objectives : null, new Vector2(272, 43));
            Text(tasks, "RUTA DE MISIÓN", 16, new Vector2(-20, 43), new Vector2(530, 28), Muted, true);
            Text(tasks, "01  Reunir materiales     02  Reparar componentes\n03  Preparar la nave para continuar", 20, new Vector2(-20, -14), new Vector2(530, 65), Ink);
        }

        private void Recipes()
        {
            Text(workspace, "Recetas de " + Planet.name, 29, new Vector2(-50, 195), new Vector2(980, 48), Ink, true);
            Text(workspace, Planet.objective, 18, new Vector2(-50, 154), new Vector2(980, 32), Muted);
            for (int i = 0; i < Planet.recipes.Length; i++)
            {
                var recipe = Planet.recipes[i];
                var row = Card(recipe.component, new Vector2(0, 68 - i * 105), new Vector2(1090, 92), White, null);
                Text(row, (i + 1).ToString("00"), 18, new Vector2(-482, 0), new Vector2(60, 40), Muted);
                Text(row, recipe.component, 23, new Vector2(-260, 0), new Vector2(350, 70), Ink, true);
                Text(row, Ingredients(recipe), 21, new Vector2(225, 0), new Vector2(570, 70), Ink);
            }
            Text(workspace, "Consulta de recetas · Los materiales dependen del planeta seleccionado", 15,
                new Vector2(0, -219), new Vector2(1090, 26), Muted);
        }

        private void Minerals()
        {
            Text(workspace, "Minerales conocidos · " + Planet.name, 29, new Vector2(0, 189), new Vector2(1090, 50), Ink, true);
            for (int i = 0; i < Planet.minerals.Length; i++)
            {
                var card = Card(Planet.minerals[i], new Vector2(-371 + i * 371, -6), new Vector2(345, 302), White, null);
                Crystal(card, new Vector2(0, 54), new Vector2(100, 134), i == 1 ? Planet.accent : new Color(0.44f, 0.51f, 0.61f));
                Text(card, Planet.minerals[i], 23, new Vector2(0, -52), new Vector2(290, 65), Ink, true);
                Text(card, "Usado en " + CountRecipes(Planet.minerals[i]) + " recetas", 17, new Vector2(0, -113), new Vector2(290, 32), Muted);
            }
            Text(workspace, "Catálogo de materiales · No representa cantidades disponibles en la mochila", 15,
                new Vector2(0, -219), new Vector2(1090, 26), Muted);
        }

        private void Tasks()
        {
            Text(workspace, Planet.objective, 29, new Vector2(0, 189), new Vector2(1090, 60), Ink, true);
            var steps = new[] { "Reunir los materiales de " + Planet.name, "Consultar las recetas y reparar los componentes", "Preparar la nave para continuar el viaje" };
            for (int i = 0; i < steps.Length; i++)
            {
                var row = Card("Objetivo " + i, new Vector2(0, 78 - i * 100), new Vector2(1090, 84), White, null);
                Text(row, (i + 1).ToString("00"), 24, new Vector2(-477, 0), new Vector2(65, 40), Planet.accent, true);
                Text(row, steps[i], 23, new Vector2(25, 0), new Vector2(890, 60), Ink);
            }
            Text(workspace, "Guía de objetivos · Seguimiento de misión pendiente", 15, new Vector2(0, -219), new Vector2(1090, 26), Muted);
        }

        private int CountRecipes(string mineral)
        {
            int count = 0;
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
                if (result.Length > 0) result.Append("  +  ");
                result.Append(ingredient.amount).Append(" × ").Append(ingredient.mineral);
            }
            return result.ToString();
        }
        private void Update()
        {
            progress = Mathf.MoveTowards(progress, TargetOpen ? 1 : 0, Time.unscaledDeltaTime / 0.32f);
            float eased = progress * progress * (3 - 2 * progress);
            overlay.alpha = eased; overlay.blocksRaycasts = progress > 0;
            overlay.interactable = TargetOpen && progress > 0.95f;
            frame.anchoredPosition = new Vector2(0, Mathf.Lerp(-950, 0, eased));
            frame.localScale = Vector3.one * Mathf.Lerp(0.5f, 1, eased);
            frame.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-8, 0, eased));
        }
        private RectTransform Card(string name, Vector2 position, Vector2 size, Color color, UnityAction action)
        {
            var rect = Panel(workspace, name, size, color, 23);
            rect.anchoredPosition = position;
            if (skin != null && rect.TryGetComponent<Image>(out var background))
                background.color = new Color(1, 1, 1, skin.widgetOpacity);
            if (skin != null)
            {
                var backing = Panel(rect, "Widget readability backing", size - new Vector2(10, 10),
                    new Color(0.018f, 0.035f, 0.055f, skin.widgetOpacity * 0.85f), 0);
                backing.GetComponent<Graphic>().raycastTarget = false;
                var accent = Panel(rect, "Planet accent", new Vector2(3, size.y - 28), Planet.accent, 0);
                accent.anchoredPosition = new Vector2(-size.x * 0.5f + 12, 0);
                accent.GetComponent<Graphic>().raycastTarget = false;
            }
            if (action != null) AddButton(rect, action);
            return rect;
        }
        private RectTransform Panel(Transform parent, string name, Vector2 size, Color color, float radius)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.sizeDelta = size;
            bool hardware = name == "Tablet outer shell" || name == "Inner bezel";
            if (skin != null && radius >= 15 && color.a > 0 && !hardware)
            {
                var image = go.AddComponent<Image>();
                image.sprite = name == "Display" || name == "Widget navigation" ? skin.inset : skin.panel;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.42f;
                image.color = Color.white;
            }
            else
            {
                var panel = go.AddComponent<TabletRoundedPanel>(); panel.radius = radius; panel.color = color;
            }
            return rect;
        }
        private static Text Text(Transform parent, string value, int fontSize, Vector2 position, Vector2 size, Color color, bool bold = false)
        {
            var go = new GameObject(value, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
            var text = go.AddComponent<Text>(); text.font = font; text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.color = color; text.text = value;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            return text;
        }
        private RectTransform Button(Transform parent, string value, Vector2 position, Vector2 size, UnityAction action, Color bg, Color fg, int fontSize)
        {
            var rect = Panel(parent, value, size, bg, 15); rect.anchoredPosition = position;
            if (skin != null && rect.TryGetComponent<Image>(out var image)) image.sprite = skin.button;
            AddButton(rect, action);
            Text(rect, value, fontSize, Vector2.zero, size - new Vector2(20, 0), fg).alignment = TextAnchor.MiddleCenter;
            return rect;
        }
        private void AddButton(RectTransform rect, UnityAction action)
        {
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Graphic>();
            var colors = button.colors; colors.highlightedColor = new Color(0.92f, 0.94f, 1); colors.pressedColor = new Color(0.8f, 0.85f, 0.94f);
            button.colors = colors;
            if (skin != null && rect.GetComponent<Image>() != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { highlightedSprite = skin.hover, pressedSprite = skin.pressed, selectedSprite = skin.selectedTab };
            }
            button.onClick.AddListener(action);
        }
        private void SetSelection(Graphic graphic, bool selected)
        {
            if (skin != null && graphic is Image image)
            {
                image.sprite = selected ? skin.selectedTab : skin.tab;
                image.color = Color.white;
            }
            else graphic.color = selected ? new Color(0.30f, 0.36f, 0.46f) : new Color(0.13f, 0.17f, 0.23f);
        }
        private void AddWallpaper()
        {
            if (skin == null || skin.wallpaper == null) return;
            var viewport = new GameObject("Wallpaper viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(screen, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(5, 5); viewportRect.offsetMax = new Vector2(-5, -5);
            var picture = new GameObject("Wallpaper", typeof(RectTransform), typeof(Image));
            picture.transform.SetParent(viewport.transform, false);
            var image = picture.GetComponent<Image>();
            image.sprite = skin.wallpaper; image.raycastTarget = false;
            var fit = picture.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = skin.wallpaper.rect.width / skin.wallpaper.rect.height;
            var shade = new GameObject("Wallpaper readability shade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(viewport.transform, false);
            var rect = (RectTransform)shade.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.055f, skin.wallpaperDim);
            shade.GetComponent<Image>().raycastTarget = false;
            var header = Panel(screen, "Header contrast", new Vector2(1252, 180), new Color(0.015f, 0.025f, 0.055f, 0.40f), 0);
            header.anchoredPosition = new Vector2(0, 270);
            header.GetComponent<Graphic>().raycastTarget = false;
        }
        private static void Icon(Transform parent, Sprite sprite, Vector2 position)
        {
            if (sprite == null) return;
            var go = new GameObject("Kit icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(26, 26); rect.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.sprite = sprite;
            image.color = new Color(0.35f, 0.87f, 0.96f); image.preserveAspect = true; image.raycastTarget = false;
        }
        private static void Crystal(Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject("Mineral crystal", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.sizeDelta = size; rect.anchoredPosition = position;
            var icon = go.AddComponent<TabletCrystalIcon>(); icon.color = color; icon.raycastTarget = false;
        }
    }
}
