using System;
using GameJam.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PlayerInventory = GameJam.Inventory.Inventory;

namespace GameJam.Crafting
{
    /// <summary>
    /// Interfaz de crafteo estilo Minecraft creada por codigo: por cada receta muestra
    /// los ingredientes en casillas, una flecha y la casilla del resultado.
    /// </summary>
    public sealed class CraftingBoxUI : MonoBehaviour
    {
        private const int RecipeCount = 3;
        private const int MaxIngredients = 4;

        private static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.16f, 0.94f);
        private static readonly Color SlotColor = new Color(0.2f, 0.24f, 0.32f, 1f);
        private static readonly Color SlotBorder = new Color(0.05f, 0.06f, 0.09f, 1f);
        private static readonly Color Accent = new Color(0.35f, 0.85f, 1f);
        private static readonly Color Missing = new Color(1f, 0.4f, 0.35f);
        private static readonly Color Enough = new Color(0.5f, 1f, 0.55f);

        private Font font;
        private GameObject prompt;
        private GameObject panel;
        private Text flashText;
        private float flashUntil;
        private Action<int> onCraft;
        private readonly RecipeRow[] rows = new RecipeRow[RecipeCount];

        private sealed class RecipeRow
        {
            public GameObject root;
            public Image[] slotIcons = new Image[MaxIngredients];
            public Text[] slotCounts = new Text[MaxIngredients];
            public GameObject[] slots = new GameObject[MaxIngredients];
            public Image resultIcon;
            public Text resultName;
            public Text description;
            public Button button;
            public Text buttonText;
        }

        /// <summary>
        /// Construye el lienzo, el aviso de interaccion y el panel oculto.
        /// </summary>
        public static CraftingBoxUI Create(string title, Action<int> craft, Action close)
        {
            GameObject go = new GameObject("CajaCrafteo_UI");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            CraftingBoxUI ui = go.AddComponent<CraftingBoxUI>();
            ui.Build(title, craft, close);
            return ui;
        }

        public void SetPromptVisible(bool visible)
        {
            if (prompt.activeSelf != visible)
            {
                prompt.SetActive(visible);
            }
        }

        public void SetPanelVisible(bool visible)
        {
            if (panel.activeSelf != visible)
            {
                panel.SetActive(visible);
            }
        }

        public void Flash(string message)
        {
            flashText.text = message;
            flashUntil = Time.time + 1.5f;
        }

        /// <summary>
        /// Actualiza casillas, cantidades y botones con el inventario del jugador local.
        /// </summary>
        public void Refresh(CraftingTable table, PlayerInventory inventory)
        {
            flashText.enabled = Time.time < flashUntil;
            if (!panel.activeSelf)
            {
                return;
            }

            for (int i = 0; i < RecipeCount; i++)
            {
                RecipeRow row = rows[i];
                RecipeData recipe = table.GetRecipe(i);
                row.root.SetActive(recipe != null);
                if (recipe == null)
                {
                    continue;
                }

                bool enough = true;
                for (int s = 0; s < MaxIngredients; s++)
                {
                    bool used = recipe.ingredients != null && s < recipe.ingredients.Count && recipe.ingredients[s] != null;
                    row.slots[s].SetActive(used);
                    if (!used)
                    {
                        continue;
                    }

                    RecipeIngredient ingredient = recipe.ingredients[s];
                    MaterialData material = ingredient.material;
                    int have = inventory != null && material != null ? inventory.GetMaterialAmount(material) : 0;
                    enough &= have >= ingredient.amount;
                    row.slotIcons[s].sprite = material != null ? material.icon : null;
                    row.slotIcons[s].enabled = row.slotIcons[s].sprite != null;
                    row.slotCounts[s].text = (material != null && material.icon == null ? material.materialName + "\n" : "") + have + "/" + ingredient.amount;
                    row.slotCounts[s].color = have >= ingredient.amount ? Enough : Missing;
                }

                bool done = table.IsComponentCompleted(i);
                row.resultIcon.sprite = recipe.icon;
                row.resultIcon.enabled = recipe.icon != null;
                row.resultName.text = recipe.recipeName;
                row.description.text = recipe.description;
                row.button.interactable = enough && !done;
                row.buttonText.text = done ? "Fabricado" : enough ? "Fabricar" : "Faltan materiales";
            }
        }

