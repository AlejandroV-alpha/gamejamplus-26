using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Addons.SimpleKCC;

namespace Starter.ThirdPersonCharacter
{
    /// <summary>
    /// Main player scrip - controls player movement and animations.
    /// </summary>
    public sealed class Player : NetworkBehaviour
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

        [Header("Aim Rig")]
        public MultiAimConstraint AimHand;
        public TwoBoneIKConstraint RightHandGrabPistol;
        public TwoBoneIKConstraint LeftHandGrabPistol;
        public float AimWeightBlendDuration = 0.2f;

        [Networked]
        private NetworkBool _isJumping { get; set; }
        [Networked]
        private NetworkBool _isAiming { get; set; }
        [Networked]
        private NetworkBool _isSprinting { get; set; }

        private Vector3 _moveVelocity;
        private Camera _camera;
        private int _lastJumpTick = -1;
        private float _aimBlend;
        private float _aimBlendVelocity;
        private GameJam.Multiplayer.MarsFusionAvatar _avatar;

        public override void Spawned() => _avatar = GetComponent<GameJam.Multiplayer.MarsFusionAvatar>();

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            ProcessInput(PlayerInput.CurrentInput);

            if (KCC.IsGrounded)
            {
                // Stop jumping
                _isJumping = false;
            }

            PlayerInput.ResetInput();
        }

        public override void Render()
        {
            Animator.SetFloat(AnimatorId.Speed, KCC.RealSpeed, 0.15f, Time.deltaTime);
            Animator.SetFloat(AnimatorId.MotionSpeed, 1f);
            Animator.SetBool(AnimatorId.Jump, _isJumping);
            Animator.SetBool(AnimatorId.Grounded, KCC.IsGrounded && !_isJumping);
            Animator.SetBool(AnimatorId.FreeFall, KCC.RealVelocity.y < -10f);
            UpdateAimRig();
            // The astronaut clip contains anticipation; start at actual takeoff as in the previous controller.
            if (_isJumping && _lastJumpTick < 0)
            {
                Animator.Play("Airborne", 0, .34f);
                _lastJumpTick = Runner.Tick.Raw;
            }
            if (!_isJumping) _lastJumpTick = -1;
        }

        private void LateUpdate()
        {
            // Only local player needs to update the camera
            // Note: In shared mode the local player has always state authority over player's objects.
            if (HasStateAuthority == false)
                return;

            // Update camera pivot and transfer properties from camera handle to Main Camera.
            CameraPivot.rotation = Quaternion.Euler(PlayerInput.CurrentInput.LookRotation);
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            var origin = CameraPivot.position;
            var delta = CameraHandle.position - origin;
            var position = CameraHandle.position;
            if (Physics.SphereCast(origin, .15f, delta.normalized, out var hit, delta.magnitude, 1, QueryTriggerInteraction.Ignore))
                position = origin + delta.normalized * Mathf.Max(.15f, hit.distance - .08f);
            _camera.transform.SetPositionAndRotation(position, CameraHandle.rotation);
        }

        private void ProcessInput(GameplayInput input)
        {
            float jumpImpulse = 0f;

            // Comparing current input buttons to previous input buttons - this prevents glitches when input is lost
            if (input.AimToggle)
                _isAiming = !_isAiming;

            if (KCC.IsGrounded && input.Jump)
            {
                // Set world space jump vector
                jumpImpulse = JumpImpulse;
                _isJumping = true;
            }

            // It feels better when the player falls quicker
            KCC.SetGravity(-Mathf.Abs(KCC.RealVelocity.y >= 0f ? UpGravity : DownGravity));

            bool sprinting = input.Sprint && input.MoveDirection.sqrMagnitude > 0.01f && (_avatar == null || _avatar.Stamina > 0);
            _isSprinting = sprinting;
            float speed = sprinting ? SprintSpeed : WalkSpeed;

            var lookRotation = Quaternion.Euler(0f, input.LookRotation.y, 0f);
            // Calculate correct move direction from input (rotated based on camera look)
            var moveDirection = lookRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            var desiredMoveVelocity = moveDirection * speed;

            float acceleration;
            if (desiredMoveVelocity == Vector3.zero)
            {
                // No desired move velocity - we are stopping
                acceleration = KCC.IsGrounded ? GroundDeceleration : AirDeceleration;
            }
            else
            {
                // Rotate the character towards move direction over time
                var currentRotation = KCC.TransformRotation;
                var targetRotation = Quaternion.LookRotation(moveDirection);
                var nextRotation = Quaternion.Lerp(currentRotation, targetRotation, RotationSpeed * Runner.DeltaTime);

                KCC.SetLookRotation(nextRotation.eulerAngles);

                acceleration = KCC.IsGrounded ? GroundAcceleration : AirAcceleration;
            }

            _moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

            // Ensure consistent movement speed even on steep slope
            if (KCC.ProjectOnGround(_moveVelocity, out var projectedVector))
            {
                _moveVelocity = projectedVector;
            }

            KCC.Move(_moveVelocity, jumpImpulse);
        }

        private void UpdateAimRig()
        {
            bool airborne = _isJumping || !KCC.IsGrounded;
            bool aimingPose = _isAiming && !airborne && !_isSprinting;
            float duration = Mathf.Max(0.01f, AimWeightBlendDuration);
            _aimBlend = Mathf.SmoothDamp(_aimBlend, aimingPose ? 1f : 0f, ref _aimBlendVelocity, duration);
            if (AimHand != null)
                AimHand.weight = _aimBlend;
            if (LeftHandGrabPistol != null)
                LeftHandGrabPistol.weight = _aimBlend;
            if (RightHandGrabPistol != null)
                RightHandGrabPistol.weight = _aimBlend;
        }

        // Animation event
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight < 0.5f)
                return;

            if (FootstepAudioClips.Length > 0)
            {
                var index = Random.Range(0, FootstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(FootstepAudioClips[index], KCC.Position, FootstepAudioVolume);
            }
        }

        // Animation event
        private void OnLand(AnimationEvent animationEvent)
        {
            AudioSource.PlayClipAtPoint(LandingAudioClip, KCC.Position, FootstepAudioVolume);
        }
    }
}
