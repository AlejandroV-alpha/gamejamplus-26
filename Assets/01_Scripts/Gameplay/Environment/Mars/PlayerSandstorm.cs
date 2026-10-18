using Fusion;
using Fusion.Addons.SimpleKCC;
using GameJam.Multiplayer;
using UnityEngine;

namespace GameJam.Environment.Mars
{
    /// <summary>
    /// Modifica el movimiento autorizado durante la tormenta global.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerSandstorm : NetworkBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float movementMultiplier = 0.5f;
        [SerializeField, Min(5f)] private float gustCheckInterval = 5f;
        [SerializeField, Range(0f, 1f)] private float gustProbability = 0.15f;
        [SerializeField, Min(0f)] private float gustStrength = 1.2f;
        [SerializeField, Min(0.01f)] private float gustDuration = 0.35f;

        // Fusion requiere propiedades para conservar los intervalos de simulacion.
        [Networked] private TickTimer gustCheckTimer { get; set; }
        [Networked] private TickTimer gustEndTimer { get; set; }
        [Networked] private Vector3 gustVelocity { get; set; }

        private SimpleKCC kcc;
        private MarsFusionAvatar avatar;

        /// <summary>
        /// Obtiene los componentes existentes sin cambiar sus valores originales.
        /// </summary>
        public override void Spawned()
        {
            kcc = GetComponent<SimpleKCC>();
            avatar = GetComponent<MarsFusionAvatar>();
        }

        /// <summary>
        /// Evalua una rafaga por intervalo solamente desde la autoridad del jugador.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }
            if (!CanApplyStorm())
            {
                gustCheckTimer = TickTimer.None;
                gustEndTimer = TickTimer.None;
                gustVelocity = Vector3.zero;
                return;
            }
            if (!gustCheckTimer.IsRunning)
            {
                ScheduleGustCheck();
                return;
            }
            if (!gustCheckTimer.Expired(Runner))
            {
                return;
            }

            ScheduleGustCheck();
            if (Random.value >= gustProbability)
            {
                return;
            }

            Vector3 direction = kcc.RealVelocity;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = kcc.TransformDirection;
                direction.y = 0f;
            }

            gustVelocity = -direction.normalized * gustStrength;
            gustEndTimer = TickTimer.CreateFromSeconds(Runner, gustDuration);
            Debug.Log("[Sandstorm] Rafaga aplicada.", this);
        }

        /// <summary>
        /// Programa la siguiente evaluacion con un minimo de cinco segundos.
        /// </summary>
        private void ScheduleGustCheck()
        {
            gustCheckTimer = TickTimer.CreateFromSeconds(Runner, Mathf.Max(5f, gustCheckInterval));
        }

        /// <summary>
        /// Reduce el movimiento horizontal y suma una rafaga antes del unico Move.
        /// </summary>
        public Vector3 ModifyMovementVelocity(Vector3 velocity)
        {
            if (!HasStateAuthority || !isActiveAndEnabled)
            {
                return velocity;
            }
            if (!CanApplyStorm())
            {
                return velocity;
            }

            // Esta velocidad es locomocion sobre el suelo; salto y gravedad van separados.
            velocity *= movementMultiplier;
            if (gustEndTimer.IsRunning && !gustEndTimer.Expired(Runner))
            {
                float remaining = gustEndTimer.RemainingTime(Runner).GetValueOrDefault();
                float strength = Mathf.Clamp01(remaining / gustDuration);
                velocity += gustVelocity * strength;
                Vector3 projectedVelocity;
                if (kcc.ProjectOnGround(velocity, out projectedVelocity))
                {
                    velocity = projectedVelocity;
                }
            }

            return velocity;
        }

        /// <summary>
        /// Comprueba vida y tormenta del mismo runner sin limitar el area del mapa.
        /// </summary>
        private bool CanApplyStorm()
        {
            if (kcc == null || avatar == null)
            {
                return false;
            }
            if (!avatar.IsAlive)
            {
                return false;
            }

            SandstormSpawner storm = SandstormSpawner.GetForRunner(Runner);
            if (storm == null)
            {
                return false;
            }

            return storm.IsStormActive();
        }

        /// <summary>
        /// Mantiene parametros validos y probabilidades dentro de su intervalo.
        /// </summary>
        private void OnValidate()
        {
            movementMultiplier = Mathf.Clamp01(movementMultiplier);
            gustCheckInterval = Mathf.Max(5f, gustCheckInterval);
            gustProbability = Mathf.Clamp01(gustProbability);
            gustStrength = Mathf.Max(0f, gustStrength);
            gustDuration = Mathf.Max(0.01f, gustDuration);
        }
    }
}
