using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using Starter;
using Starter.ThirdPersonCharacter;

namespace GameJam.Environment.Mars
{
    /// <summary>
    /// Copia propia del movimiento para integrar tormentas sin alterar el original.
    /// </summary>
    public sealed class SandstormPlayerController : NetworkBehaviour
    {
        [Header("References")]
        public SimpleKCC KCC;
        public PlayerInput PlayerInput;
        public Animator Animator;
        public Transform CameraPivot;
        public Transform CameraHandle;

        [Header("Movement Setup")]
        public float WalkSpeed = 2f;
        public float SprintSpeed = 5f;
        public float JumpImpulse = 10f;
        public float UpGravity = -25f;
        public float DownGravity = -40f;
        public float RotationSpeed = 8f;

        [Header("Movement Accelerations")]
        public float GroundAcceleration = 55f;
        public float GroundDeceleration = 25f;
        public float AirAcceleration = 25f;
        public float AirDeceleration = 1.3f;

        [Header("Sounds")]
        public AudioClip[] FootstepAudioClips;
        public AudioClip LandingAudioClip;
        [Range(0f, 1f)]
        public float FootstepAudioVolume = 0.5f;

        [Networked]
        private NetworkBool isJumping { get; set; }

        private Vector3 moveVelocity;
        private Camera aimCamera;
        private int lastJumpTick = -1;
        private GameJam.Multiplayer.MarsFusionAvatar avatar;

        private PlayerSandstorm sandstorm;

        /// <summary>
        /// Obtiene el avatar y el modificador propio de tormenta.
        /// </summary>
        public override void Spawned()
        {
            avatar = GetComponent<GameJam.Multiplayer.MarsFusionAvatar>();
            sandstorm = GetComponent<PlayerSandstorm>();
        }

        /// <summary>
        /// Simula el movimiento local y consume la entrada existente.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }
            var input = PlayerInput.CurrentInput;
            if (!PlayerInput.ControlsEnabled || (avatar != null && !avatar.IsAlive))
            {
                input.MoveDirection = Vector2.zero;
                input.Sprint = false;
                input.Jump = false;
                input.AimToggle = false;
            }
            ProcessInput(input);

            if (KCC.IsGrounded)
            {
                isJumping = false;
            }

            PlayerInput.ResetInput();
        }

        /// <summary>
        /// Actualiza las animaciones usando el movimiento sincronizado.
        /// </summary>
        public override void Render()
        {
            Animator.SetFloat(AnimatorId.Speed, KCC.RealSpeed, 0.15f, Time.deltaTime);
            Animator.SetFloat(AnimatorId.MotionSpeed, 1f);
            Animator.SetBool(AnimatorId.Jump, isJumping);
            Animator.SetBool(AnimatorId.Grounded, KCC.IsGrounded && !isJumping);
            Animator.SetBool(AnimatorId.FreeFall, KCC.RealVelocity.y < -10f);
            if (isJumping && lastJumpTick < 0)
            {
                Animator.Play("Airborne", 0, .34f);
                lastJumpTick = Runner.Tick.Raw;
            }
            if (!isJumping)
            {
                lastJumpTick = -1;
            }
        }

        /// <summary>
        /// Actualiza la camara del jugador local y evita atravesar paredes.
        /// </summary>
        private void LateUpdate()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            CameraPivot.rotation = Quaternion.Euler(PlayerInput.CurrentInput.LookRotation);
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
            if (aimCamera == null)
            {
                return;
            }
            Vector3 origin = CameraPivot.position;
            Vector3 delta = CameraHandle.position - origin;
            Vector3 position = CameraHandle.position;
            RaycastHit hit;
            if (Physics.SphereCast(origin, .15f, delta.normalized, out hit, delta.magnitude, 1, QueryTriggerInteraction.Ignore))
            {
                position = origin + delta.normalized * Mathf.Max(.15f, hit.distance - .08f);
            }
            aimCamera.transform.SetPositionAndRotation(position, CameraHandle.rotation);
        }

        /// <summary>
        /// Conserva el movimiento original y aplica la tormenta antes de Move.
        /// </summary>
        private void ProcessInput(GameplayInput input)
        {
            float jumpImpulse = 0f;

            if (KCC.IsGrounded && input.Jump)
            {
                jumpImpulse = JumpImpulse;
                isJumping = true;
            }

            float gravity = DownGravity;
            if (KCC.RealVelocity.y >= 0f)
            {
                gravity = UpGravity;
            }
            KCC.SetGravity(-Mathf.Abs(gravity));

            bool canSprint = true;
            if (avatar != null)
            {
                canSprint = avatar.Stamina > 0f;
            }
            var survivalAvatar = avatar as GameJam.Survival.SurvivalAvatar;
            bool sprinting = survivalAvatar != null
                ? survivalAvatar.SimulateSprint(input.Sprint, input.MoveDirection.sqrMagnitude > 0.01f)
                : input.Sprint && canSprint;
            float speed = WalkSpeed;
            if (sprinting)
            {
                speed = SprintSpeed;
            }

            Quaternion lookRotation = Quaternion.Euler(0f, input.LookRotation.y, 0f);
            Vector3 moveDirection = lookRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            Vector3 desiredMoveVelocity = moveDirection * speed;

            float acceleration;
            if (desiredMoveVelocity == Vector3.zero)
            {
                acceleration = AirDeceleration;
                if (KCC.IsGrounded)
                {
                    acceleration = GroundDeceleration;
                }
            }
            else
            {
                Quaternion currentRotation = KCC.TransformRotation;
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                Quaternion nextRotation = Quaternion.Lerp(currentRotation, targetRotation, RotationSpeed * Runner.DeltaTime);

                KCC.SetLookRotation(nextRotation.eulerAngles);

                acceleration = AirAcceleration;
                if (KCC.IsGrounded)
                {
                    acceleration = GroundAcceleration;
                }
            }

            moveVelocity = Vector3.Lerp(moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

            Vector3 projectedVector;
            if (KCC.ProjectOnGround(moveVelocity, out projectedVector))
            {
                moveVelocity = projectedVector;
            }

            // La velocidad interna conserva su valor normal entre tormentas.
            Vector3 finalVelocity = moveVelocity;
            if (sandstorm != null)
            {
                finalVelocity = sandstorm.ModifyMovementVelocity(finalVelocity);
            }

            // Una sola llamada conserva la gravedad, el salto y las colisiones.
            KCC.Move(finalVelocity, jumpImpulse);
        }

        /// <summary>
        /// Reproduce el sonido de pasos con los clips existentes.
        /// </summary>
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight < 0.5f)
            {
                return;
            }

            if (FootstepAudioClips.Length > 0)
            {
                int index = Random.Range(0, FootstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(FootstepAudioClips[index], KCC.Position, FootstepAudioVolume);
            }
        }

        /// <summary>
        /// Reproduce el sonido de aterrizaje existente.
        /// </summary>
        private void OnLand(AnimationEvent animationEvent)
        {
            AudioSource.PlayClipAtPoint(LandingAudioClip, KCC.Position, FootstepAudioVolume);
        }
    }
}
