using System;
using System.Text;
using GameJam.Crafting;
using GameJam.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam.Tablet
{
    public sealed partial class TabletGuideUI
    {
        private TabletMissionTracker mission;
        private readonly System.Collections.Generic.List<Action> missionBindings = new System.Collections.Generic.List<Action>();
        private CanvasGroup missionToast;
        private TextMeshProUGUI missionToastText;
        private float missionToastUntil;
        private bool choseMissionPlanet;
        private static readonly Color CompleteColor = new Color(.4f, 1f, .68f);
        private bool ViewingLiveMission => mission != null && mission.IsLive && Planet != null
            && Planet.recipes != null && Planet.recipes.Length > 0 && Planet.recipes[0].source != null
            && Planet.recipes[0].source.planet == mission.Planet;
        private string MissionSummary => !ViewingLiveMission ? "CATALOGO · Sin misión activa en este planeta"
            : mission.RepairProgress >= 0f ? "INSTALANDO " + Mathf.FloorToInt(mission.RepairProgress * 100f) + "% · " + mission.InstalledCount + "/3 instaladas"
            : mission.InstalledCount == 3 ? "NAVE REPARADA · 3/3 componentes instalados"
            : mission.CraftedCount + "/3 fabricadas · " + mission.InstalledCount + "/3 instaladas";

        public void BindMission(MarsFusionAvatar owner)
        {
            mission = gameObject.AddComponent<TabletMissionTracker>();
            mission.Bind(owner);
            mission.Changed += RefreshMissionUI;
            mission.Activity += NotifyMissionActivity;
            RefreshMissionUI();
        }
        private void RefreshMissionUI()
        {
            if (!choseMissionPlanet && mission != null && mission.IsLive && catalog != null)
            {
                for (int i = 0; i < catalog.planets.Length; i++)
                {
                    var recipes = catalog.planets[i].recipes;
                    if (recipes != null && recipes.Length > 0 && recipes[0].source != null
                        && recipes[0].source.planet == mission.Planet)
                    {
                        choseMissionPlanet = true;
                        planetIndex = i;
                        RenderPage();
                        return;
                    }
                }
            }
            foreach (var binding in missionBindings) binding();
        }
        private void BindMissionLabel(Action update)
        {
            missionBindings.Add(update);
            update();
        }
        private RecipeData MissionRecipe(int index)
        {
            if (ViewingLiveMission) return mission.Table.GetRecipe(index);
            return Planet != null && Planet.recipes != null && index < Planet.recipes.Length
                ? Planet.recipes[index].source : null;
        }
        private bool HasRecipeSources()
        {
            if (Planet == null || Planet.recipes == null || Planet.recipes.Length == 0) return false;
            foreach (var recipe in Planet.recipes) if (recipe.source == null) return false;
            return true;
        }
        private string ComponentState(int index)
        {
            if (!ViewingLiveMission) return "RECETA DE REFERENCIA";
            return mission.IsInstalled(index) ? "[x] INSTALADO"
                : mission.IsCrafted(index) ? "FABRICADO · Instalar con Q" : "PENDIENTE DE FABRICAR";
        }

        private bool RenderMissionChecklist()
        {
            if (!HasRecipeSources()) return false;
            var summary = PageHeader("Checklist · " + Clean(Planet.name), "");
            BindMissionLabel(() => summary.text = MissionSummary);
            var content = AppList("b_Checklist_Vivo", Planet.recipes.Length, 110f);
            for (int i = 0; i < Planet.recipes.Length; i++)
            {
                int index = i;
                var recipe = MissionRecipe(index);
                var row = CreateRoundedPanel(content, "Checklist_" + recipe.id, new Vector2(1035, 96), 16, CardBg);
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1f);
                row.anchoredPosition = new Vector2(0, -50 - i * 110f);
                CreateItemSlot(row, recipe.icon, new Vector2(-460, 0), 68);
                var name = CreateLabel(row, Clean(recipe.recipeName), 22, new Vector2(-230, 0), new Vector2(340, 60), InkWhite);
                name.alignment = TextAlignmentOptions.Left;
                var made = CreateLabel(row, "", 17, new Vector2(120, 0), new Vector2(260, 60), MutedCyan);
                var installed = CreateLabel(row, "", 17, new Vector2(380, 0), new Vector2(240, 60), MutedCyan);
                BindMissionLabel(() =>
                {
                    bool live = ViewingLiveMission, a = live && mission.IsCrafted(index), b = live && mission.IsInstalled(index);
                    made.text = a ? "[x] FABRICADO" : "[ ] FABRICAR";
                    installed.text = b ? "[x] INSTALADO" : "[ ] INSTALAR";
                    made.color = a ? CompleteColor : MutedCyan;
                    installed.color = b ? CompleteColor : MutedCyan;
                });
            }
            PageNote("Progreso compartido del equipo · Fabricar en caja [E] · Instalar en nave [Q]");
            return true;
        }

        private bool RenderMissionRecipes()
        {
            if (!HasRecipeSources()) return false;
            var summary = PageHeader("Recetas · " + Clean(Planet.name), "");
            BindMissionLabel(() => summary.text = ViewingLiveMission ? MissionSummary + " · Cantidades de TU inventario" : MissionSummary);
            var content = AppList("b_Recetas_Visuales", Planet.recipes.Length, 116f);
            for (int i = 0; i < Planet.recipes.Length; i++)
            {
                int index = i;
                var recipe = MissionRecipe(index);
                var row = CreateRoundedPanel(content, "Recipe_" + recipe.id, new Vector2(1035, 110), 14, CardBg);
                row.anchorMin = row.anchorMax = new Vector2(.5f, 1f);
                row.anchoredPosition = new Vector2(0, -56 - i * 116f);
                for (int s = 0; s < recipe.ingredients.Count; s++)
                {
                    var ingredient = recipe.ingredients[s];
                    if (ingredient == null || ingredient.material == null) continue;
                    float x = -445 + s * 135;
                    var slot = CreateItemSlot(row, ingredient.material.icon, new Vector2(x, 8), 66);
                    var count = CreateLabel(slot, "", 16, new Vector2(13, -18), new Vector2(55, 22), InkWhite);
                    var mineral = CreateLabel(row, Clean(ingredient.material.materialName), 12, new Vector2(x, -39), new Vector2(126, 28), MutedCyan);
                    BindMissionLabel(() =>
                    {
                        int amount = ViewingLiveMission ? mission.Amount(ingredient.material) : 0;
                        count.text = ViewingLiveMission ? amount + "/" + ingredient.amount : "x" + ingredient.amount;
                        count.color = !ViewingLiveMission ? InkWhite : amount >= ingredient.amount ? CompleteColor : new Color(1f, .55f, .45f);
                    });
                }
                CreateLabel(row, ">", 36, new Vector2(80, 0), new Vector2(60, 70), CyanAccent);
                CreateItemSlot(row, recipe.icon, new Vector2(170, 0), 76);
                var name = CreateLabel(row, Clean(recipe.recipeName), 21, new Vector2(355, 19), new Vector2(270, 40), InkWhite);
                var status = CreateLabel(row, "", 14, new Vector2(355, -24), new Vector2(270, 38), MutedCyan);
                BindMissionLabel(() =>
                {
                    status.text = ComponentState(index);
                    status.color = ViewingLiveMission && mission.IsInstalled(index) ? CompleteColor : MutedCyan;
                });
            }
            PageNote("Ingredientes > Componente · Usa la caja de crafteo [E] para fabricar; la tablet muestra el estado.");
            return true;
        }
        private RectTransform CreateItemSlot(Transform parent, Sprite sprite, Vector2 position, float size)
        {
            var border = CreateRoundedPanel(parent, "Item slot", new Vector2(size, size), 7, new Color(.12f,.2f,.28f));
            border.anchoredPosition = position;
            var inset = CreateRoundedPanel(border, "Slot background", new Vector2(size - 6, size - 6), 5, InsetBg);
            if (sprite != null) CreateIcon(inset, sprite, Vector2.zero, new Vector2(size - 14, size - 14), Color.white);
            else CreateLabel(inset, "?", 25, Vector2.zero, new Vector2(size - 10, size - 10), MutedCyan);
            return border;
        }
        private void RenderMissionJournal()
        {
            var summary = PageHeader("Registro de misión", "");
            BindMissionLabel(() => summary.text = MissionSummary);
            var content = AppList("b_Registro", 1, 350f);
            var log = CreateLabel(content, "", 18, Vector2.zero, new Vector2(1020, 350), InkWhite);
            var rect = (RectTransform)log.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -12);
            log.alignment = TextAlignmentOptions.TopLeft;
            BindMissionLabel(() =>
            {
                var text = new StringBuilder();
                if (mission == null || mission.Journal.Count == 0) text.Append("Sin actividad registrada en esta misión.");
                else for (int i = mission.Journal.Count - 1; i >= 0; i--)
                    text.Append("[").Append((i + 1).ToString("00")).Append("]  ").Append(Clean(mission.Journal[i])).Append("\n\n");
                log.text = text.ToString();
                float height = Mathf.Max(350, (mission != null ? mission.Journal.Count : 0) * 64f);
                rect.sizeDelta = new Vector2(1020, height);
                content.sizeDelta = new Vector2(0, height + 24f);
            });
            PageNote("Recogidas, fabricación e instalaciones · Actualización automática desde el estado de red.");
        }
        private void BuildMissionFeedback()
        {
            var panel = CreateRoundedPanel(transform, "Tablet mission notification", new Vector2(650, 64), 10, CardBg);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f);
            panel.anchoredPosition = new Vector2(0, -100);
            missionToast = panel.gameObject.AddComponent<CanvasGroup>();
            missionToast.alpha = 0;
            missionToast.blocksRaycasts = false;
            missionToastText = CreateLabel(panel, "", 18, Vector2.zero, new Vector2(625, 54), CompleteColor);
        }
        private void NotifyMissionActivity(string message)
        {
            if (missionToastText != null) missionToastText.text = "TABLET · " + Clean(message) + "\n[TAB] Ver progreso de misión";
            missionToastUntil = Time.unscaledTime + 4f;
        }
        private void UpdateMissionFeedback()
        {
            if (missionToast == null) return;
            bool alive = mission != null && mission.Owner != null && mission.Owner.Object != null
                && mission.Owner.Object.IsValid && mission.Owner.IsAlive;
            float target = alive && !IsVisible && Time.unscaledTime < missionToastUntil ? 1f : 0f;
            missionToast.alpha = Mathf.MoveTowards(missionToast.alpha, target, Time.unscaledDeltaTime * 8f);
        }
        private void OnDestroy()
        {
            if (mission == null) return;
            mission.Changed -= RefreshMissionUI;
            mission.Activity -= NotifyMissionActivity;
        }
    }
}

