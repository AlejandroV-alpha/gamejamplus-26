using System.Collections.Generic;
using GameJam.Combat;
using UnityEngine;

namespace GameJam.Grinder
{
    /// <summary>
    /// Cuerpo del monstruo muerto como ragdoll (presentacion local en cada cliente).
    /// En el instante de la muerte se clona el modelo del enemigo con su pose actual, se oculta el
    /// original (asi no se ve hundirse ni desvanecerse) y se le ponen cuerpos rigidos y articulaciones
    /// a los huesos principales para que caiga y se bambolee al arrastrarlo.
    /// </summary>
    public sealed class CorpseRagdoll : MonoBehaviour
    {
        private static readonly List<CorpseRagdoll> unclaimed = new List<CorpseRagdoll>();
        private const int MaxBodies = 18;

        private readonly List<Rigidbody> bodies = new List<Rigidbody>();
        private Rigidbody core;                  // el cuerpo mas central (pelvis/tronco)
        private Rigidbody anchor;
        private ConfigurableJoint grip;
        private float bornAt;
        public bool Claimed { get; private set; }
        public float Size { get; private set; } = 1f;

        /// <summary>Centro actual del cuerpo (se mueve con la fisica).</summary>
        public Vector3 Center => core != null ? core.worldCenterOfMass : transform.position;

        // ------------------------------------------------------------------ creacion

        public static CorpseRagdoll Create(EnemyCombatUI enemy)
        {
            if (enemy == null) return null;
            var animator = enemy.GetComponentInChildren<Animator>();
            Transform source = animator != null ? animator.transform : enemy.transform;

            // Se clona inactivo para que ningun script del enemigo llegue a ejecutarse en la copia.
            var holder = new GameObject("CorpseHolder");
            holder.SetActive(false);
            var clone = Instantiate(source.gameObject, source.position, source.rotation, holder.transform);
            clone.transform.localScale = source.lossyScale;
            StripToVisuals(clone);
            clone.transform.SetParent(null, true);
            Destroy(holder);
            clone.name = "Corpse_" + enemy.name;

            var corpse = clone.AddComponent<CorpseRagdoll>();
            corpse.bornAt = Time.time;
            clone.SetActive(true);
            corpse.BuildRagdoll();
            enemy.HideVisuals();
            unclaimed.Add(corpse);
            return corpse;
        }

        /// <summary>El cuerpo libre mas cercano a un punto (lo reclaman los restos de red).</summary>
        public static CorpseRagdoll Claim(Vector3 near, float maxDistance)
        {
            CorpseRagdoll best = null; float bestD = maxDistance * maxDistance;
            foreach (var c in unclaimed)
            {
                if (c == null) continue;
                float d = (c.transform.position - near).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best != null) { unclaimed.Remove(best); best.Claimed = true; }
            return best;
        }

