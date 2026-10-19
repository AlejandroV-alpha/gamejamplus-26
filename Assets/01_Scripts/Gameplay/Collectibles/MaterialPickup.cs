using Fusion;
using GameJam.Inventory;
using GameJam.Gameplay;
using UnityEngine;
using PlayerInventory = GameJam.Inventory.Inventory;

namespace GameJam.Collectibles
{
    /// <summary>
    /// Decide un ganador y coordina una unica entrega de materiales.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
    public sealed class MaterialPickup : NetworkBehaviour
    {
        [SerializeField] private MaterialData material;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField, Min(0.1f)] private float collectionDistance = 2.5f;
        [SerializeField] private GameObject visual;

        [Networked] private TickTimer despawnAfter { get; set; }
        [Networked] public int DroppedAmount { get; set; }
        // These pickups do not move after placement. Replicate their world pose once,
        // rather than relying on Runner.Spawn's local Transform arguments.
        [Networked] private Vector3 PlacedPosition { get; set; }
        [Networked] private Quaternion PlacedRotation { get; set; }
        [Networked] private NetworkBool HasPlacedPose { get; set; }
        private int Quantity => DroppedAmount > 0 ? DroppedAmount : amount;

        [Networked] private NetworkBool IsClaimed { get; set; }
        [Networked] private PlayerRef Claimant { get; set; }

        public Material feedbackMaterial;
        private Collider pickupCollider;
        private bool claimHandled;
        private bool rewardApplied;

        /// <summary>
        /// Obtiene el collider sin alterar la identidad de red del objeto.
        /// </summary>
        private void Awake()
        {
            pickupCollider = GetComponent<Collider>();
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                PlacedPosition = transform.position;
                PlacedRotation = transform.rotation;
                HasPlacedPose = true;
            }
            ApplyPlacedPose();
        }

        private void ApplyPlacedPose()
        {
            if (IsReady() && HasPlacedPose && !HasStateAuthority)
                transform.SetPositionAndRotation(PlacedPosition, PlacedRotation);
        }

        /// <summary>
        /// Indica si el material sigue disponible en el estado replicado.
        /// </summary>
        public bool IsAvailable()
        {
            if (!IsReady())
            {
                return false;
            }

            return !IsClaimed;
        }

        /// <summary>
        /// Envia una peticion local sin entregar ninguna recompensa.
        /// </summary>
        public bool RequestCollection()
        {
            if (!IsAvailable())
            {
                return false;
            }

            NetworkObject playerObject;
            if (!Runner.TryGetPlayerObject(Runner.LocalPlayer, out playerObject))
            {
                return false;
            }

            if (!IsValidCollector(playerObject, Runner.LocalPlayer))
            {
                return false;
            }

            RPC_RequestCollection();
            return true;
        }

        /// <summary>
        /// Acepta una sola peticion desde la autoridad del recolectable.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestCollection(RpcInfo info = default)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            if (!IsAvailable())
            {
                return;
            }

            PlayerRef requester = GetRequestSource(info);
            NetworkObject playerObject;
            if (!Runner.TryGetPlayerObject(requester, out playerObject))
            {
                return;
            }

            if (!IsValidCollector(playerObject, requester))
            {
                return;
            }

            // La autoridad cierra la disponibilidad antes de procesar otra peticion.
            Claimant = requester;
            IsClaimed = true;
        }

        /// <summary>
        /// Entrega el material solo al ganador confirmado por el estado de red.
        /// </summary>
        private void Update()
        {
            if (!IsReady())
            {
                return;
            }

            if (!IsClaimed)
            {
                claimHandled = false;
                return;
            }

            if (Claimant != Runner.LocalPlayer)
            {
                return;
            }

            if (claimHandled || rewardApplied)
            {
                return;
            }

            NetworkObject playerObject;
            if (!Runner.TryGetPlayerObject(Claimant, out playerObject))
            {
                return;
            }

            PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }

            if (!inventory.HasStateAuthority)
            {
                return;
            }

            claimHandled = true;
            rewardApplied = inventory.AddMaterial(material, Quantity);
            RPC_ConfirmCollection(rewardApplied);
        }

        /// <summary>
        /// Retira el objeto tras confirmar la entrega o libera una entrega fallida.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ConfirmCollection(bool rewardAdded, RpcInfo info = default)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            if (!IsClaimed)
            {
                return;
            }

            if (GetRequestSource(info) != Claimant)
            {
                return;
            }

            if (rewardAdded)
            {
                RPC_CollectedEffect();
                despawnAfter = TickTimer.CreateFromSeconds(Runner,.2f);
                return;
            }

            Claimant = PlayerRef.None;
            IsClaimed = false;
        }

        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        private void RPC_CollectedEffect()
        {
            GameJam.Multiplayer.InteractionFeedback.Burst(transform.position+Vector3.up*.4f,feedbackMaterial,new Color(.3f,1,.85f));
        }

        /// <summary>
        /// Descarta una entrega interrumpida sin reasignar su recompensa.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            if (despawnAfter.IsRunning)
            {
                if (despawnAfter.Expired(Runner)) Runner.Despawn(Object);
                return;
            }
            if (!IsClaimed)
            {
                return;
            }

            NetworkObject playerObject;
            if (!Runner.TryGetPlayerObject(Claimant, out playerObject))
            {
                Runner.Despawn(Object);
            }
        }

        /// <summary>
        /// Refleja la disponibilidad sin desactivar el objeto de red.
        /// </summary>
        public override void Render()
        {
            ApplyPlacedPose();
            bool available = IsAvailable();
            if (visual != null)
            {
                visual.SetActive(available);
            }

            if (pickupCollider != null)
            {
                pickupCollider.enabled = available;
            }
        }

        /// <summary>
        /// Valida identidad, inventario, estado del jugador y distancia.
        /// </summary>
        private bool IsValidCollector(NetworkObject playerObject, PlayerRef requester)
        {
            if (material == null || amount <= 0)
            {
                return false;
            }

            if (playerObject == null || !playerObject.IsValid)
            {
                return false;
            }

            // Shared assigns state ownership to the player; Single/Host keep it on the server.
            PlayerRef owner = Runner.GameMode == GameMode.Shared ? playerObject.StateAuthority : playerObject.InputAuthority;
            if (owner != requester)
            {
                return false;
            }

            PlayerCollector collector = playerObject.GetComponent<PlayerCollector>();
            if (collector == null)
            {
                return false;
            }

            if (!collector.CanCollect())
            {
                return false;
            }

            float distanceSquared = (playerObject.transform.position - transform.position).sqrMagnitude;
            return distanceSquared <= collectionDistance * collectionDistance;
        }

        /// <summary>
        /// Identifica al emisor real tanto en Shared como en la prueba Single.
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
        /// Comprueba que el estado replicado se pueda consultar.
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
