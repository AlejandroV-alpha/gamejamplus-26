using Fusion;
using GameJam.Collectibles;
using GameJam.Survival;
using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// Proporciona al avatar su unica vida sincronizada y recibe dano con autoridad.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHealth : NetworkBehaviour, IPlayerHealth
    {
        [SerializeField, Min(1f)] private float startingHealth = 100f;

        // El contrato existente y Fusion requieren estas propiedades de red.
        [Networked] public float CurrentHealth { get; private set; }
        [Networked] public float MaxHealth { get; private set; }
        [Networked] public bool IsAlive { get; private set; }

        [Networked] public uint DeathSequence { get; private set; }

        private bool deathAnnounced;

        /// <summary>
        /// Inicializa la vida solamente desde el propietario del astronauta.
        /// </summary>
        public override void Spawned()
        {
            deathAnnounced = false;
            if (HasStateAuthority)
            {
                MaxHealth = Mathf.Max(1f, startingHealth);
                CurrentHealth = MaxHealth;
                IsAlive = true;
            }
        }

        /// <summary>
        /// Consulta la vida actual cuando existe estado de red.
        /// </summary>
        public float GetCurrentHealth()
        {
            if (!IsReady())
            {
                return 0f;
            }

            return CurrentHealth;
        }

        /// <summary>
        /// Consulta la vida maxima sincronizada.
        /// </summary>
        public float GetMaxHealth()
        {
            if (!IsReady())
            {
                return 0f;
            }

            return MaxHealth;
        }

        /// <summary>
        /// Aplica dano local autorizado o lo solicita al propietario del objetivo.
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (!IsReady() || damage <= 0)
            {
                return;
            }

            if (HasStateAuthority)
            {
                ApplyDamage(damage);
            }
            else
            {
                RpcTakeDamage(damage);
            }
        }

        /// <summary>
        /// Recibe peticiones de dano en la autoridad del objetivo.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RpcTakeDamage(int damage)
        {
            ApplyDamage(damage);
        }

        /// <summary>
        /// Reduce la vida sin valores negativos y registra la muerte.
        /// </summary>
        private void ApplyDamage(int damage)
        {
            if (!IsReady() || !HasStateAuthority)
            {
                return;
            }

            if (damage <= 0 || !IsAlive || GameJam.Multiplayer.SceneTransitionVideo.IsCampaignTransition)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            IsAlive = CurrentHealth > 0f;
            if (!IsAlive)
            {
                DeathSequence++;
            }
            Debug.Log("[PlayerHealth] Vida: " + CurrentHealth + " / " + MaxHealth, this);
        }

        /// <summary>
        /// Anuncia la muerte una sola vez en cada cliente.
        /// </summary>
        public override void Render()
        {
            if (!IsReady())
            {
                return;
            }
            if (IsAlive)
            {
                deathAnnounced = false;
                return;
            }
            if (deathAnnounced)
            {
                return;
            }

            deathAnnounced = true;
            Debug.Log("[PlayerHealth] Jugador muerto.", this);
        }

        /// <summary>
        /// Restaura vida solo para la reserva vigente del objetivo autorizado.
        /// </summary>
        internal bool TryRevive(SurvivalRevive reservation, int amount)
        {
            if (!IsReady() || !HasStateAuthority || IsAlive || CurrentHealth > 0f || amount <= 0)
            {
                return false;
            }
            if (reservation == null || reservation != GetComponent<SurvivalRevive>() || !reservation.CanComplete())
            {
                return false;
            }
            CurrentHealth = Mathf.Clamp(amount, 1f, MaxHealth);
            reservation.BeginGetUp();
            IsAlive = true;
            deathAnnounced = false;
            return true;
        }

        /// <summary>
        /// Cura al jugador vivo desde su autoridad sin superar la salud maxima.
        /// </summary>
        protected bool HealAlive(int amount)
        {
            if (!IsReady() || !HasStateAuthority || !IsAlive || amount <= 0 || CurrentHealth >= MaxHealth)
            {
                return false;
            }
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            return true;
        }

        /// <summary>
        /// Comprueba que Fusion haya asignado el estado del objeto.
        /// </summary>
        private bool IsReady()
        {
            if (Object == null || !Object.IsValid)
            {
                return false;
            }

            return StateBufferIsValid;
        }
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
