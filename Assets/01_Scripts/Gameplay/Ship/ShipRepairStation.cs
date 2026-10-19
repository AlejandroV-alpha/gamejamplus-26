using Fusion;
using System.Collections.Generic;
using System.Linq;
using GameJam.Multiplayer;
using UnityEngine.SceneManagement;
using GameJam.Crafting;
using UnityEngine;

namespace GameJam.Ship
{
    /// <summary>
    /// Instala componentes fabricados uno por uno desde cualquier superficie de la nave.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
    public sealed class ShipRepairStation : NetworkBehaviour
    {
        [SerializeField] private CraftingTable craftingTable;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

        // Fusion genera el almacenamiento y la replicacion de esta propiedad.
        [Networked] private NetworkBool Repaired { get; set; }
        [Networked] private TickTimer ReturnToLobbyTimer { get; set; }

        [Networked] private int installedComponents { get; set; }
        private ShipRepairPresentation ship;
        private Collider[] hullColliders;
        private bool repairAnnounced;
        // 0: mission, 1: departure videos, 2: loading next planet, 3: final credits.
        [Networked] private int TravelPhase { get; set; }
        private readonly HashSet<PlayerRef> travelFinished = new();
        private bool localTravelStarted, localTravelFinished;
        private float nextTravelAck;
        private bool IsNeptune => gameObject.scene.name == "b_NeptuneExploration";
        private int NextPlanetIndex => SceneUtility.GetBuildIndexByScenePath("Assets/00_Scenes/b_NeptuneExploration.unity");

