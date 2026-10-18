using Fusion;
using GameJam.Collectibles;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInventory = GameJam.Inventory.Inventory;
using CharacterInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.Gameplay
{
    /// <summary>
    /// Solicita recoger el material cercano al pulsar E en el jugador local.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerCollector : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float searchRadius = 2.5f;
        [SerializeField] private LayerMask collectionLayers = Physics.DefaultRaycastLayers;

        private PlayerInventory inventory;
        private MarsFusionAvatar avatar;
        private CharacterInput characterInput;

        /// <summary>
        /// Obtiene los componentes existentes del astronauta.
        /// </summary>
        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            avatar = GetComponent<MarsFusionAvatar>();
            characterInput = GetComponent<CharacterInput>();
        }

        /// <summary>
        /// Lee E exclusivamente desde el jugador con autoridad local.
        /// </summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                // Only the locally owned avatar reports an attempted interaction.
                if (Object == null || !Object.IsValid || !HasStateAuthority) return;
                TryCollectNearest();
            }
        }

        /// <summary>
        /// Comprueba el inventario y el estado replicado del jugador.
        /// </summary>
        public bool CanCollect()
        {
            if (Object == null || !Object.IsValid)
            {
                return false;
            }

            if (inventory == null || avatar == null)
            {
                return false;
            }

            if (!avatar.IsAlive)
            {
                return false;
            }

            return !avatar.TabletOpen;
        }

        /// <summary>
        /// Solicita el material disponible mas cercano sin conceder recompensas.
        /// </summary>
        public bool TryCollectNearest()
        {
            if (!CanCollectLocally())
            {
                Debug.LogWarning($"[Interaction E] Blocked: networkReady={Object != null && Object.IsValid}, inventory={inventory != null}, avatar={avatar != null}, alive={(avatar != null && Object != null && Object.IsValid && avatar.IsAlive)}, tabletOpen={(avatar != null && Object != null && Object.IsValid && avatar.TabletOpen)}, controls={characterInput != null && characterInput.ControlsEnabled}.", this);
                return false;
            }

            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, searchRadius,
                collectionLayers, QueryTriggerInteraction.Ignore);
            MaterialPickup nearestPickup = null;
            HealthPickup nearestHealthPickup = null;
            float nearestDistanceSquared = searchRadius * searchRadius;

            foreach (Collider nearbyCollider in nearbyColliders)
            {
                HealthPickup healthPickup = nearbyCollider.GetComponentInParent<HealthPickup>();
                if (healthPickup != null && healthPickup.CanCollect(Object))
                {
                    float healthDistanceSquared = (healthPickup.transform.position - transform.position).sqrMagnitude;
                    if (healthDistanceSquared < nearestDistanceSquared)
                    {
                        nearestDistanceSquared = healthDistanceSquared;
                        nearestHealthPickup = healthPickup;
                        nearestPickup = null;
                    }
                    continue;
                }

                MaterialPickup pickup = nearbyCollider.GetComponentInParent<MaterialPickup>();
                if (pickup == null)
                {
                    continue;
                }

                if (!pickup.IsAvailable() || pickup.Runner != Runner)
                {
                    continue;
                }

                float distanceSquared = (pickup.transform.position - transform.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestPickup = pickup;
                    nearestHealthPickup = null;
                }
            }

            if (nearestHealthPickup != null)
            {
                return nearestHealthPickup.RequestCollection();
            }

            if (nearestPickup == null)
            {
                Debug.LogWarning($"[Interaction E] No available pickup within {searchRadius:F1} m. Colliders found: {nearbyColliders.Length}. Check pickup network registration, collider, layer and distance.", this);
                return false;
            }

            Debug.Log($"[Interaction E] Requesting {nearestPickup.name} at {Mathf.Sqrt(nearestDistanceSquared):F2} m.", nearestPickup);
            var action = GetComponent<GameJam.Multiplayer.PlayerInteractionAction>();
            return action != null ? action.TryCollect(nearestPickup) : nearestPickup.RequestCollection();
        }

        /// <summary>
        /// Bloquea la entrada remota y la recoleccion con controles desactivados.
        /// </summary>
        private bool CanCollectLocally()
        {
            if (!CanCollect())
            {
                return false;
            }

            if (!HasStateAuthority)
            {
                return false;
            }

            if (characterInput == null)
            {
                return false;
            }

            return characterInput.ControlsEnabled;
        }
    }
}
