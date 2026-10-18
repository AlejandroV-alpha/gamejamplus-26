using Fusion;
using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// Proporciona al avatar su unica vida sincronizada y recibe dano con autoridad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : NetworkBehaviour, IPlayerHealth
    {
        [SerializeField, Min(1f)] private float startingHealth = 100f;

        // El contrato existente y Fusion requieren estas propiedades de red.
        [Networked] public float CurrentHealth { get; private set; }
        [Networked] public float MaxHealth { get; private set; }
        [Networked] public bool IsAlive { get; private set; }

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

            if (damage <= 0 || !IsAlive)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            IsAlive = CurrentHealth > 0f;
            Debug.Log("[PlayerHealth] Vida: " + CurrentHealth + " / " + MaxHealth, this);
        }

        /// <summary>
        /// Anuncia la muerte una sola vez en cada cliente.
        /// </summary>
        public override void Render()
        {
            if (!IsReady() || IsAlive)
            {
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
    }
}
