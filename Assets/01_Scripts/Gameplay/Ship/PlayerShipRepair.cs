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
                    float distance=station.DistanceSquared(transform.position);
                    if(distance<nearest){nearest=distance;promptStation=station;}
                }
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var survivalRepair = GetComponent<GameJam.Survival.SurvivalShipRepair>();
            if ((survivalRepair == null || !survivalRepair.enabled) && keyboard.qKey.wasPressedThisFrame)
            {
                TryRepairNearest();
            }
        }

        private void OnGUI()
        {
            if (GetComponent<GameJam.Survival.SurvivalShipRepair>()?.enabled ?? false) return;
            if (promptStation==null || player==null || !player.CanCraftLocally()) return;
            if(promptStation.Object==null || !promptStation.Object.IsValid || promptStation.IsRepaired())return;

            GameJam.UI.GameplayFeedbackPanel.Draw(
                GameJam.Localization.GameLocalization.Get("hud.repair_ship"),
                "[Q]  |  " + promptStation.ProgressLabel, new Color(1f, .65f, .35f));
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
            foreach (var station in FindObjectsByType<ShipRepairStation>(FindObjectsSortMode.None))
            {
                if (station == null || station.Runner != Runner)
                {
                    continue;
                }

                float distanceSquared = station.DistanceSquared(transform.position);
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
