using Fusion;
using GameJam.Crafting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Ship
{
    /// <summary>
    /// Solicita reparar con F desde el astronauta local.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCrafter))]
    public sealed class PlayerShipRepair : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

        private PlayerCrafter player;
        private ShipRepairStation promptStation;
        private float nextPromptScan;

        /// <summary>
        /// Reutiliza las comprobaciones existentes de estado y control local.
        /// </summary>
        private void Awake()
        {
            player = GetComponent<PlayerCrafter>();
        }

        /// <summary>
        /// Lee F solo cuando el jugador local puede interactuar.
        /// </summary>
        private void Update()
        {
            if (player == null || !player.CanCraftLocally())
            {
                return;
            }

            if (Time.time >= nextPromptScan)
            {
                nextPromptScan = Time.time + .25f;
                promptStation = null;
                float nearest = interactionDistance * interactionDistance;
                foreach(var station in FindObjectsByType<ShipRepairStation>(FindObjectsSortMode.None))
                {
                    if(station.Object==null || !station.Object.IsValid || station.Runner!=Runner || station.IsRepaired()) continue;
                    float distance=(station.transform.position-transform.position).sqrMagnitude;
                    if(distance<nearest){nearest=distance;promptStation=station;}
                }
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                TryRepairNearest();
            }
        }

        private void OnGUI()
        {
            if (promptStation==null || player==null || !player.CanCraftLocally()) return;
            if(promptStation.Object==null || !promptStation.Object.IsValid || promptStation.IsRepaired())return;
            GUI.Box(new Rect(Screen.width*.5f-140,Screen.height*.84f,280,32),"F  |  Reparar nave");
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
                Debug.LogWarning($"[ShipRepair] No repair station from this session within {interactionDistance:F1} m.", this);
                return false;
            }

            var action = GetComponent<GameJam.Multiplayer.PlayerInteractionAction>();
            return action != null ? action.TryRepair(nearestStation) : nearestStation.RequestRepair();
        }
    }
}
