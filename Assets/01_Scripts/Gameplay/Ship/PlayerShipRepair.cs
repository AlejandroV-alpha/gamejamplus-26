using Fusion;
using GameJam.Crafting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Ship
{
    /// <summary>
    /// Solicita reparar con R desde el astronauta local.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCrafter))]
    public sealed class PlayerShipRepair : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

        private PlayerCrafter player;

        /// <summary>
        /// Reutiliza las comprobaciones existentes de estado y control local.
        /// </summary>
        private void Awake()
        {
            player = GetComponent<PlayerCrafter>();
        }

        /// <summary>
        /// Lee R solo cuando el jugador local puede interactuar.
        /// </summary>
        private void Update()
        {
            if (player == null || !player.CanCraftLocally())
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                TryRepairNearest();
            }
        }

        /// <summary>
        /// Solicita reparar en la estacion mas cercana del mismo runner.
        /// </summary>
        public bool TryRepairNearest()
        {
            if (player == null || !player.CanCraftLocally())
            {
                return false;
            }

            ShipRepairStation nearestStation = null;
            float nearestDistanceSquared = interactionDistance * interactionDistance;
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            foreach (Collider nearbyCollider in colliders)
            {
                ShipRepairStation station = nearbyCollider.GetComponentInParent<ShipRepairStation>();
                if (station == null || station.Runner != Runner)
                {
                    continue;
                }

                float distanceSquared = (station.transform.position - transform.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestStation = station;
                }
            }

            if (nearestStation == null)
            {
                return false;
            }

            return nearestStation.RequestRepair();
        }
    }
}
