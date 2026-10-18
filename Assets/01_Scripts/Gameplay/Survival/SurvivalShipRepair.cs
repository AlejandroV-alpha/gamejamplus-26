using Fusion;
using GameJam.Crafting;
using GameJam.Multiplayer;
using GameJam.Ship;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Survival
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCrafter))]
    public sealed class SurvivalShipRepair : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        private PlayerCrafter player;
        private ShipRepairStation promptStation;
        private float nextPromptScan;

        /// <summary>
        /// Obtiene el componente existente de crafting.
        /// </summary>
        private void Awake()
        {
            player = GetComponent<PlayerCrafter>();
        }

        /// <summary>
        /// Actualiza el aviso existente sin leer F por segunda vez.
        /// </summary>
        private void Update()
        {
            if (Object != null && Object.IsValid && HasStateAuthority && Application.isFocused && Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                TryRepairNearest();
            }

            if (player == null || !player.CanCraftLocally() || Time.time < nextPromptScan)
            {
                return;
            }
            nextPromptScan = Time.time + 0.25f;
            promptStation = null;
            float nearest = interactionDistance * interactionDistance;
            foreach (ShipRepairStation station in FindObjectsByType<ShipRepairStation>(FindObjectsSortMode.None))
            {
                if (station.Object == null || !station.Object.IsValid || station.Runner != Runner || station.IsRepaired())
                {
                    continue;
                }
                float distance = (station.transform.position - transform.position).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    promptStation = station;
                }
            }
        }

        /// <summary>
        /// Conserva exclusivamente el aviso de reparacion que ya existia.
        /// </summary>
        private void OnGUI()
        {
            if (promptStation == null || player == null || !player.CanCraftLocally())
            {
                return;
            }
            if (promptStation.Object == null || !promptStation.Object.IsValid || promptStation.IsRepaired())
            {
                return;
            }
            GUI.Box(new Rect(Screen.width * 0.5f - 140, Screen.height * 0.84f, 280, 32), "Q  |  " + GameJam.Localization.GameLocalization.Get("hud.repair_ship"));
        }

        /// <summary>
        /// Reutiliza la reparacion original con la tecla Q.
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
                Debug.LogWarning("[ShipRepair] No hay una estacion de esta sesion dentro del rango.", this);
                return false;
            }
            PlayerInteractionAction action = GetComponent<PlayerInteractionAction>();
            if (action != null)
            {
                return action.TryRepair(nearestStation);
            }
            return nearestStation.RequestRepair();
        }
    }
}
