using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace GameJam.Environment.Mars
{
    /// <summary>
    /// Sincroniza el ciclo global de tormentas del planeta.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class SandstormSpawner : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float minSpawnInterval = 45f;
        [SerializeField, Min(0.1f)] private float maxSpawnInterval = 75f;
        [SerializeField, Min(0.1f)] private float stormDuration = 20f;
        [SerializeField] private GameObject visual;

        // Fusion requiere propiedades para sincronizar el estado persistente.
        [Networked] private NetworkBool stormActive { get; set; }
        [Networked] private TickTimer phaseTimer { get; set; }

        private static readonly HashSet<SandstormSpawner> activeSpawners = new HashSet<SandstormSpawner>();
        private bool visualStormActive;

        /// <summary>
        /// Registra el evento sin depender de referencias manuales en los jugadores.
        /// </summary>
        private void OnEnable()
        {
            activeSpawners.Add(this);
        }

        /// <summary>
        /// Retira el evento y oculta su representacion al desactivarse.
        /// </summary>
        private void OnDisable()
        {
            activeSpawners.Remove(this);
            if (visual != null)
            {
                visual.SetActive(false);
            }
            visualStormActive = false;
        }

        /// <summary>
        /// Inicializa el intervalo desde la autoridad y muestra el estado recibido.
        /// </summary>
        public override void Spawned()
        {
            if (HasStateAuthority && !phaseTimer.IsRunning)
            {
                stormActive = false;
                ScheduleNextStorm();
            }

            UpdateVisual();
        }

        /// <summary>
        /// Cambia de fase solo cuando expira el temporizador de la autoridad.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }
            if (!phaseTimer.Expired(Runner))
            {
                return;
            }

            if (stormActive)
            {
                stormActive = false;
                ScheduleNextStorm();
            }
            else
            {
                stormActive = true;
                phaseTimer = TickTimer.CreateFromSeconds(Runner, stormDuration);
            }
        }

        /// <summary>
        /// Elige el siguiente intervalo solamente desde la autoridad del evento.
        /// </summary>
        [ContextMenu("Iniciar tormenta ahora (Play, autoridad)")]
        private void StartStormFromInspector() { TryStartStorm(); }

        // Call on the storm authority; no coroutine or extra network messages per particle.
        public bool TryStartStorm()
        {
            if (!IsReady() || !HasStateAuthority) return false;
            stormActive = true;
            phaseTimer = TickTimer.CreateFromSeconds(Runner, stormDuration);
            return true;
        }

        private void ScheduleNextStorm()
        {
            float interval = Random.Range(minSpawnInterval, maxSpawnInterval);
            phaseTimer = TickTimer.CreateFromSeconds(Runner, interval);
        }

        /// <summary>
        /// Actualiza la representacion local usando el mismo estado sincronizado.
        /// </summary>
        public override void Render()
        {
            UpdateVisual();
        }

        /// <summary>
        /// Muestra el cilindro y anuncia solamente los cambios de fase.
        /// </summary>
        private void UpdateVisual()
        {
            bool active = IsStormActive();
            if (visual != null)
            {
                visual.SetActive(active);
            }
            if (active == visualStormActive)
            {
                return;
            }

            visualStormActive = active;
            if (active)
            {
                Debug.Log("[Sandstorm] Tormenta iniciada.", this);
            }
            else
            {
                Debug.Log("[Sandstorm] Tormenta finalizada.", this);
            }
        }

        /// <summary>
        /// Consulta el estado global incluyendo jugadores que se unen tarde.
        /// </summary>
        public bool IsStormActive()
        {
            if (!IsReady())
            {
                return false;
            }

            return stormActive;
        }

        /// <summary>
        /// Devuelve los segundos restantes de tormenta o cero si no esta activa.
        /// </summary>
        public float GetRemainingStormTime()
        {
            if (!IsStormActive())
            {
                return 0f;
            }

            return phaseTimer.RemainingTime(Runner).GetValueOrDefault();
        }

        /// <summary>
        /// Encuentra la tormenta del mismo runner sin usar distancias ni colliders.
        /// </summary>
        public static SandstormSpawner GetForRunner(NetworkRunner runner)
        {
            foreach (SandstormSpawner spawner in activeSpawners)
            {
                if (spawner == null || !spawner.IsReady())
                {
                    continue;
                }
                if (spawner.Runner == runner)
                {
                    return spawner;
                }
            }

            return null;
        }

        /// <summary>
        /// Comprueba que exista estado de red antes de consultarlo.
        /// </summary>
        private bool IsReady()
        {
            if (Object == null || !Object.IsValid)
            {
                return false;
            }

            return StateBufferIsValid;
        }

        /// <summary>
        /// Mantiene intervalos positivos y ordenados en el Inspector.
        /// </summary>
        private void OnValidate()
        {
            minSpawnInterval = Mathf.Max(0.1f, minSpawnInterval);
            maxSpawnInterval = Mathf.Max(minSpawnInterval, maxSpawnInterval);
            stormDuration = Mathf.Max(0.1f, stormDuration);
        }
    }
}
