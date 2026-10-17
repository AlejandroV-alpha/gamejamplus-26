using System.Collections.Generic;
using Fusion;
using GameJam.Inventory;
using UnityEngine;

namespace GameJam.Crafting
{
    /// <summary>
    /// Coordina tres recetas y conserva el progreso compartido del planeta.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
    public sealed class CraftingTable : NetworkBehaviour
    {
        private const int RecipeCount = 3;

        [SerializeField] private CraftingPlanet planet = CraftingPlanet.Mars;
        [SerializeField] private RecipeData[] recipes = new RecipeData[RecipeCount];
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

        // Estas propiedades son almacenamiento de red generado por Fusion.
        [Networked, Capacity(RecipeCount)]
        private NetworkArray<PlayerRef> ApprovedPlayers { get; }
        [Networked, Capacity(RecipeCount)]
        private NetworkArray<NetworkBool> CompletedRecipes { get; }

        private readonly Dictionary<MaterialData, int>[] recipeCosts = new Dictionary<MaterialData, int>[RecipeCount];
        private readonly bool[] consumedRecipes = new bool[RecipeCount];
        private readonly bool[] announcedRecipes = new bool[RecipeCount];
        private bool configurationValid;

        /// <summary>
        /// Valida las tres recetas existentes y prepara sus costes.
        /// </summary>
        private void Awake()
        {
            configurationValid = ValidateRecipes();
            if (!configurationValid)
            {
                Debug.LogError("[Crafting] Configuracion de recetas invalida.", this);
            }
        }

        /// <summary>
        /// Devuelve el planeta configurado en esta mesa.
        /// </summary>
        public CraftingPlanet GetPlanet()
        {
            return planet;
        }

        /// <summary>
        /// Devuelve la receta de una posicion entre cero y dos.
        /// </summary>
        public RecipeData GetRecipe(int recipeIndex)
        {
            if (!configurationValid || !IsValidIndex(recipeIndex))
            {
                return null;
            }

            return recipes[recipeIndex];
        }

        /// <summary>
        /// Consulta si un componente ya esta terminado en el estado replicado.
        /// </summary>
        public bool IsComponentCompleted(int recipeIndex)
        {
            if (!IsReady() || !IsValidIndex(recipeIndex))
            {
                return false;
            }

            return CompletedRecipes.Get(recipeIndex);
        }

        /// <summary>
        /// Cuenta los componentes terminados por cualquier jugador del equipo.
        /// </summary>
        public int GetCompletedCount()
        {
            int completedCount = 0;
            for (int index = 0; index < RecipeCount; index++)
            {
                if (IsComponentCompleted(index))
                {
                    completedCount++;
                }
            }

            return completedCount;
        }

        /// <summary>
        /// Indica si los tres componentes del planeta estan terminados.
        /// </summary>
        public bool IsPlanetCompleted()
        {
            return GetCompletedCount() == RecipeCount;
        }

        /// <summary>
        /// Solicita aprobacion sin descontar materiales del inventario.
        /// </summary>
        public bool RequestCraft(int recipeIndex)
        {
            if (!IsReady() || !IsValidIndex(recipeIndex))
            {
                return false;
            }

            if (IsComponentCompleted(recipeIndex))
            {
                Debug.Log("[Crafting] Componente ya fabricado: " + recipes[recipeIndex].recipeName, this);
                return false;
            }

            if (ApprovedPlayers.Get(recipeIndex) != PlayerRef.None)
            {
                Debug.Log("[Crafting] Componente pendiente de confirmacion.", this);
                return false;
            }

            PlayerCrafter crafter = GetValidCrafter(Runner.LocalPlayer);
            if (crafter == null || !crafter.CanCraftLocally())
            {
                return false;
            }

            if (!crafter.GetInventory().HasMaterials(recipeCosts[recipeIndex]))
            {
                Debug.Log("[Crafting] Materiales insuficientes: " + recipes[recipeIndex].recipeName, this);
                return false;
            }

            RpcRequestCraft(recipeIndex);
            return true;
        }

        /// <summary>
        /// Aprueba un unico jugador antes de autorizar el consumo.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestCraft(int recipeIndex, RpcInfo info = default)
        {
            if (!HasStateAuthority || !IsReady())
            {
                return;
            }

            if (!IsValidIndex(recipeIndex))
            {
                return;
            }

            PlayerRef requester = GetRequestSource(info);
            if (GetValidCrafter(requester) == null)
            {
                return;
            }

            if (CompletedRecipes.Get(recipeIndex))
            {
                RpcFeedback(requester, "Componente ya fabricado: " + recipes[recipeIndex].recipeName);
                return;
            }

            if (ApprovedPlayers.Get(recipeIndex) != PlayerRef.None)
            {
                RpcFeedback(requester, "Componente pendiente de confirmacion.");
                return;
            }

            ApprovedPlayers.Set(recipeIndex, requester);
            RpcApproveCraft(requester, recipeIndex);
        }

