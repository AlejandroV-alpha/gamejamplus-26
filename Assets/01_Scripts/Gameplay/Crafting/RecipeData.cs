using System;
using System.Collections.Generic;
using GameJam.Inventory;
using UnityEngine;

namespace GameJam.Crafting
{
    public enum CraftingPlanet
    {
        Neptune = 0,
        Saturn = 1,
        Mars = 2
    }

    /// <summary>
    /// Define un material y la cantidad requerida por una receta.
    /// </summary>
    [Serializable]
    public sealed class RecipeIngredient
    {
        public MaterialData material;
        [Min(1)]
        public int amount = 1;
    }

    /// <summary>
    /// Define los datos de una receta sin ejecutar la fabricacion.
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeData", menuName = "GameJam/Crafting/Recipe")]
    public sealed class RecipeData : ScriptableObject
    {
        [Tooltip("Identificador unico y estable. No cambiar despues de asignarlo.")]
        public string id;
        public string recipeName;
        [TextArea]
        public string description;
        public CraftingPlanet planet;
        public Sprite icon;
        public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();
    }
}
