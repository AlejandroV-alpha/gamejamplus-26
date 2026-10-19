using System;
using System.Collections.Generic;
using GameJam.Crafting;
using GameJam.Inventory;
using GameJam.Multiplayer;
using GameJam.Ship;
using UnityEngine;

namespace GameJam.Tablet
{
    // Reads replicated gameplay state; never infer completion by parsing Debug.Log.
    public sealed class TabletMissionTracker : MonoBehaviour
    {
        public event Action Changed;
        public event Action<string> Activity;
        public MarsFusionAvatar Owner { get; private set; }
        public CraftingTable Table { get; private set; }
        public ShipRepairStation Station { get; private set; }
        public CraftingPlanet Planet { get; private set; }
        public bool IsLive => Table != null && Table.Object != null && Table.Object.IsValid;
        public int CraftedCount { get; private set; }
        public int InstalledCount { get; private set; }
        public float RepairProgress { get; private set; } = -1f;
        public string LatestActivity { get; private set; } = "Esperando datos de misión";
        public readonly List<string> Journal = new List<string>();
        private readonly bool[] crafted = new bool[3], installed = new bool[3];
        private readonly Dictionary<MaterialData, int> amounts = new Dictionary<MaterialData, int>();
        private GameJam.Inventory.Inventory inventory;
        private float nextDiscovery;
        private bool initialized;

        public void Bind(MarsFusionAvatar owner)
        {
            Owner = owner;
            inventory = owner != null ? owner.GetComponent<GameJam.Inventory.Inventory>() : null;
            RefreshNow();
        }
        public bool IsCrafted(int index) => index >= 0 && index < 3 && crafted[index];
        public bool IsInstalled(int index) => index >= 0 && index < 3 && installed[index];
        public int Amount(MaterialData material) => material != null && amounts.TryGetValue(material, out var amount) ? amount : 0;
        private void LateUpdate() => RefreshNow();

        public void RefreshNow()
        {
            if (Owner == null || Owner.Object == null || !Owner.Object.IsValid || Owner.Runner == null) return;
            bool changed = false;
            if (Time.unscaledTime >= nextDiscovery || (Table != null && !IsLive))
            {
                nextDiscovery = Time.unscaledTime + .5f;
                CraftingTable found = null;
                foreach (var table in FindObjectsByType<CraftingTable>(FindObjectsSortMode.None))
                    if (table.Object != null && table.Object.IsValid && table.Runner == Owner.Runner
                        && table.gameObject.scene == Owner.gameObject.scene) { found = table; break; }
                if (Table != found) { Table = found; initialized = false; amounts.Clear(); changed = true; }
                Station = null;
                if (Table != null)
                {
                    Planet = Table.GetPlanet();
                    foreach (var station in FindObjectsByType<ShipRepairStation>(FindObjectsSortMode.None))
                        if (station.Object != null && station.Object.IsValid && station.Runner == Owner.Runner
                            && station.CraftingTable == Table) { Station = station; break; }
                }
            }
            if (!IsLive)
            {
                if (CraftedCount != 0 || InstalledCount != 0 || RepairProgress >= 0f) changed = true;
                CraftedCount = InstalledCount = 0;
                Array.Clear(crafted, 0, crafted.Length); Array.Clear(installed, 0, installed.Length);
                RepairProgress = -1f;
                if (changed) Changed?.Invoke();
                return;
            }
            int previousInstalled = InstalledCount;
            CraftedCount = InstalledCount = 0;
            for (int i = 0; i < 3; i++)
            {
                var recipe = Table.GetRecipe(i);
                bool made = Table.IsComponentCompleted(i);
                bool fitted = Station != null && Station.IsComponentInstalled(i);
                if (crafted[i] != made || installed[i] != fitted) changed = true;
                if (initialized && fitted && !installed[i]) Publish("Instalado: " + (recipe != null ? recipe.recipeName : "Componente"));
                else if (initialized && made && !crafted[i]) Publish("Fabricado: " + (recipe != null ? recipe.recipeName : "Componente"));
                crafted[i] = made; installed[i] = fitted;
                if (made) CraftedCount++;
                if (fitted) InstalledCount++;
                if (recipe == null) continue;
                foreach (var ingredient in recipe.ingredients)
                {
                    if (ingredient == null || ingredient.material == null) continue;
                    var material = ingredient.material;
                    int count = inventory != null ? inventory.GetMaterialAmount(material) : 0;
                    bool known = amounts.TryGetValue(material, out int previous);
                    if (!known || previous != count)
                    {
                        amounts[material] = count; changed = true;
                        if (initialized && known && count > previous) Publish("Recogido: " + material.materialName + " +" + (count - previous));
                    }
                }
            }
            float repair = -1f;
            foreach (var id in Owner.Runner.ActivePlayers)
                if (Owner.Runner.TryGetPlayerObject(id, out var actor) && actor != null && actor.IsValid)
                {
                    var action = actor.GetComponent<PlayerInteractionAction>();
                    if (action != null && action.IsBusy && action.ActionKind == 2 && Station != null
                        && action.TargetObject == Station.Object) repair = Mathf.Max(repair, action.Progress);
                }
            if (RepairProgress != repair)
            {
                if (initialized && RepairProgress < 0f && repair >= 0f) Publish("Instalando un componente en la nave…");
                else if (initialized && RepairProgress >= 0f && repair < 0f && InstalledCount == previousInstalled)
                    Publish("Instalación interrumpida · El componente sigue disponible");
                RepairProgress = repair; changed = true;
            }
            if (initialized && InstalledCount == 3 && previousInstalled < 3)
                Publish("Nave reparada · Misión completada");
            if (!initialized)
            {
                LatestActivity = "Misión sincronizada · " + CraftedCount + "/3 fabricadas · " + InstalledCount + "/3 instaladas";
                initialized = true; changed = true;
            }
            if (changed) Changed?.Invoke();
        }
        private void Publish(string message)
        {
            LatestActivity = message;
            Journal.Add(message);
            if (Journal.Count > 20) Journal.RemoveAt(0);
            Activity?.Invoke(message);
        }
    }
}