        private static void StripToVisuals(GameObject root)
        {
            // Varias pasadas: algunos componentes dependen de otros (RequireComponent).
            for (int pass = 0; pass < 4; pass++)
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is MeshFilter || c is SkinnedMeshRenderer || c is MeshRenderer) continue;
                    DestroyImmediate(c);
                }
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        }

        // ------------------------------------------------------------------ ragdoll

        private void BuildRagdoll()
        {
            var bones = new HashSet<Transform>();
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var b in smr.bones) if (b != null) bones.Add(b);

            var bounds = VisualBounds();
            Size = Mathf.Max(.5f, Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z));

            if (bones.Count == 0)
            {
                // Sin esqueleto: un solo cuerpo rigido con caja.
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = transform.InverseTransformPoint(bounds.center);
                box.size = Vector3.Scale(bounds.size, new Vector3(1 / Mathf.Max(.001f, transform.lossyScale.x), 1 / Mathf.Max(.001f, transform.lossyScale.y), 1 / Mathf.Max(.001f, transform.lossyScale.z)));
                core = AddBody(gameObject, 40f);
                return;
            }

            // Huesos "largos": los que tienen un hijo a una distancia apreciable. Se eligen los mas largos.
            float minLength = Size * .07f;
            var candidates = new List<(Transform bone, Transform child, float len)>();
            foreach (var b in bones)
            {
                Transform child = null; float len = 0;
                foreach (Transform c in b)
                {
                    if (!bones.Contains(c)) continue;
                    float l = Vector3.Distance(b.position, c.position);
                    if (l > len) { len = l; child = c; }
                }
                if (child != null && len >= minLength) candidates.Add((b, child, len));
            }
            candidates.Sort((x, y) => y.len.CompareTo(x.len));
            if (candidates.Count > MaxBodies) candidates.RemoveRange(MaxBodies, candidates.Count - MaxBodies);

            float totalMass = Mathf.Clamp(Size * 35f, 25f, 160f);
            var made = new Dictionary<Transform, Rigidbody>();
            // Primero los mas cercanos a la raiz, para que cada articulacion tenga ya a su padre.
            candidates.Sort((x, y) => Depth(x.bone).CompareTo(Depth(y.bone)));
            foreach (var (bone, child, len) in candidates)
            {
                var col = bone.gameObject.AddComponent<CapsuleCollider>();
                Vector3 local = bone.InverseTransformPoint(child.position);
                int axis = Dominant(local);
                float scale = Mathf.Max(.0001f, bone.lossyScale[axis]);
                col.direction = axis;
                col.center = local * .5f;
                col.height = Mathf.Abs(local[axis]) * 1.05f;
                col.radius = Mathf.Clamp(len * .28f, Size * .035f, Size * .18f) / scale;
                col.height = Mathf.Max(col.height, col.radius * 2f);
                var rb = AddBody(bone.gameObject, totalMass / candidates.Count);
                made[bone] = rb;

                var parent = ParentBody(bone.parent, made);
                if (parent != null)
                {
                    var j = bone.gameObject.AddComponent<CharacterJoint>();
                    j.connectedBody = parent;
                    j.axis = Vector3.right; j.swingAxis = Vector3.up;
                    j.lowTwistLimit = new SoftJointLimit { limit = -35 };
                    j.highTwistLimit = new SoftJointLimit { limit = 35 };
                    j.swing1Limit = new SoftJointLimit { limit = 45 };
                    j.swing2Limit = new SoftJointLimit { limit = 45 };
                    j.enableProjection = true;
                }
                else if (core == null) core = rb;
            }
            if (core == null && bodies.Count > 0) core = bodies[0];

            // Las piezas de un mismo cuerpo no chocan entre si.
            var cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
                for (int k = i + 1; k < cols.Length; k++) Physics.IgnoreCollision(cols[i], cols[k], true);
        }

        private Rigidbody AddBody(GameObject go, float mass)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(1f, mass);
            rb.linearDamping = .4f;
            rb.angularDamping = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            bodies.Add(rb);
            return rb;
        }

        private static Rigidbody ParentBody(Transform t, Dictionary<Transform, Rigidbody> made)
        {
            for (; t != null; t = t.parent) if (made.TryGetValue(t, out var rb)) return rb;
            return null;
        }

        private static int Depth(Transform t) { int d = 0; for (; t != null; t = t.parent) d++; return d; }
        private static int Dominant(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x > a.y && a.x > a.z ? 0 : (a.y > a.z ? 1 : 2);
        }

        public Bounds VisualBounds()
        {
            var b = new Bounds(transform.position, Vector3.zero); bool first = true;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ------------------------------------------------------------------ arrastre

        /// <summary>Engancha el cuerpo a un punto que se mueve (la mano del jugador).</summary>
        public void Grab(Vector3 handPoint)
        {
            if (bodies.Count == 0 || grip != null) return;
            // Se agarra de la pieza mas cercana a la mano: si coges un brazo, el resto cuelga.
            Rigidbody best = core; float bestD = float.MaxValue;
            foreach (var rb in bodies)
            {
                float d = (rb.worldCenterOfMass - handPoint).sqrMagnitude;
                if (d < bestD) { bestD = d; best = rb; }
            }
            if (anchor == null)
            {
                var a = new GameObject("CorpseGrip");
                anchor = a.AddComponent<Rigidbody>();
                anchor.isKinematic = true;
                anchor.interpolation = RigidbodyInterpolation.Interpolate;
            }
            anchor.transform.position = best.worldCenterOfMass;
            anchor.position = best.worldCenterOfMass;
            grip = best.gameObject.AddComponent<ConfigurableJoint>();
            grip.connectedBody = anchor;
            grip.autoConfigureConnectedAnchor = false;
            grip.anchor = best.transform.InverseTransformPoint(best.worldCenterOfMass);
            grip.connectedAnchor = Vector3.zero;
            // Cuerda corta y elastica: tira del cuerpo sin teletransportarlo.
            grip.xMotion = grip.yMotion = grip.zMotion = ConfigurableJointMotion.Limited;
            grip.linearLimit = new SoftJointLimit { limit = .15f };
            grip.linearLimitSpring = new SoftJointLimitSpring { spring = 9000f * Mathf.Max(1f, best.mass / 4f), damper = 400f };
            foreach (var rb in bodies) rb.WakeUp();
        }

        /// <summary>Mueve el punto de agarre (suavemente; la fisica hace el resto).</summary>
        public void MoveGrip(Vector3 handPoint)
        {
            if (anchor == null || grip == null) return;
            anchor.MovePosition(Vector3.MoveTowards(anchor.position, handPoint, 12f * Time.fixedDeltaTime * 4f));
        }

        public void Release()
        {
            if (grip != null) Destroy(grip);
            grip = null;
        }

        public bool IsGripped => grip != null;

        /// <summary>Al entrar a la trituradora: se congela la fisica y se entrega para la animacion.</summary>
        public Transform Freeze(Transform parent)
        {
            Release();
            foreach (var rb in bodies) if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }
            var b = VisualBounds();
            var holder = new GameObject("CorpseFeed").transform;
            holder.position = new Vector3(b.center.x, b.min.y, b.center.z);
            transform.SetParent(holder, true);
            holder.SetParent(parent, true);
            return holder;
        }

        private void Update()
        {
            // Cuerpos sin dueño en red (por ejemplo, enemigos sin red) desaparecen tras un rato.
            if (!Claimed && Time.time - bornAt > 90f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            unclaimed.Remove(this);
            if (anchor != null) Destroy(anchor.gameObject);
        }
    }
}
