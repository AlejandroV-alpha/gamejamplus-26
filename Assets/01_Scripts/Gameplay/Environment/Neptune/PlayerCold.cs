
using Fusion;
using GameJam.Combat;
using UnityEngine;

namespace GameJam.Environment.Neptune
{
    /// <summary>
    /// Simula el frio individual y aplica dano desde la autoridad del jugador.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerCold : NetworkBehaviour
    {
        [Header("Frio")]
        [SerializeField, Min(1f)] private float maxCold = 100f;
        [SerializeField, Min(0f)] private float coldIncreaseRate = 2.5f;

        [Header("Dano")]
        [SerializeField, Range(0f, 100f)] private float damageThreshold = 80f;
        [SerializeField, Min(1)] private int coldDamage = 5;
        [SerializeField, Min(0.01f)] private float damageInterval = 2f;

        // Fusion requiere propiedades para almacenar el estado sincronizado.
        [Networked] private float currentCold { get; set; }
        [Networked] private float damageElapsed { get; set; }

        private PlayerHealth playerHealth;

        /// <summary>
        /// Obtiene la salud existente e inicia el frio desde su autoridad.
        /// </summary>
        public override void Spawned()
        {
            playerHealth = GetComponent<PlayerHealth>();

            if (HasStateAuthority)
            {
                currentCold = 0f;
                damageElapsed = 0f;
            }
        }

        /// <summary>
        /// Actualiza frio y dano una vez por paso de simulacion autorizado.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            if (playerHealth == null)
            {
                return;
            }

            if (!playerHealth.HasStateAuthority)
            {
                return;
            }

            if (!playerHealth.IsAlive)
            {
                damageElapsed = 0f;
                return;
            }

            float heatRate = HeatSource.GetStrongestHeatRate(
                transform.position,
                gameObject.scene
            );

            float coldChange = coldIncreaseRate;

            if (heatRate > 0f)
            {
                coldChange = -heatRate;
            }

            // Las tasas se expresan en puntos porcentuales por segundo.
            float coldUnitsPerSecond = coldChange * maxCold / 100f;

            currentCold = Mathf.Clamp(
                currentCold + coldUnitsPerSecond * Runner.DeltaTime,
                0f,
                maxCold
            );

            UpdateColdDamage();
        }

        /// <summary>
        /// Aplica dano periodico mientras el frio supera el umbral.
        /// </summary>
        private void UpdateColdDamage()
        {
            if (GetColdPercentage() < damageThreshold)
            {
                damageElapsed = 0f;
                return;
            }

            damageElapsed += Runner.DeltaTime;

            if (damageElapsed < damageInterval)
            {
                return;
            }

            damageElapsed -= damageInterval;

            playerHealth.TakeDamage(coldDamage);
        }

        /// <summary>
        /// Consulta la cantidad de frio sincronizada del astronauta.
        /// </summary>
        public float GetCurrentCold()
        {
            if (!IsReady())
            {
                return 0f;
            }

            return currentCold;
        }

        /// <summary>
        /// Consulta el frio como porcentaje entre cero y cien.
        /// </summary>
        public float GetColdPercentage()
        {
            return GetCurrentCold() * 100f / Mathf.Max(1f, maxCold);
        }

        /// <summary>
        /// Indica si el jugador vivo se encuentra en el umbral de dano periodico.
        /// </summary>
        public bool IsTakingColdDamage()
        {
            if (!IsReady())
            {
                return false;
            }

            if (playerHealth == null)
            {
                return false;
            }

            if (!playerHealth.IsAlive)
            {
                return false;
            }

            return GetColdPercentage() >= damageThreshold;
        }

        /// <summary>
        /// Comprueba que Fusion haya asignado estado al astronauta.
        /// </summary>
        private bool IsReady()
        {
            if (Object == null)
            {
                return false;
            }

            if (!Object.IsValid)
            {
                return false;
            }

            return StateBufferIsValid;
        }

        /// <summary>
        /// Mantiene parametros validos para la simulacion del frio.
        /// </summary>
        private void OnValidate()
        {
            maxCold = Mathf.Max(1f, maxCold);
            coldIncreaseRate = Mathf.Max(0f, coldIncreaseRate);
            damageThreshold = Mathf.Clamp(damageThreshold, 0f, 100f);
            coldDamage = Mathf.Max(1, coldDamage);
            damageInterval = Mathf.Max(0.01f, damageInterval);
        }
    }
}
