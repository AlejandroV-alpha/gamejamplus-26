using FirstGearGames.SmoothCameraShaker;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using Fusion;
using Fusion.Addons.SimpleKCC;

namespace Starter.ThirdPersonCharacter
{
    /// <summary>
    /// Main player scrip - controls player movement and animations.
    /// </summary>
    public class Player : NetworkBehaviour
    {
        [Header("References")]
        public SimpleKCC KCC;
        public PlayerInput PlayerInput;
        public Animator Animator;
        public Transform CameraPivot;
        public Transform CameraHandle;
        public Transform ShoulderCameraHandle;
        public GameObject CrosshairPrefab;

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
        public MultiAimConstraint AimBody;
        public TwoBoneIKConstraint RightHandGrabPistol;
        public TwoBoneIKConstraint LeftHandGrabPistol;
        public float AimWeightBlendDuration = 0.2f;
        public float AimCameraBlendDuration = 0.45f;

        [Networked]
        private NetworkBool _isJumping { get; set; }
        [Networked]
        private NetworkBool _isAiming { get; set; }
        [Networked]
        private NetworkBool _isSprinting { get; set; }

        private Vector3 _moveVelocity;
        private int _lastJumpTick = -1;
        private float _aimBlend;
        private float _aimBlendVelocity;
        private CinemachineCamera _centerCamera;
        private CinemachineCamera _shoulderCamera;
        private CinemachineFollow _centerFollow;
        private CinemachineFollow _shoulderFollow;
        private CinemachineBrain _brain;
        private CinemachineBlendDefinition _previousBlend;
        private bool _blendOverridden;
        private GameObject _crosshair;
        private bool _ownsCrosshair;
        private const float CrosshairAimThreshold = 0.85f;
        private GameJam.Multiplayer.MarsFusionAvatar _avatar;
        private GameJam.Environment.Mars.PlayerSandstorm _sandstorm;
        private int _handsLayer = -1;

        public override void Spawned()
        {
            _avatar = GetComponent<GameJam.Multiplayer.MarsFusionAvatar>();
            _sandstorm = GetComponent<GameJam.Environment.Mars.PlayerSandstorm>();
            if (Animator != null)
            {
                _handsLayer = Animator.GetLayerIndex("HandsLayer");
                if (_handsLayer >= 0)
                    Animator.SetLayerWeight(_handsLayer, 0f);
            }
            if (HasStateAuthority)
                SetupLocalCameras();
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => DestroyLocalCameras();

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            var movementInput = PrepareMovementInput(PlayerInput.CurrentInput);
            if ((_sandstorm != null && _sandstorm.IsTumbling) || (GetComponent<GameJam.Survival.SurvivalRevive>()?.IsAnimating ?? false))
            {
                movementInput.MoveDirection = Vector2.zero;
                movementInput.Jump = false;
                movementInput.Sprint = false;
                movementInput.AimToggle = false;
                _isAiming = false;
            }
            ProcessInput(movementInput);

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
            if (HasStateAuthority)
                UpdateLocalCamera();
            // The astronaut clip contains anticipation; start at actual takeoff as in the previous controller.
            if (_isJumping && _lastJumpTick < 0)
            {
                Animator.Play("Airborne", 0, .34f);
                _lastJumpTick = Runner.Tick.Raw;
            }
            if (!_isJumping) _lastJumpTick = -1;
        }

        private void UpdateLocalCamera()
        {
            if (_centerCamera == null)
                SetupLocalCameras();
            if (CameraPivot != null)
                CameraPivot.rotation = Quaternion.Euler(PlayerInput.CurrentInput.LookRotation);
            if (_centerFollow != null && CameraHandle != null)
                _centerFollow.FollowOffset = CameraHandle.localPosition;
            if (_shoulderFollow != null && ShoulderCameraHandle != null)
                _shoulderFollow.FollowOffset = ShoulderCameraHandle.localPosition;
            if (_shoulderCamera != null)
                _shoulderCamera.Priority = IsAimingPose() ? 20 : 0;
            UpdateCrosshair();
        }

        private void SetupLocalCameras()
        {
            if (CameraPivot == null)
                return;
            var main = Camera.main;
            if (main == null)
                return;

            _brain = main.GetComponent<CinemachineBrain>();
            if (_brain == null)
                _brain = main.gameObject.AddComponent<CinemachineBrain>();
            if (main.GetComponent<CameraShaker>() == null)
                main.gameObject.AddComponent<CameraShaker>();
            if (!_blendOverridden)
            {
                _previousBlend = _brain.DefaultBlend;
                _blendOverridden = true;
            }
            _brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                Mathf.Max(0.01f, AimCameraBlendDuration));

            var rest = CameraHandle != null ? CameraHandle.localPosition : new Vector3(0f, 0.25f, -4.5f);
            var shoulder = ShoulderCameraHandle != null ? ShoulderCameraHandle.localPosition : rest;
            _centerCamera = CreateVirtualCamera("LocalCenterCamera", rest, 10, out _centerFollow);
            _shoulderCamera = CreateVirtualCamera("LocalShoulderCamera", shoulder, 0, out _shoulderFollow);
        }

