using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Ship
{
    // Match actual recovered ship meshes to the recipe that consumes their salvage prefab.
    public sealed class ShipRepairPresentation : MonoBehaviour
    {
        public ShipRepairStation station;
        public GameObject[] missingParts;
        private int[] partComponents;
        private void BuildMap()
        {
            partComponents = new int[missingParts.Length];
            var meshes = new HashSet<Mesh>[3];
            for (int component = 0; component < 3; component++)
            {
                meshes[component] = new HashSet<Mesh>();
                var recipe = station.GetComponentRecipe(component);
                if (recipe == null) continue;
                foreach (var ingredient in recipe.ingredients)
                    if (ingredient.material != null && ingredient.material.prefab != null)
                        foreach (var filter in ingredient.material.prefab.GetComponentsInChildren<MeshFilter>(true))
                            if (filter.sharedMesh != null) meshes[component].Add(filter.sharedMesh);
            }
            for (int i = 0; i < missingParts.Length; i++)
            {
                partComponents[i] = Mathf.Min(2, i * 3 / Mathf.Max(1, missingParts.Length));
                if (missingParts[i] == null) continue;
                bool matched = false;
                foreach (var filter in missingParts[i].GetComponentsInChildren<MeshFilter>(true))
                {
                    for (int component = 0; component < 3; component++)
                        if (filter.sharedMesh != null && meshes[component].Contains(filter.sharedMesh))
                        { partComponents[i] = component; matched = true; break; }
                    if (matched) break;
                }
            }
        }
        private void LateUpdate()
        {
            if (station == null || station.Object == null || !station.Object.IsValid) return;
            if (partComponents == null || partComponents.Length != missingParts.Length) BuildMap();
            for (int i = 0; i < missingParts.Length; i++)
            {
                bool installed = station.IsComponentInstalled(partComponents[i]);
                if (missingParts[i] != null && missingParts[i].activeSelf != installed) missingParts[i].SetActive(installed);
            }
        }
    }
}