        /// <summary>
        /// Consume el coste completo solo en el inventario del jugador aprobado.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcApproveCraft([RpcTarget] PlayerRef target, int recipeIndex)
        {
            if (!IsReady() || !IsValidIndex(recipeIndex))
            {
                return;
            }

            if (consumedRecipes[recipeIndex])
            {
                RpcConfirmCraft(recipeIndex, true);
                return;
            }

            PlayerCrafter crafter = GetValidCrafter(Runner.LocalPlayer);
            if (crafter == null || !crafter.CanCraftLocally())
            {
                RpcConfirmCraft(recipeIndex, false);
                return;
            }

            bool consumed = crafter.GetInventory().TryRemoveMaterials(recipeCosts[recipeIndex]);
            consumedRecipes[recipeIndex] = consumed;
            if (!consumed)
            {
                Debug.Log("[Crafting] Materiales insuficientes: " + recipes[recipeIndex].recipeName, this);
            }

            RpcConfirmCraft(recipeIndex, consumed);
        }

        /// <summary>
        /// Registra la pieza o libera un intento que no desconto materiales.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcConfirmCraft(int recipeIndex, bool consumed, RpcInfo info = default)
        {
            if (!HasStateAuthority || !IsReady())
            {
                return;
            }

            if (!IsValidIndex(recipeIndex) || CompletedRecipes.Get(recipeIndex))
            {
                return;
            }

            PlayerRef requester = GetRequestSource(info);
            if (ApprovedPlayers.Get(recipeIndex) != requester)
            {
                return;
            }

            if (requester == PlayerRef.None)
            {
                return;
            }

            if (consumed)
            {
                CompletedRecipes.Set(recipeIndex, true);
            }
            else
            {
                ApprovedPlayers.Set(recipeIndex, PlayerRef.None);
            }

            // Una desconexion sin confirmacion no libera la pieza: el cobro es incierto.
        }

        /// <summary>
        /// Muestra un rechazo solamente al jugador que hizo la peticion.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcFeedback([RpcTarget] PlayerRef target, string message)
        {
            Debug.Log("[Crafting] " + message, this);
        }

        /// <summary>
        /// Anuncia cambios de progreso tambien a clientes que entran tarde.
        /// </summary>
        public override void Render()
        {
            if (!IsReady())
            {
                return;
            }

            bool changed = false;
            for (int index = 0; index < RecipeCount; index++)
            {
                if (!CompletedRecipes.Get(index) || announcedRecipes[index])
                {
                    continue;
                }

                announcedRecipes[index] = true;
                changed = true;
                Debug.Log("[Crafting] Componente fabricado: " + recipes[index].recipeName, this);
            }

            if (changed)
            {
                Debug.Log("[Crafting] Progreso del planeta " + planet + ": " + GetCompletedCount() + "/3", this);
                if (IsPlanetCompleted())
                {
                    Debug.Log("[Crafting] Tres componentes completados.", this);
                }
            }
        }

        /// <summary>
        /// Valida identidad, estado y distancia desde la instancia que comprueba.
        /// </summary>
        private PlayerCrafter GetValidCrafter(PlayerRef player)
        {
            NetworkObject playerObject;
            if (!Runner.TryGetPlayerObject(player, out playerObject))
            {
                return null;
            }

            if (playerObject == null || !playerObject.IsValid)
            {
                return null;
            }

            if (playerObject.StateAuthority != player)
            {
                return null;
            }

            PlayerCrafter crafter = playerObject.GetComponent<PlayerCrafter>();
            if (crafter == null || !crafter.CanCraft())
            {
                return null;
            }

            float distanceSquared = (playerObject.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared > interactionDistance * interactionDistance)
            {
                return null;
            }

            return crafter;
        }

        /// <summary>
        /// Identifica al emisor tanto en Shared como en una prueba Single.
        /// </summary>
        private PlayerRef GetRequestSource(RpcInfo info)
        {
            if (info.IsInvokeLocal)
            {
                return Runner.LocalPlayer;
            }

            return info.Source;
        }

        /// <summary>
        /// Comprueba el rango de las tres recetas configuradas.
        /// </summary>
        private bool IsValidIndex(int recipeIndex)
        {
            return recipeIndex >= 0 && recipeIndex < RecipeCount;
        }

        /// <summary>
        /// Comprueba la configuracion y la disponibilidad del estado de red.
        /// </summary>
        private bool IsReady()
        {
            if (!configurationValid || Object == null)
            {
                return false;
            }

            return Object.IsValid && StateBufferIsValid;
        }

        /// <summary>
        /// Rechaza recetas incompletas, repetidas o de otro planeta.
        /// </summary>
        private bool ValidateRecipes()
        {
            if (recipes == null || recipes.Length != RecipeCount)
            {
                return false;
            }

            HashSet<string> recipeIds = new HashSet<string>();
            for (int index = 0; index < RecipeCount; index++)
            {
                RecipeData recipe = recipes[index];
                if (recipe == null || recipe.planet != planet)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(recipe.id) || !recipeIds.Add(recipe.id))
                {
                    return false;
                }

                if (recipe.ingredients == null || recipe.ingredients.Count == 0)
                {
                    return false;
                }

                Dictionary<MaterialData, int> costs = new Dictionary<MaterialData, int>();
                foreach (RecipeIngredient ingredient in recipe.ingredients)
                {
                    if (ingredient == null || ingredient.material == null)
                    {
                        return false;
                    }

                    if (ingredient.amount <= 0 || string.IsNullOrWhiteSpace(ingredient.material.id))
                    {
                        return false;
                    }

                    int previousAmount;
                    costs.TryGetValue(ingredient.material, out previousAmount);
                    if (ingredient.amount > int.MaxValue - previousAmount)
                    {
                        return false;
                    }

                    costs[ingredient.material] = previousAmount + ingredient.amount;
                }

                recipeCosts[index] = costs;
            }

            return true;
        }
    }
}
