using Fusion;
using GameJam.Combat;
using GameJam.Collectibles;

namespace GameJam.Survival
{
    public sealed class SurvivalHealth : PlayerHealth
    {
        [Networked] private NetworkObject receiptPickup { get; set; }
        [Networked] private uint receiptVersion { get; set; }
        [Networked] private NetworkBool receiptHealed { get; set; }

        private HealthPickup pendingPickup;
        private uint pendingVersion;

        /// <summary>
        /// Recibe la reserva observada sin escribir salud fuera de la simulacion.
        /// </summary>
        internal void QueueHealthPickup(HealthPickup pickup, uint version)
        {
            if (!HasStateAuthority || pendingPickup != null)
            {
                return;
            }
            pendingPickup = pickup;
            pendingVersion = version;
        }

        /// <summary>
        /// Resuelve reservas en el tick del astronauta aunque el botiquin sea remoto.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            if (!HasStateAuthority || pendingPickup == null)
            {
                return;
            }
            ResolveHealthPickup(pendingPickup, pendingVersion);
            pendingPickup = null;
        }

        /// <summary>
        /// Comprueba si puede recibir otro botiquin sin perder una confirmacion pendiente.
        /// </summary>
        public bool CanReceiveHealthPickup()
        {
            if (Object == null || !Object.IsValid || !StateBufferIsValid || !IsAlive || CurrentHealth >= MaxHealth)
            {
                return false;
            }
            return ReceiptIsClosed();
        }

        /// <summary>
        /// Conserva el resultado hasta que la autoridad del botiquin lo haya reconocido.
        /// </summary>
        private bool ReceiptIsClosed()
        {
            if (receiptPickup == null || !receiptPickup.IsValid)
            {
                return true;
            }
            HealthPickup pickup = receiptPickup.GetComponent<HealthPickup>();
            return pickup != null && pickup.IsReceiptClosed(receiptVersion);
        }

        /// <summary>
        /// Resuelve una reserva replicada una sola vez desde la autoridad de la salud.
        /// </summary>
        internal void ResolveHealthPickup(HealthPickup pickup, uint version)
        {
            if (!HasStateAuthority || Object == null || !Object.IsValid || !StateBufferIsValid || pickup == null)
            {
                return;
            }
            if (receiptPickup == pickup.Object && receiptVersion == version)
            {
                return;
            }
            if (!ReceiptIsClosed() || !pickup.IsReservedFor(Object, version))
            {
                return;
            }

            // La decision queda replicada junto con la salud en el mismo tick.
            receiptPickup = pickup.Object;
            receiptVersion = version;
            receiptHealed = false;
            if (pickup.CanDeliverTo(Object, version))
            {
                receiptHealed = HealAlive(pickup.GetHealAmount());
            }
        }

        /// <summary>
        /// Permite al botiquin leer una confirmacion persistente sin aceptar RPC de curacion.
        /// </summary>
        internal bool TryReadHealthReceipt(NetworkObject pickup, uint version, out bool healed)
        {
            healed = false;
            if (Object == null || !Object.IsValid || !StateBufferIsValid || receiptPickup != pickup || receiptVersion != version)
            {
                return false;
            }
            healed = receiptHealed;
            return true;
        }
    }
}
