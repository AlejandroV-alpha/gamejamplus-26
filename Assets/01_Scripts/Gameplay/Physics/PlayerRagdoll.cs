using System;
using System.Collections.Generic;
using Fusion.Addons.SimpleKCC;
using GameJam.Multiplayer;
using Starter.ThirdPersonCharacter;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace GameJam.PhysicsControl
{
    /// <summary>
    /// Sistema de ragdoll físico para el astronauta al estilo Lethal Company / REPO / Content Warning.
    /// Mientras el jugador camina, los cuerpos rígidos están en kinematic y los colliders apagados (cero coste de física).
    /// Al recibir un empujón fuerte, bomba o pulsar [Y] (Flop), el cuerpo entra en física activa con impulsos,
    /// cayendo y rodando por las dunas marcianas, con la cámara siguiéndolo de forma suave.
    /// Al levantarse, los huesos realizan una transición suave de rigidez muscular progresiva desde la pose caída
    /// hasta la postura de locomoción estándar, evitando cualquier chasquido o artefacto visual.
    /// </summary>
    [DefaultExecutionOrder(12200)]
    [DisallowMultipleComponent]
    public sealed class PlayerRagdoll : MonoBehaviour
    {
        private struct BoneDef
        {
            public HumanBodyBones bone;
            public HumanBodyBones child;
            public float radius;
            public float mass;
            public float swing;
            public float twist;
            public bool isSphere;

            public BoneDef(HumanBodyBones b, HumanBodyBones c, float r, float m, float s, float t, bool sphere = false)
            {
                bone = b; child = c; radius = r; mass = m; swing = s; twist = t; isSphere = sphere;
            }
        }

        private static readonly BoneDef[] HumanoidBones =
        {
            new BoneDef(HumanBodyBones.Hips, HumanBodyBones.Spine, 0.17f, 24f, 0f, 0f),
            new BoneDef(HumanBodyBones.Spine, HumanBodyBones.Chest, 0.16f, 16f, 20f, 10f),
            new BoneDef(HumanBodyBones.Chest, HumanBodyBones.Head, 0.18f, 18f, 20f, 10f),
            new BoneDef(HumanBodyBones.Head, HumanBodyBones.LastBone, 0.20f, 10f, 35f, 20f, true),
            new BoneDef(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.075f, 4f, 65f, 25f),
            new BoneDef(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.065f, 3f, 75f, 10f),
            new BoneDef(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.075f, 4f, 65f, 25f),
            new BoneDef(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.065f, 3f, 75f, 10f),
            new BoneDef(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.095f, 8f, 50f, 15f),
            new BoneDef(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.08f, 5f, 70f, 5f),
            new BoneDef(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.095f, 8f, 50f, 15f),
            new BoneDef(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.08f, 5f, 70f, 5f),
        };

        private readonly List<Rigidbody> rigidbodies = new List<Rigidbody>();
        private readonly List<Collider> colliders = new List<Collider>();
        private readonly Dictionary<Transform, Rigidbody> boneToRb = new Dictionary<Transform, Rigidbody>();

        private Transform[] allBones;
        private Vector3[] snapshotPositions;
        private Quaternion[] snapshotRotations;
        private int hipsBoneIndex = -1;

        private Animator animator;
        private SimpleKCC kcc;
        private MarsFusionAvatar avatar;
        private Starter.ThirdPersonCharacter.Player player;
        private RigBuilder rigBuilder;
        private Transform cameraPivot;
        private Vector3 cameraPivotLocalRest;
        private Vector3 snapshotCameraPivotLocalPos;
        private Transform hipsTransform;
        public Rigidbody HipsRigidbody { get; private set; }

        public bool IsRagdolled { get; private set; }
        public bool IsRecovering { get; private set; }
        public float RagdollStartTime { get; private set; }
        public float MinRagdollDuration { get; set; } = 1.3f;

        private float recoveryStartTime;
        private const float RecoveryDuration = 0.40f;

        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private Vector3[] rbPos;
        private Quaternion[] rbRot;

        /// <summary>Punto del suelo donde se levanto el cuerpo en el ultimo StandUp.</summary>
        public Vector3 LastStandPosition { get; private set; }

        /// <summary>Posicion real del cuerpo (caderas en ragdoll, raiz si no).</summary>
        public Vector3 BodyPosition => IsRagdolled && hipsTransform != null ? hipsTransform.position : transform.position;

        // Mientras el cuerpo rueda, la raiz del jugador (lo que apuntan los monstruos, el daño y la red)
        // se quedaba donde lo tumbaron. El esqueleto se suelta al mundo durante el ragdoll, asi la raiz
        // puede seguir a las caderas sin tocar la fisica de los huesos (el ragdoll se mueve igual que antes).
        private Transform hipsParent;
        private int hipsSibling;
        private bool hipsDetached;

        private void FixedUpdate()
        {
            if (!IsRagdolled || !hipsDetached || hipsTransform == null) return;
            Vector3 pivotWorld = cameraPivot != null ? cameraPivot.position : Vector3.zero;
            transform.position = GroundBelow(hipsTransform.position);
            if (cameraPivot != null) cameraPivot.position = pivotWorld;
        }

        private void DetachSkeleton()
        {
            if (hipsDetached || hipsTransform == null || hipsTransform.parent == null) return;
            hipsParent = hipsTransform.parent;
            hipsSibling = hipsTransform.GetSiblingIndex();
            hipsTransform.SetParent(null, true);
            hipsDetached = true;
        }

        private void ReattachSkeleton()
        {
            if (!hipsDetached || hipsTransform == null) return;
            hipsDetached = false;
            if (hipsParent == null) return;
            hipsTransform.SetParent(hipsParent, true);
            hipsTransform.SetSiblingIndex(hipsSibling);
        }

        private Vector3 GroundBelow(Vector3 point)
        {
            Vector3 ground = point + Vector3.down * .9f;
            int hits = Physics.RaycastNonAlloc(point + Vector3.up * 1.5f, Vector3.down, groundHits, 4.5f, ~0, QueryTriggerInteraction.Ignore);
            float highest = float.NegativeInfinity;
            for (int i = 0; i < hits; i++)
            {
                var h = groundHits[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.point.y > highest) { highest = h.point.y; ground = h.point; }
            }
            return ground;
        }

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            kcc = GetComponent<SimpleKCC>();
            avatar = GetComponent<MarsFusionAvatar>();
            player = GetComponent<Starter.ThirdPersonCharacter.Player>();
            rigBuilder = GetComponentInChildren<RigBuilder>(true);

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CameraPivot")
                {
                    cameraPivot = t;
                    cameraPivotLocalRest = t.localPosition;
                    break;
                }
            }

            BuildRagdollHierarchy();
        }

        private void BuildRagdollHierarchy()
        {
            if (animator == null || !animator.isHuman) return;

            // Make sure skinned meshes don't pop out of view when tumbling
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);

            var kccCollider = GetComponent<Collider>();

            foreach (var def in HumanoidBones)
            {
                var bone = animator.GetBoneTransform(def.bone);
                if (bone == null) continue;

                if (def.bone == HumanBodyBones.Hips)
                {
                    hipsTransform = bone;
                }

                var rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = def.mass;
                rb.linearDamping = 0.15f;
                rb.angularDamping = 1.4f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;

                Collider col;
                if (def.isSphere)
                {
                    var sc = bone.gameObject.AddComponent<SphereCollider>();
                    sc.radius = def.radius;
                    sc.center = Vector3.up * 0.12f;
                    col = sc;
                }
                else
                {
                    Vector3 local = Vector3.up * 0.25f;
                    if (def.child != HumanBodyBones.LastBone)
                    {
                        var child = animator.GetBoneTransform(def.child);
                        if (child != null) local = bone.InverseTransformPoint(child.position);
                    }
                    int axis = DominantAxis(local);
                    var cc = bone.gameObject.AddComponent<CapsuleCollider>();
                    cc.direction = axis;
                    cc.radius = def.radius;
                    cc.height = Mathf.Max(cc.radius * 2f, Mathf.Abs(local[axis]) + cc.radius);
                    cc.center = local * 0.5f;
                    col = cc;
                }

                col.enabled = false;
                colliders.Add(col);
                rigidbodies.Add(rb);
                boneToRb[bone] = rb;

                if (def.bone == HumanBodyBones.Hips)
                    HipsRigidbody = rb;

                // Ignore collision with KCC character capsule
                if (kccCollider != null)
                    Physics.IgnoreCollision(col, kccCollider, true);

                // Attach character joint to nearest parent bone with rigidbody
                if (def.bone != HumanBodyBones.Hips)
                {
                    var parentRb = FindParentRigidbody(bone.parent);
                    if (parentRb != null)
                    {
                        var joint = bone.gameObject.AddComponent<CharacterJoint>();
                        joint.connectedBody = parentRb;
                        joint.axis = Vector3.up;
                        joint.swingAxis = Vector3.forward;
                        joint.lowTwistLimit = new SoftJointLimit { limit = -def.twist };
                        joint.highTwistLimit = new SoftJointLimit { limit = def.twist };
                        joint.swing1Limit = new SoftJointLimit { limit = def.swing };
                        joint.swing2Limit = new SoftJointLimit { limit = def.swing * 0.65f };
                        joint.enableProjection = true;
                        joint.enablePreprocessing = false;
                    }
                }
            }

            // Ignore mutual collisions between ragdoll parts of the same body
            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++)
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);

            // Index all skeleton transforms under hips for smooth zero-pop get-up blending
            var boneList = new List<Transform>();
            if (hipsTransform != null)
            {
                boneList.Add(hipsTransform);
                hipsBoneIndex = 0;
                foreach (Transform t in hipsTransform.GetComponentsInChildren<Transform>(true))
                {
                    if (t != hipsTransform)
                        boneList.Add(t);
                }
            }
            allBones = boneList.ToArray();
            snapshotPositions = new Vector3[allBones.Length];
            snapshotRotations = new Quaternion[allBones.Length];
        }

        private Rigidbody FindParentRigidbody(Transform current)
        {
            while (current != null)
            {
                if (boneToRb.TryGetValue(current, out var rb)) return rb;
                current = current.parent;
            }
            return null;
        }

        private static int DominantAxis(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x > a.y && a.x > a.z ? 0 : (a.y > a.z ? 1 : 2);
        }

        /// <summary>
        /// Activa la simulación física completa del ragdoll impulsando el cuerpo.
        /// </summary>
        public void GoRagdoll(Vector3 impulse, float minDuration = 1.5f)
        {
            if (IsRagdolled) return;
            IsRagdolled = true;
            IsRecovering = false;
            RagdollStartTime = Time.time;
            MinRagdollDuration = minDuration;

            if (rigBuilder != null) rigBuilder.enabled = false;
            if (player != null) player.CancelCombatState();

            Vector3 inheritedVelocity = kcc != null ? kcc.RealVelocity : Vector3.zero;

            if (kcc != null)
            {
                kcc.enabled = false;
            }

            if (animator != null) animator.enabled = false;

            foreach (var col in colliders) if (col != null) col.enabled = true;
            foreach (var rb in rigidbodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.linearVelocity = inheritedVelocity;
            }

            DetachSkeleton();

            // Apply punchy impulse to the torso and hips
            if (HipsRigidbody != null)
            {
                HipsRigidbody.AddForce(impulse, ForceMode.VelocityChange);
                HipsRigidbody.angularVelocity = UnityEngine.Random.insideUnitSphere * 12f;
            }

            var chestBone = animator != null ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            if (chestBone != null && boneToRb.TryGetValue(chestBone, out var chestRb))
            {
                chestRb.AddForce(impulse * 0.85f, ForceMode.VelocityChange);
            }
        }

        /// <summary>
        /// Recupera al astronauta colocándolo sobre el suelo y restaurando el Animator y KCC mediante
        /// una transición progresiva de rigidez muscular en todos los huesos para evitar cualquier chasquido.
        /// </summary>
        public void StandUp()
        {
            if (!IsRagdolled) return;

            ReattachSkeleton();
            Vector3 hipsWorldPos = hipsTransform != null ? hipsTransform.position : transform.position;
            Quaternion hipsWorldRot = hipsTransform != null ? hipsTransform.rotation : transform.rotation;

            // Congela y captura instantáneamente la pose física de todos los huesos
            if (allBones != null)
            {
                for (int i = 0; i < allBones.Length; i++)
                {
                    Transform b = allBones[i];
                    if (b != null)
                    {
                        snapshotPositions[i] = b.localPosition;
                        snapshotRotations[i] = b.localRotation;
                    }
                }
            }

            foreach (var rb in rigidbodies)
            {
                if (rb == null) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            foreach (var col in colliders) if (col != null) col.enabled = false;

            // Busca el punto de suelo justo debajo de las caderas
            Vector3 groundPoint = GroundBelow(hipsWorldPos);
            LastStandPosition = groundPoint + Vector3.up * 0.05f;

            // Orientación horizontal biomecánica basada en el torso/cabeza
            Transform spine = animator != null ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            Vector3 spineDir = (spine != null && hipsTransform != null) ? (spine.position - hipsWorldPos) : (hipsTransform != null ? hipsTransform.up : transform.forward);
            Vector3 headDir = Vector3.ProjectOnPlane(spineDir, Vector3.up);

            Vector3 facing = transform.forward;
            if (headDir.sqrMagnitude > 0.04f && hipsTransform != null)
            {
                bool onBack = Vector3.Dot(hipsTransform.forward, Vector3.up) > 0.1f;
                facing = (onBack ? -headDir : headDir).normalized;
            }
            else if (facing.sqrMagnitude < 0.04f)
            {
                facing = Vector3.forward;
            }

            transform.position = LastStandPosition;
            if (kcc != null)
            {
                kcc.enabled = true;
                kcc.SetPosition(LastStandPosition);
                kcc.SetLookRotation(Quaternion.LookRotation(facing).eulerAngles);
            }

            // Recalcula el snapshot local de las caderas respecto a su nuevo padre para eliminar cualquier salto visual
            if (hipsBoneIndex >= 0 && hipsTransform != null && hipsTransform.parent != null)
            {
                snapshotPositions[hipsBoneIndex] = hipsTransform.parent.InverseTransformPoint(hipsWorldPos);
                snapshotRotations[hipsBoneIndex] = Quaternion.Inverse(hipsTransform.parent.rotation) * hipsWorldRot;
            }

            if (cameraPivot != null)
            {
                Vector3 cameraPivotWorldPos = cameraPivot.position;
                snapshotCameraPivotLocalPos = cameraPivot.parent != null
                    ? cameraPivot.parent.InverseTransformPoint(cameraPivotWorldPos)
                    : cameraPivot.localPosition;
            }

            IsRagdolled = false;
            IsRecovering = true;
            recoveryStartTime = Time.time;

            if (rigBuilder != null) rigBuilder.enabled = false;
            if (player != null) player.CancelCombatState();

            if (animator != null)
            {
                animator.enabled = true;
                animator.Play("Locomotion", 0, 0f);
                animator.Update(0f);
            }
        }

        private void LateUpdate()
        {
            if (IsRagdolled)
            {
                if (hipsTransform != null && cameraPivot != null)
                {
                    Vector3 targetPivot = hipsTransform.position + Vector3.up * 0.75f;
                    cameraPivot.position = Vector3.Lerp(cameraPivot.position, targetPivot, Time.deltaTime * 14f);
                }
                return;
            }

            if (IsRecovering)
            {
                float elapsed = Time.time - recoveryStartTime;
                float t = Mathf.Clamp01(elapsed / RecoveryDuration);
                // Transición s-curve de rigidez muscular progresiva
                float blend = Mathf.SmoothStep(0f, 1f, t);

                if (allBones != null)
                {
                    for (int i = 0; i < allBones.Length; i++)
                    {
                        Transform b = allBones[i];
                        if (b == null) continue;

                        Vector3 animLocalPos = b.localPosition;
                        Quaternion animLocalRot = b.localRotation;

                        b.localPosition = Vector3.Lerp(snapshotPositions[i], animLocalPos, blend);
                        b.localRotation = Quaternion.Slerp(snapshotRotations[i], animLocalRot, blend);
                    }
                }

                if (cameraPivot != null)
                {
                    cameraPivot.localPosition = Vector3.Lerp(snapshotCameraPivotLocalPos, cameraPivotLocalRest, blend);
                }

                if (t >= 1f)
                {
                    IsRecovering = false;
                    if (rigBuilder != null)
                    {
                        rigBuilder.enabled = true;
                    }
                    if (cameraPivot != null)
                    {
                        cameraPivot.localPosition = cameraPivotLocalRest;
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (IsRagdolled)
            {
                try { StandUp(); }
                catch (Exception) { if (hipsDetached && hipsTransform != null) Destroy(hipsTransform.gameObject); hipsDetached = false; }
            }
            IsRecovering = false;
        }

        private void OnDestroy()
        {
            // Si el jugador se destruye con el esqueleto suelto, no dejarlo tirado en la escena.
            if (hipsDetached && hipsTransform != null) Destroy(hipsTransform.gameObject);
            hipsDetached = false;
            if (IsRagdolled) IsRagdolled = false;
            IsRecovering = false;
        }
    }
}
