using Fusion;
using GameJam.Gameplay;
using GameJam.Multiplayer;
using GameJam.Survival;
using UnityEngine;

namespace GameJam.Collectibles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
    public sealed class HealthPickup : NetworkBehaviour
    {
        [SerializeField, Min(1)] private int healAmount = 30;
        [SerializeField, Min(0.1f)] private float collectionDistance = 2.5f;
        [SerializeField, Min(0.5f)] private float confirmationTimeout = 3f;

        private const byte Available = 0;
        private const byte Reserved = 1;
        private const byte Cancelling = 2;
        private const byte Consumed = 3;

        [Networked] private byte phase { get; set; }
        [Networked] private NetworkObject claimant { get; set; }
        [Networked] private PlayerRef claimantOwner { get; set; }
        [Networked] private uint version { get; set; }
        [Networked] private TickTimer deadline { get; set; }
        private float requestAgainAt;

        /// <summary>
        /// Comprueba disponibilidad y elegibilidad antes de seleccionar el botiquin.
        /// </summary>
        public bool CanCollect(NetworkObject player)
        {
            if (!IsReady() || phase != Available || !IsValidPlayer(player))
            {
                return false;
            }
            SurvivalHealth health = player.GetComponent<SurvivalHealth>();
            return health.CanReceiveHealthPickup();
        }

        /// <summary>
        /// Solicita una reserva sin conceder salud ni duplicar solicitudes pendientes.
        /// </summary>
        public bool RequestCollection()
        {
            if (!IsReady() || Time.unscaledTime < requestAgainAt)
            {
                return false;
            }
            NetworkObject player;
            if (!Runner.TryGetPlayerObject(Runner.LocalPlayer, out player) || !player.HasStateAuthority || !CanCollect(player))
            {
                return false;
            }
            requestAgainAt = Time.unscaledTime + confirmationTimeout;
            RPC_RequestCollection();
            return true;
        }

        /// <summary>
        /// Reserva para un solo emisor validado por la autoridad del botiquin.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestCollection(RpcInfo info = default)
        {
            if (!HasStateAuthority || !IsReady() || phase != Available)
            {
                return;
            }
            PlayerRef source = info.Source;
            if (info.IsInvokeLocal)
            {
                source = Runner.LocalPlayer;
            }
            NetworkObject player;
            if (!Runner.TryGetPlayerObject(source, out player) || !CanCollect(player) || GetOwner(player) != source)
            {
                return;
            }
            claimant = player;
            claimantOwner = source;
            version++;
            phase = Reserved;
            deadline = TickTimer.CreateFromSeconds(Runner, confirmationTimeout);
        }

        /// <summary>
        /// Aplica la recompensa en su propietario y confirma el resultado replicado.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!IsReady() || phase == Available)
            {
                return;
            }
            SurvivalHealth health = null;
            if (claimant != null && claimant.IsValid)
            {
                health = claimant.GetComponent<SurvivalHealth>();
                if (health != null && health.HasStateAuthority && (phase == Reserved || phase == Cancelling))
                {
                    health.ResolveHealthPickup(this, version);
                }
            }
            if (!HasStateAuthority)
            {
                return;
            }
            if (phase == Consumed)
            {
                Runner.Despawn(Object);
                return;
            }
            bool healed;
            if (health != null && health.TryReadHealthReceipt(Object, version, out healed))
            {
                if (healed)
                {
                    phase = Consumed;
                    Debug.Log("[HealthPickup] Curacion confirmada; consumible retirado.", this);
                    Runner.Despawn(Object);
                }
                else
                {
                    phase = Available;
                    claimant = null;
                    claimantOwner = PlayerRef.None;
                    deadline = TickTimer.None;
                }
                return;
            }
            NetworkObject registered;
            if (claimant == null || !claimant.IsValid || !Runner.TryGetPlayerObject(claimantOwner, out registered)
                || registered != claimant || GetOwner(claimant) != claimantOwner)
            {
                // Sin confirmacion no es seguro reasignar una recompensa posiblemente aplicada.
                phase = Consumed;
                Runner.Despawn(Object);
                return;
            }
            if (deadline.ExpiredOrNotRunning(Runner))
            {
                if (phase == Reserved)
                {
                    phase = Cancelling;
                    deadline = TickTimer.CreateFromSeconds(Runner, confirmationTimeout);
                }
                else
                {
                    phase = Consumed;
                    Debug.LogWarning("[HealthPickup] Reserva sin confirmacion; retirado para evitar duplicados.", this);
                    Runner.Despawn(Object);
                }
            }
        }

        /// <summary>
        /// Entrega la reserva al propietario local sin depender de simular proxies.
        /// </summary>
        public override void Render()
        {
            if (!IsReady() || (phase != Reserved && phase != Cancelling) || claimant == null || !claimant.IsValid)
            {
                return;
            }
            SurvivalHealth health = claimant.GetComponent<SurvivalHealth>();
            if (health != null && health.HasStateAuthority)
            {
                health.QueueHealthPickup(this, version);
            }
        }

        /// <summary>
        /// Identifica una reserva vigente sin depender de RPC de recompensa.
        /// </summary>
        internal bool IsReservedFor(NetworkObject player, uint token)
        {
            return IsReady() && (phase == Reserved || phase == Cancelling) && claimant == player
                && version == token && player != null && player.IsValid && player.Runner == Runner;
        }

        /// <summary>
        /// Revalida el permiso antes de escribir la salud del consumidor.
        /// </summary>
        internal bool CanDeliverTo(NetworkObject player, uint token)
        {
            if (!IsReservedFor(player, token) || phase != Reserved || GetOwner(player) != claimantOwner || !IsValidPlayer(player))
            {
                return false;
            }
            Starter.ThirdPersonCharacter.PlayerInput input = player.GetComponent<Starter.ThirdPersonCharacter.PlayerInput>();
            return input != null && input.ControlsEnabled;
        }

        /// <summary>
        /// Expone el valor configurado sin permitir cantidades enviadas por clientes.
        /// </summary>
        internal int GetHealAmount()
        {
            return healAmount;
        }

        /// <summary>
        /// Indica cuando el jugador puede reemplazar su recibo anterior.
        /// </summary>
        internal bool IsReceiptClosed(uint token)
        {
            return IsReady() && (version != token || phase == Available || phase == Consumed);
        }

        /// <summary>
        /// Valida sesion, salud, recolector y distancia usando el estado de red.
        /// </summary>
        private bool IsValidPlayer(NetworkObject player)
        {
            if (player == null || !player.IsValid || player.Runner != Runner || healAmount <= 0)
            {
                return false;
            }
            SurvivalHealth health = player.GetComponent<SurvivalHealth>();
            PlayerCollector collector = player.GetComponent<PlayerCollector>();
            PlayerInteractionAction action = player.GetComponent<PlayerInteractionAction>();
            if (health == null || !health.IsAlive || health.CurrentHealth >= health.MaxHealth
                || collector == null || !collector.isActiveAndEnabled || !collector.CanCollect())
            {
                return false;
            }
            if (action != null && action.IsBusy)
            {
                return false;
            }
            return (player.transform.position - transform.position).sqrMagnitude <= collectionDistance * collectionDistance;
        }

        /// <summary>
        /// Conserva la identificacion del propietario en Shared y pruebas Single.
        /// </summary>
        private PlayerRef GetOwner(NetworkObject player)
        {
            if (Runner.GameMode == GameMode.Shared)
            {
                return player.StateAuthority;
            }
            return player.InputAuthority;
        }

        /// <summary>
        /// Evita leer propiedades antes de que Fusion asigne su estado.
        /// </summary>
        private bool IsReady()
        {
            return Object != null && Object.IsValid && StateBufferIsValid;
        }

        /// <summary>
        /// Limita los valores configurables a rangos validos.
        /// </summary>
        private void OnValidate()
        {
            healAmount = Mathf.Max(1, healAmount);
            collectionDistance = Mathf.Max(0.1f, collectionDistance);
            confirmationTimeout = Mathf.Max(0.5f, confirmationTimeout);
        }
    }
}