        private CinemachineCamera CreateVirtualCamera(string name, Vector3 followOffset, int priority, out CinemachineFollow follow)
        {
            var cameraObject = new GameObject(name);
            var virtualCamera = cameraObject.AddComponent<CinemachineCamera>();
            virtualCamera.Follow = CameraPivot;
            virtualCamera.LookAt = CameraPivot;
            virtualCamera.Priority = priority;

            var followComponent = cameraObject.AddComponent<CinemachineFollow>();
            var tracker = TrackerSettings.Default;
            tracker.BindingMode = BindingMode.LockToTarget;
            tracker.PositionDamping = Vector3.zero;
            tracker.RotationDamping = Vector3.zero;
            followComponent.TrackerSettings = tracker;
            followComponent.FollowOffset = followOffset;
            follow = followComponent;

            var rotate = cameraObject.AddComponent<CinemachineRotateWithFollowTarget>();
            rotate.Damping = 0f;

            var deoccluder = cameraObject.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = 1;
            deoccluder.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
            {
                Enabled = true,
                DistanceLimit = 0f,
                MinimumOcclusionTime = 0f,
                CameraRadius = 0.15f,
                Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward,
                MaximumEffort = 4,
                SmoothingTime = 0f,
                Damping = 0.4f,
                DampingWhenOccluded = 0.2f
            };
            return virtualCamera;
        }

        private void DestroyLocalCameras()
        {
            if (_centerCamera != null)
                Destroy(_centerCamera.gameObject);
            if (_shoulderCamera != null)
                Destroy(_shoulderCamera.gameObject);
            _centerCamera = null;
            _shoulderCamera = null;
            _centerFollow = null;
            _shoulderFollow = null;
            if (_blendOverridden && _brain != null)
                _brain.DefaultBlend = _previousBlend;
            _blendOverridden = false;
            _brain = null;
            if (_crosshair != null)
            {
                if (_ownsCrosshair) Destroy(_crosshair);
                else _crosshair.SetActive(false);
            }
            _crosshair = null;
            _ownsCrosshair = false;
        }

        private void UpdateCrosshair()
        {
            if (_crosshair == null && CrosshairPrefab != null)
            {
                _crosshair = Instantiate(CrosshairPrefab);
                _crosshair.name = "Local Player Crosshair";
                DontDestroyOnLoad(_crosshair);
                _ownsCrosshair = true;
            }
            if (_crosshair == null)
            {
                var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var canvas in canvases)
                {
                    if (canvas.gameObject.name != "CrosshairCanvas")
                        continue;
                    _crosshair = canvas.gameObject;
                    break;
                }
            }

            if (_crosshair != null)
                _crosshair.SetActive(_aimBlend >= CrosshairAimThreshold && PlayerInput.ControlsEnabled);
        }

        /// <summary>
        /// Conserva la entrada original antes de simular el movimiento.
        /// </summary>
        protected virtual GameplayInput PrepareMovementInput(GameplayInput input)
        {
            return input;
        }

        /// <summary>
        /// Conserva las condiciones originales para permitir correr.
        /// </summary>
        protected virtual bool ResolveSprint(GameplayInput input)
        {
            return input.Sprint && input.MoveDirection.sqrMagnitude > 0.01f && (_avatar == null || _avatar.Stamina > 0);
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

            bool sprinting = ResolveSprint(input);
            _isSprinting = sprinting;
            float speed = sprinting ? SprintSpeed : WalkSpeed;

            var lookRotation = Quaternion.Euler(0f, input.LookRotation.y, 0f);
            // Calculate correct move direction from input (rotated based on camera look)
            var moveDirection = lookRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            var desiredMoveVelocity = moveDirection * speed;

            float acceleration;
            if (IsAimingPose())
            {
                // Face the camera yaw only. Pitch stays on the camera, not on the body.
                var targetRotation = Quaternion.Euler(0f, input.LookRotation.y, 0f);
                var nextRotation = Quaternion.Lerp(KCC.TransformRotation, targetRotation, RotationSpeed * Runner.DeltaTime);
                KCC.SetLookRotation(new Vector3(0f, nextRotation.eulerAngles.y, 0f));
                acceleration = desiredMoveVelocity == Vector3.zero
                    ? (KCC.IsGrounded ? GroundDeceleration : AirDeceleration)
                    : (KCC.IsGrounded ? GroundAcceleration : AirAcceleration);
            }
            else if (desiredMoveVelocity == Vector3.zero)
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

            var finalVelocity = _sandstorm != null ? _sandstorm.ModifyMovementVelocity(_moveVelocity) : _moveVelocity;
            if (_avatar != null) finalVelocity = _avatar.ApplyFriendlyPush(finalVelocity);
            KCC.Move(finalVelocity, jumpImpulse);
        }

        // Match firing permission to the visible, settled aiming pose.
        public bool IsAimReady => IsAimingPose() && _aimBlend >= CrosshairAimThreshold
            && PlayerInput != null && PlayerInput.ControlsEnabled;

        private bool IsAimingPose()
        {
            return !(GetComponent<GameJam.Survival.SurvivalRevive>()?.IsAnimating ?? false) && (_avatar==null || _avatar.IsAlive) && !(GetComponent<GameJam.Combat.PlayerHurtReaction>()?.IsReacting ?? false) && _isAiming && !_isSprinting && KCC.IsGrounded && !_isJumping && !(GetComponent<GameJam.Multiplayer.PlayerInteractionAction>()?.IsBusy ?? false);
        }

        private void UpdateAimRig()
        {
            bool aimingPose = IsAimingPose();
            float duration = Mathf.Max(0.01f, AimWeightBlendDuration);
            _aimBlend = Mathf.SmoothDamp(_aimBlend, aimingPose ? 1f : 0f, ref _aimBlendVelocity, duration);
            if (AimHand != null)
                AimHand.weight = _aimBlend;
            if (AimBody != null)
                AimBody.weight = _aimBlend;
            if (LeftHandGrabPistol != null)
                LeftHandGrabPistol.weight = _aimBlend;
            if (RightHandGrabPistol != null)
                RightHandGrabPistol.weight = _aimBlend;
            if (Animator != null && _handsLayer >= 0)
                Animator.SetLayerWeight(_handsLayer, _aimBlend);
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
