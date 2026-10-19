using Fusion;
using GameJam.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInventory = GameJam.Inventory.Inventory;
using CharacterInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.Crafting
{
    /// <summary>
    /// Lee las teclas locales y solicita recetas a la mesa cercana.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCollector))]
    public sealed class PlayerCrafter : NetworkBehaviour
    {
        [SerializeField] private Key firstRecipeKey = Key.Digit4;
        [SerializeField] private Key secondRecipeKey = Key.Digit5;
        [SerializeField] private Key thirdRecipeKey = Key.Digit6;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

        private PlayerCollector collector;
        private CharacterInput characterInput;
        private PlayerInventory inventory;

        /// <summary>
        /// Obtiene los componentes propios y el control existente.
        /// </summary>
        private void Awake()
        {
            collector = GetComponent<PlayerCollector>();
            characterInput = GetComponent<CharacterInput>();
            inventory = GetComponent<PlayerInventory>();
        }

        /// <summary>
        /// Consulta las teclas sin alterar los controles originales.
        /// </summary>
        private void Update()
        {
            if (!CanCraftLocally())
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[firstRecipeKey].wasPressedThisFrame)
            {
                TryCraftNearest(0);
            }
            else if (keyboard[secondRecipeKey].wasPressedThisFrame)
            {
                TryCraftNearest(1);
            }
            else if (keyboard[thirdRecipeKey].wasPressedThisFrame)
            {
                TryCraftNearest(2);
            }
        }

        /// <summary>
        /// Comprueba el estado del jugador visible para las otras autoridades.
        /// </summary>
        public bool CanCraft()
        {
            if (collector == null || inventory == null)
            {
                return false;
            }

            return collector.CanCollect();
        }

        /// <summary>
        /// Acepta entrada solo del propietario con controles habilitados.
        /// </summary>
        public bool CanCraftLocally()
        {
            if (!CanCraft() || !HasStateAuthority)
            {
                return false;
            }

            if (characterInput == null)
            {
                return false;
            }

            return characterInput.ControlsEnabled;
        }

        /// <summary>The open crafting box blocks movement, but permits its own recipe requests.</summary>
        public bool CanCraftAtTable(CraftingTable table)
        {
            if (!CanCraft() || !HasStateAuthority || characterInput == null) return false;
            if (characterInput.ControlsEnabled) return true;

            var box = table != null ? table.GetComponent<CraftingBox>() : null;
            if (box == null || !box.IsOpenFor(gameObject)) return false;
            var avatar = GetComponent<GameJam.Multiplayer.MarsFusionAvatar>();
            var session = FindAnyObjectByType<GameJam.Multiplayer.MarsFusionSession>();
            return avatar != null && (session == null || !session.MenuOpen)
                && (avatar.LocalUI == null || !avatar.LocalUI.IsVisible)
                && (avatar.LocalHud == null || !avatar.LocalHud.IsInventoryOpen)
                && !(GetComponent<GameJam.Survival.SurvivalRevive>()?.IsRecovering ?? false)
                && !(GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false)
                && !(GetComponent<GameJam.Multiplayer.PlayerInteractionAction>()?.IsBusy ?? false)
                && !(GetComponent<GameJam.Combat.PlayerHurtReaction>()?.IsReacting ?? false)
                && !GameJam.Multiplayer.SceneTransitionVideo.IsBlockingInput
                && !GameJam.Multiplayer.LobbyDepartureEffects.IsBlockingInput
                && !GameJam.Multiplayer.PortalWhiteTransition.IsBlockingInput
                && !GusanoExcavadorFusion.LocalPlayerHeld;
        }

        /// <summary>
        /// Devuelve el inventario individual del astronauta.
        /// </summary>
        public PlayerInventory GetInventory()
        {
            return inventory;
        }

        /// <summary>
        /// Solicita una receta a la mesa mas cercana del mismo runner.
        /// </summary>
        public bool TryCraftNearest(int recipeIndex)
        {
            if (!CanCraftLocally())
            {
                Debug.LogWarning("[Crafting] Interaction blocked: check player authority, health, tablet and controls.", this);
                return false;
            }

            CraftingTable nearestTable = null;
            float nearestDistanceSquared = interactionDistance * interactionDistance;
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            foreach (Collider nearbyCollider in colliders)
            {
                CraftingTable table = nearbyCollider.GetComponentInParent<CraftingTable>();
                if (table == null || table.Runner != Runner)
                {
                    continue;
                }

                float distanceSquared = (table.transform.position - transform.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestTable = table;
                }
            }

            if (nearestTable == null)
            {
                Debug.LogWarning($"[Crafting] No crafting table from this session within {interactionDistance:F1} m. Use keys 4, 5 or 6 beside the table.", this);
                return false;
            }

            return nearestTable.RequestCraft(recipeIndex);
        }
    }
}
