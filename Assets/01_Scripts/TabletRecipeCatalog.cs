using System;
using UnityEngine;

namespace GameJam.Tablet
{
    [CreateAssetMenu(menuName = "GameJam/Tablet Recipe Catalog")]
    public sealed class TabletRecipeCatalog : ScriptableObject
    {
        [Serializable] public sealed class Ingredient { public string mineral; public int amount; }
        [Serializable] public sealed class Recipe
        {
            public string component;
            public Ingredient[] ingredients;
            public GameJam.Crafting.RecipeData source;
        }
        [Serializable] public sealed class Planet
        {
            public string name;
            public string objective;
            public string[] minerals;
            public Color accent;
            public Recipe[] recipes;
        }
        public Planet[] planets;
    }
}