        public CraftingTable CraftingTable => craftingTable;
        public GameJam.Crafting.RecipeData GetComponentRecipe(int index) => craftingTable != null ? craftingTable.GetRecipe(index) : null;
        public bool IsComponentInstalled(int index) => IsReady() && index >= 0 && index < 3 && (installedComponents & (1 << index)) != 0;
        public int InstalledCount => !IsReady() ? 0 : ((installedComponents & 1) != 0 ? 1 : 0) + ((installedComponents & 2) != 0 ? 1 : 0) + ((installedComponents & 4) != 0 ? 1 : 0);
        public string ProgressLabel => "Instaladas " + InstalledCount + "/3 · Faltan " + (3 - InstalledCount);
        private int NextReadyComponent()
        {
            if (!IsReady() || craftingTable == null || craftingTable.Runner != Runner) return -1;
            for (int i = 0; i < 3; i++) if (!IsComponentInstalled(i) && craftingTable.IsComponentCompleted(i)) return i;
            return -1;
        }
        public Vector3 InteractionPoint(Vector3 position)
        {
            Vector3 result = transform.position;
            float best = float.PositiveInfinity;
            if (hullColliders != null)
                foreach (var collider in hullColliders)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger) continue;
                    Vector3 point = collider is MeshCollider mesh && !mesh.convex
                        ? collider.bounds.ClosestPoint(position) : collider.ClosestPoint(position);
                    float distance = (point - position).sqrMagnitude;
                    if (distance < best) { best = distance; result = point; }
                }
            return result;
        }
        public float DistanceSquared(Vector3 position) => (InteractionPoint(position) - position).sqrMagnitude;

        /// <summary>
        /// Reinicia solamente el aviso local al aparecer en la simulacion.
        /// </summary>
        public override void Spawned()
        {
            repairAnnounced = false;
            foreach (var presentation in FindObjectsByType<ShipRepairPresentation>(FindObjectsSortMode.None))
                if (presentation.station == this) { ship = presentation; break; }
            if (ship != null)
            {
                hullColliders = ship.GetComponentsInChildren<Collider>(true);
                // Keep the baked NetworkObject/controller; the graybox marker is no longer an interaction target.
                foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                var marker = GetComponent<Collider>();
                if (marker != null) marker.enabled = false;
            }
        }

        /// <summary>
        /// Consulta el estado de reparacion replicado de esta estacion.
        /// </summary>
        public bool IsRepaired()
        {
            if (!IsReady())
            {
                return false;
            }

            return Repaired;
        }

        /// <summary>
        /// Solicita reparar desde un jugador local cercano y habilitado.
        /// </summary>
        public Material feedbackMaterial;

        public bool CanBeginRepair()
        {
            return IsReady() && !Repaired && NextReadyComponent() >= 0 && GetValidPlayer(Runner.LocalPlayer)!=null;
        }

        public bool RequestRepair(bool finishingAnimation=false)
        {
            if (!IsReady())
            {
                return false;
            }

            PlayerCrafter player = GetValidPlayer(Runner.LocalPlayer);
            if (player == null || (!finishingAnimation && !player.CanCraftLocally()))
            {
                return false;
            }

            RpcRequestRepair();
            return true;
        }

        /// <summary>
        /// Valida la peticion y repara una sola vez desde la autoridad.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcRequestRepair(RpcInfo info = default)
        {
            if (!HasStateAuthority || !IsReady())
            {
                return;
            }

            PlayerRef requester = info.Source;
            if (info.IsInvokeLocal)
            {
                requester = Runner.LocalPlayer;
            }

            if (GetValidPlayer(requester) == null)
            {
                return;
            }

            if (Repaired)
            {
                RpcFeedback(requester, "La nave ya esta reparada.");
                return;
            }

            if (craftingTable == null || craftingTable.Runner != Runner)
            {
                RpcFeedback(requester, "La estacion no tiene una mesa valida.");
                return;
            }

            int component = NextReadyComponent();
            if (component < 0)
            {
                RpcFeedback(requester, ProgressLabel + ". Fabrica otro componente en la mesa.");
                return;
            }
            // Atomic on the station authority: two clients cannot install the same component twice.
            installedComponents |= 1 << component;
            var recipe = GetComponentRecipe(component);
            RpcFeedback(requester, (recipe != null ? recipe.recipeName : "Componente") + " instalado. " + ProgressLabel);
            var actor = GetValidPlayer(requester);
            RpcRepairEffect(actor != null ? InteractionPoint(actor.transform.position) + Vector3.up * .5f : transform.position);
            if (InstalledCount == 3)
            {
                Repaired = true;
                ReturnToLobbyTimer = TickTimer.CreateFromSeconds(Runner, 8f);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !Repaired) return;
            if (TravelPhase == 0 && ReturnToLobbyTimer.Expired(Runner))
            {
                if (!IsNeptune && NextPlanetIndex < 0)
                {
                    Debug.LogError("[Campaign] Falta b_NeptuneExploration en Build Settings.", this);
                    ReturnToLobbyTimer = TickTimer.CreateFromSeconds(Runner, 8f);
                    return;
                }
                ReturnToLobbyTimer = TickTimer.None;
                TravelPhase = 1;
            }
            if (TravelPhase != 1 || !Runner.ActivePlayers.Any()
                || !Runner.ActivePlayers.All(p => travelFinished.Contains(p))) return;
            if (IsNeptune)
            {
                TravelPhase = 3;
            }
            else if (Runner.IsSceneAuthority)
            {
                TravelPhase = 2;
                Runner.LoadScene(SceneRef.FromIndex(NextPlanetIndex));
            }
        }

        private void Update()
        {
            if (!IsReady()) return;
            if (TravelPhase == 1)
            {
                if (!localTravelStarted)
                    localTravelStarted = SceneTransitionVideo.PlaySequence(
                        IsNeptune ? new[] { "EscapandoDeNeptuno1" }
                            : new[] { "NAVE_Despegandon_Marte (1)", "LLEGANDO_Neptuno" },
                        IsNeptune ? -1 : NextPlanetIndex, () => localTravelFinished = true);
                if (localTravelFinished && Time.realtimeSinceStartup >= nextTravelAck)
                {
                    nextTravelAck = Time.realtimeSinceStartup + 1f;
                    RpcTravelFinished();
                }
            }
            else if (TravelPhase == 3) SceneTransitionVideo.ShowCredits();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcTravelFinished(RpcInfo info = default)
        {
            if (TravelPhase != 1) return;
            var source = info.IsInvokeLocal ? Runner.LocalPlayer : info.Source;
            if (Runner.ActivePlayers.Contains(source)) travelFinished.Add(source);
        }

        /// <summary>
        /// Muestra el mensaje de reparacion cuando el estado de red esta disponible.
        /// </summary>
        private void OnGUI()
        {
            if (!IsReady())
            {
                return;
            }

            if (!Repaired || !ReturnToLobbyTimer.IsRunning)
            {
                return;
            }

            float remaining = ReturnToLobbyTimer.RemainingTime(Runner) ?? 0f;

            GameJam.UI.GameplayFeedbackPanel.Draw(
                "MISI\u00d3N COMPLETADA - NAVE REPARADA",
                $"Despegue en {Mathf.CeilToInt(remaining)} s...",
                new Color(.25f, 1f, .75f), top: true);
        }


        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        private void RpcRepairEffect(Vector3 position)
        {
            GameJam.Multiplayer.InteractionFeedback.Burst(position,feedbackMaterial,new Color(.25f,1,.6f));
        }

        /// <summary>
        /// Comunica un rechazo exclusivamente al jugador solicitante.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcFeedback([RpcTarget] PlayerRef target, string message)
        {
            Debug.Log("[ShipRepair] " + message, this);
        }

        /// <summary>
        /// Anuncia el estado reparado una vez por cliente, incluso al entrar tarde.
        /// </summary>
        public override void Render()
        {
            if (!IsRepaired() || repairAnnounced)
            {
                return;
            }

            repairAnnounced = true;
            Debug.Log("[ShipRepair] Nave reparada.", this);
        }

        /// <summary>
        /// Comprueba identidad, estado del jugador y distancia a la estacion.
        /// </summary>
        private PlayerCrafter GetValidPlayer(PlayerRef requester)
        {
            NetworkObject playerObject;
            if (!Runner.TryGetPlayerObject(requester, out playerObject))
            {
                return null;
            }

            if (playerObject == null || !playerObject.IsValid)
            {
                return null;
            }

            PlayerRef owner = Runner.GameMode == GameMode.Shared ? playerObject.StateAuthority : playerObject.InputAuthority;
            if (owner != requester)
            {
                return null;
            }

            PlayerCrafter player = playerObject.GetComponent<PlayerCrafter>();
            if (player == null || !player.CanCraft())
            {
                return null;
            }

            float distanceSquared = DistanceSquared(playerObject.transform.position);
            if (distanceSquared > interactionDistance * interactionDistance)
            {
                return null;
            }

            return player;
        }

        /// <summary>
        /// Comprueba que el estado de red esta disponible.
        /// </summary>
        private bool IsReady()
        {
            if (Object == null || !Object.IsValid)
            {
                return false;
            }

            return Runner != null && Runner.IsRunning && StateBufferIsValid;
        }
    }
}
