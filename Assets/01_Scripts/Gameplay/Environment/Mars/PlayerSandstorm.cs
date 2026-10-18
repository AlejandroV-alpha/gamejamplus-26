using Fusion;
using Fusion.Addons.SimpleKCC;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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
        [SerializeField, Min(0f)] private float gustStrength = 2f;
        [SerializeField, Min(0.01f)] private float gustDuration = 0.35f;

        [Header("Storm knockdown and recovery")]
        [SerializeField] private AnimationClip standUpClip;
        [SerializeField, Min(.2f)] private float fallDuration = .55f;
        [SerializeField, Min(0f)] private float airbornePushSpeed = 4.5f;
        [Networked] private byte downState { get; set; } // 0 free, 1 lying, 2 standing up
        [Networked] private TickTimer fallTimer { get; set; }
        [Networked] private TickTimer recoveryTimer { get; set; }
        private Animator animator;
        private Transform visualBody;
        private PlayableGraph recoveryGraph;
        private AnimationClipPlayable recoveryPlayable;
        private Transform[] poseBones;
        private Quaternion[] fallRotations;
        private Vector3[] fallPositions;
        private bool previousRootMotion;
        private bool warnedMissingClip;
        private Transform[] groundContacts;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private float groundOffset;
        private static readonly HumanBodyBones[] SupportBones = {
            HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.Head,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
        private static readonly float[] SupportClearance = { .14f, .15f, .13f, .035f, .035f, .04f, .04f };
        public bool IsTumbling => IsDowned; // Existing weapon/movement gates also cover recovery.
        public bool IsDowned => Object != null && Object.IsValid && avatar != null && avatar.IsAlive && downState != 0;

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
            ResolveStandUpClip();
            kcc = GetComponent<SimpleKCC>();
            avatar = GetComponent<MarsFusionAvatar>();
            animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.transform != transform) visualBody = animator.transform;
            if (animator != null && animator.isHuman)
            {
                groundContacts = new Transform[SupportBones.Length];
                for (int i = 0; i < SupportBones.Length; i++) groundContacts[i] = animator.GetBoneTransform(SupportBones[i]);
            }

        }

        private void ResolveStandUpClip()
        {
            if (standUpClip != null) return;
            standUpClip = Resources.Load<AnimationClip>("Animations/StormStandUp");
            if (standUpClip == null)
                foreach (var clip in Resources.LoadAll<AnimationClip>("Animations/StormStandUpSource"))
                    if (!clip.name.StartsWith("__preview") && clip.isHumanMotion)
                    { standUpClip = clip; break; }
#if UNITY_EDITOR
            // Resolve imported sub-assets by type, never by an assumed FBX fileID.
            if (standUpClip == null)
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/07Edu_Animations/Astronaut/FixedAnimations/RIGGED_ASTRONAUT.v2.0@Stand Up.fbx"))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview") && clip.isHumanMotion)
                    { standUpClip = clip; break; }