        private void Build(string title, Action<int> craft, Action close)
        {
            onCraft = craft;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Aviso inferior "[E] Abrir".
            prompt = Box(transform, "Aviso", new Color(0f, 0f, 0f, 0.6f));
            Place(prompt, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(420f, 60f));
            Label(prompt.transform, "[E]  Abrir caja de crafteo", 26, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(420f, 60f));
            prompt.SetActive(false);

            panel = Box(transform, "Panel", PanelColor);
            Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f));
            Outline(panel, Accent, 3f);
            Label(panel.transform, title, 40, Accent, TextAnchor.MiddleCenter, new Vector2(0f, 270f), new Vector2(800f, 60f));
            Label(panel.transform, "Junta los materiales y fabrica las piezas de la nave", 20, new Color(0.8f, 0.85f, 0.9f), TextAnchor.MiddleCenter, new Vector2(0f, 228f), new Vector2(800f, 30f));

            for (int i = 0; i < RecipeCount; i++)
            {
                rows[i] = BuildRow(panel.transform, i, new Vector2(0f, 120f - i * 150f));
            }

            flashText = Label(panel.transform, "", 24, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, -275f), new Vector2(800f, 36f));
            flashText.enabled = false;

            GameObject closeButton = Box(panel.transform, "Cerrar", new Color(0.6f, 0.15f, 0.15f, 1f));
            Place(closeButton, new Vector2(0.5f, 0.5f), new Vector2(400f, 280f), new Vector2(56f, 56f));
            Label(closeButton.transform, "X", 30, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(56f, 56f));
            closeButton.AddComponent<Button>().onClick.AddListener(() => close());

            Label(panel.transform, "[E] o [Esc] para cerrar", 18, new Color(0.7f, 0.75f, 0.8f), TextAnchor.MiddleRight, new Vector2(300f, -300f), new Vector2(260f, 30f));
            panel.SetActive(false);
        }

        private RecipeRow BuildRow(Transform parent, int index, Vector2 position)
        {
            RecipeRow row = new RecipeRow();
            row.root = Box(parent, "Receta_" + index, new Color(1f, 1f, 1f, 0.04f));
            Place(row.root, new Vector2(0.5f, 0.5f), position, new Vector2(840f, 135f));

            for (int s = 0; s < MaxIngredients; s++)
            {
                GameObject slot = Slot(row.root.transform, new Vector2(-370f + s * 95f, 10f), out Image icon);
                row.slots[s] = slot;
                row.slotIcons[s] = icon;
                row.slotCounts[s] = Label(slot.transform, "", 16, Color.white, TextAnchor.LowerRight, new Vector2(-4f, 2f), new Vector2(80f, 80f));
            }

            Label(row.root.transform, "→", 56, Accent, TextAnchor.MiddleCenter, new Vector2(18f, 10f), new Vector2(60f, 80f));
            GameObject result = Slot(row.root.transform, new Vector2(100f, 10f), out row.resultIcon);
            Outline(result, Accent, 2f);

            row.resultName = Label(row.root.transform, "", 24, Color.white, TextAnchor.UpperLeft, new Vector2(275f, 30f), new Vector2(250f, 34f));
            row.description = Label(row.root.transform, "", 15, new Color(0.75f, 0.8f, 0.85f), TextAnchor.UpperLeft, new Vector2(275f, -8f), new Vector2(250f, 50f));

            GameObject button = Box(row.root.transform, "Fabricar", new Color(0.15f, 0.55f, 0.75f, 1f));
            Place(button, new Vector2(0.5f, 0.5f), new Vector2(275f, -45f), new Vector2(250f, 38f));
            row.button = button.AddComponent<Button>();
            ColorBlock colors = row.button.colors;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.4f, 1f);
            row.button.colors = colors;
            row.buttonText = Label(button.transform, "Fabricar", 20, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(250f, 38f));
            int captured = index;
            row.button.onClick.AddListener(() => onCraft?.Invoke(captured));
            return row;
        }

        private GameObject Slot(Transform parent, Vector2 position, out Image icon)
        {
            GameObject border = Box(parent, "Casilla", SlotBorder);
            Place(border, new Vector2(0.5f, 0.5f), position, new Vector2(84f, 84f));
            GameObject inner = Box(border.transform, "Fondo", SlotColor);
            Place(inner, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
            GameObject iconGo = new GameObject("Icono", typeof(RectTransform));
            iconGo.transform.SetParent(inner.transform, false);
            icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Place(iconGo, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            return border;
        }

        private static GameObject Box(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static void Place(GameObject go, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Outline(GameObject go, Color color, float width)
        {
            UnityEngine.UI.Outline outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
        }

        private Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor, Vector2 position, Vector2 box)
        {
            GameObject go = new GameObject("Texto", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            Place(go, new Vector2(0.5f, 0.5f), position, box);
            return label;
        }
    }
}
