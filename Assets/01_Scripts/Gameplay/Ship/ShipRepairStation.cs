using Fusion;
using GameJam.Crafting;
using UnityEngine;

namespace GameJam.Ship
{
    /// <summary>
    /// Sincroniza una reparacion por estacion tras completar las tres recetas.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
    public sealed class ShipRepairStation : NetworkBehaviour
    {
        [SerializeField] private CraftingTable craftingTable;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;

        // Fusion genera el almacenamiento y la replicacion de esta propiedad.
        [Networked] private NetworkBool Repaired { get; set; }

        private bool repairAnnounced;

        /// <summary>
        /// Reinicia solamente el aviso local al aparecer en la simulacion.
        /// </summary>
        public override void Spawned()
        {
            repairAnnounced = false;
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
        public bool RequestRepair()
        {
            if (!IsReady())
            {
                return false;
            }

            PlayerCrafter player = GetValidPlayer(Runner.LocalPlayer);
            if (player == null || !player.CanCraftLocally())
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

            if (!craftingTable.IsPlanetCompleted())
            {
                RpcFeedback(requester, "Faltan componentes: " + craftingTable.GetCompletedCount() + "/3.");
                return;
            }

            // La siguiente peticion ya encuentra la nave reparada. No hay consumo.
            Repaired = true;
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

            if (playerObject.StateAuthority != requester)
            {
                return null;
            }

            PlayerCrafter player = playerObject.GetComponent<PlayerCrafter>();
            if (player == null || !player.CanCraft())
            {
                return null;
            }

            float distanceSquared = (playerObject.transform.position - transform.position).sqrMagnitude;
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

            return StateBufferIsValid;
        }
    }
}