#endif
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
            if (avatar == null || !avatar.IsAlive)
            {
                downState = 0;
                gustEndTimer = TickTimer.None;
                return;
            }
            bool stormActive = CanApplyStorm();
            if (downState != 0)
            {
                if (stormActive)
                {
                    if (downState == 2) downState = 1;
                }
                else if (kcc != null && kcc.IsGrounded && downState == 1)
                {
                    downState = 2;
                    recoveryTimer = TickTimer.CreateFromSeconds(Runner, standUpClip != null ? standUpClip.length : 8.267f);
                    Debug.Log("[Sandstorm] Tormenta terminada: levantando astronauta.", this);
                }
                else if (downState == 2 && recoveryTimer.Expired(Runner))
                {
                    downState = 0;
                    gustEndTimer = TickTimer.None;
                }
                return;
            }
            if (!stormActive)
            {
                gustCheckTimer = TickTimer.None;
                gustEndTimer = TickTimer.None;
                gustVelocity = Vector3.zero;
                return;
            }
            if (!kcc.IsGrounded)
            {
                downState = 1;
                fallTimer = TickTimer.CreateFromSeconds(Runner, fallDuration);
                gustVelocity = new Vector3(11f, 0f, 3f).normalized * airbornePushSpeed;
                gustEndTimer = TickTimer.CreateFromSeconds(Runner, fallDuration);
                Debug.Log("[Sandstorm] Astronauta derribado; inmovil hasta el fin de la tormenta.", this);
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
            if (!CanApplyStorm() && !IsDowned) return velocity;

            // Esta velocidad es locomocion sobre el suelo; salto y gravedad van separados.
            velocity = IsDowned ? Vector3.zero : velocity * movementMultiplier;
            if (gustEndTimer.IsRunning && !gustEndTimer.Expired(Runner))
            {
                float remaining = gustEndTimer.RemainingTime(Runner).GetValueOrDefault();
                float strength = Mathf.Clamp01(remaining / (IsDowned ? fallDuration : gustDuration));
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

        // Sample the humanoid get-up clip at its lying pose while down. The same
        // replicated recovery timer drives the complete clip on every client.
        private void LateUpdate()
        {
            if (!IsDowned || animator == null || visualBody == null)
            {
                ReleaseRecoveryGraph();
                return;
            }
            if (standUpClip == null)
            {
                if (!warnedMissingClip) ResolveStandUpClip();
            }
            if (standUpClip == null)
            {
                if (!warnedMissingClip) Debug.LogError("[Sandstorm] Falta el clip Humanoid Stand Up en este prefab.", this);
                warnedMissingClip = true;
                return;
            }
            if (!recoveryGraph.IsValid())
            {
                poseBones = visualBody.GetComponentsInChildren<Transform>();
                fallRotations = new Quaternion[poseBones.Length];
                fallPositions = new Vector3[poseBones.Length];
                for (int i = 0; i < poseBones.Length; i++)
                {
                    fallRotations[i] = poseBones[i].localRotation;
                    fallPositions[i] = poseBones[i].localPosition;
                }
                previousRootMotion = animator.applyRootMotion;
                animator.applyRootMotion = false;
                recoveryGraph = PlayableGraph.Create("Storm knockdown - humanoid recovery");
                recoveryGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                recoveryPlayable = AnimationClipPlayable.Create(recoveryGraph, standUpClip);
                recoveryPlayable.SetApplyFootIK(true);
                recoveryPlayable.SetSpeed(0);
                var output = AnimationPlayableOutput.Create(recoveryGraph, "Storm pose", animator);
                output.SetSourcePlayable(recoveryPlayable);
                recoveryGraph.Play();
            }
            double time = downState == 2
                ? Mathf.Clamp(standUpClip.length - recoveryTimer.RemainingTime(Runner).GetValueOrDefault(), 0, standUpClip.length)
                : 0;
            // Remove the previous visual correction before sampling the next pose.
            // Root motion is disabled, so this offset must never accumulate.
            visualBody.localPosition = fallPositions[0];
            recoveryPlayable.SetTime(time);
            recoveryGraph.Evaluate(0);
            if (downState == 1 && fallTimer.IsRunning && !fallTimer.Expired(Runner))
            {
                float t = Mathf.SmoothStep(0, 1, 1 - fallTimer.RemainingTime(Runner).GetValueOrDefault() / fallDuration);
                for (int i = 0; i < poseBones.Length; i++)
                {
                    poseBones[i].localRotation = Quaternion.Slerp(fallRotations[i], poseBones[i].localRotation, t);
                    poseBones[i].localPosition = Vector3.Lerp(fallPositions[i], poseBones[i].localPosition, t);
                }
            }
            GroundRecoveryPose();
        }
        private void GroundRecoveryPose()
        {
            if (groundContacts == null || kcc == null || !kcc.IsGrounded) return;
            float correction = float.NegativeInfinity;
            float rootY = transform.position.y;
            for (int i = 0; i < groundContacts.Length; i++)
            {
                if (groundContacts[i] == null) continue;
                Vector3 point = groundContacts[i].position;
                Vector3 origin = new Vector3(point.x, rootY + 2f, point.z);
                int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 5f, 1, QueryTriggerInteraction.Ignore);
                float highest = float.NegativeInfinity;
                for (int j = 0; j < count; j++)
                {
                    var hit = groundHits[j];
                    if (hit.collider.transform.IsChildOf(transform) || hit.normal.y < .35f
                        || hit.point.y > rootY + .6f) continue;
                    highest = Mathf.Max(highest, hit.point.y);
                }
                if (float.IsNegativeInfinity(highest)) continue;
                correction = Mathf.Max(correction, highest + SupportClearance[i] - point.y);
            }
            if (float.IsNegativeInfinity(correction)) return;
            groundOffset = Mathf.MoveTowards(groundOffset, Mathf.Clamp(correction, -1.5f, .6f), Time.deltaTime * 8f);
            visualBody.position += Vector3.up * groundOffset;
        }
        private void ReleaseRecoveryGraph()
        {
            if (!recoveryGraph.IsValid()) return;
            recoveryGraph.Destroy();
            groundOffset = 0;
            animator.applyRootMotion = previousRootMotion;
            // Restore only the model root; the locomotion controller resumes bone animation.
            if (poseBones != null && poseBones.Length > 0 && poseBones[0] != null)
            {
                poseBones[0].localRotation = fallRotations[0];
                poseBones[0].localPosition = fallPositions[0];
            }
        }
        private void OnDisable() => ReleaseRecoveryGraph();
        private void OnDestroy() => ReleaseRecoveryGraph();

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
