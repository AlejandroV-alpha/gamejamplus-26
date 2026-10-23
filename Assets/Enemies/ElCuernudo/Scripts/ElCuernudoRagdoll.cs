using System.Collections.Generic;
using UnityEngine;

// Ragdoll de El Cuernudo, armado en tiempo de ejecución a partir de los huesos del modelo.
// Mientras vive no existe (cero costo de física). Al morir: apaga el Animator, crea cuerpos rígidos,
// colisionadores y articulaciones con límites, y lo empuja en la dirección del golpe final.
// Cuando ya quedó quieto en el suelo, se congela para poder hundirse en la arena.
public sealed class ElCuernudoRagdoll : MonoBehaviour
{
    // nombre del hueso, hueso hacia donde apunta (para el largo), radio y masa (en unidades del modelo de 2 m)
    struct Part
    {
        public string bone, toward;
        public float radius, mass, swing, twist;
        public bool sphere;
        public Part(string bone, string toward, float radius, float mass, float swing, float twist, bool sphere = false)
        { this.bone = bone; this.toward = toward; this.radius = radius; this.mass = mass; this.swing = swing; this.twist = twist; this.sphere = sphere; }
    }

    static readonly Part[] Parts =
    {
        new Part("Hips", "Spine", .22f, 30f, 0f, 0f),
        new Part("Spine", "Chest", .21f, 20f, 25f, 15f),
        new Part("Chest", "Neck", .26f, 25f, 25f, 15f),
        new Part("Head", null, .34f, 18f, 35f, 25f, true),
        new Part("LeftUpperArm", "LeftLowerArm", .075f, 6f, 70f, 30f),
        new Part("LeftLowerArm", "LeftHand", .065f, 4f, 80f, 10f),
        new Part("RightUpperArm", "RightLowerArm", .075f, 6f, 70f, 30f),
        new Part("RightLowerArm", "RightHand", .065f, 4f, 80f, 10f),
        new Part("LeftUpperLeg", "LeftLowerLeg", .09f, 10f, 50f, 15f),
        new Part("LeftLowerLeg", "LeftFoot", .075f, 6f, 75f, 5f),
        new Part("RightUpperLeg", "RightLowerLeg", .09f, 10f, 50f, 15f),
        new Part("RightLowerLeg", "RightFoot", .075f, 6f, 75f, 5f),
    };

    public bool IsActive { get; private set; }
    public bool IsFrozen { get; private set; }

    readonly List<Rigidbody> bodies = new List<Rigidbody>();
    readonly List<Collider> colliders = new List<Collider>();
    Animator animator;
    float activatedAt;

    // size = escala del enemigo (localScale.x del root); push = dirección y fuerza del golpe final.
    public void Activate(float size, Vector3 push)
    {
        if (IsActive) return;
        IsActive = true;
        activatedAt = Time.time;

        animator = GetComponentInChildren<Animator>();
        var bones = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (!bones.ContainsKey(t.name)) bones[t.name] = t;

        // Que la malla no desaparezca al alejarse los huesos de sus límites originales.
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;

        if (animator != null) animator.enabled = false;

        var byBone = new Dictionary<Transform, Rigidbody>();
        foreach (var p in Parts)
        {
            if (!bones.TryGetValue(p.bone, out var bone)) continue;
            float toLocal = size / Mathf.Max(.0001f, bone.lossyScale.x); // unidades del modelo -> locales del hueso

            var rb = bone.gameObject.AddComponent<Rigidbody>();
            rb.mass = p.mass;
            rb.linearDamping = .05f;
            rb.angularDamping = 1.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.maxDepenetrationVelocity = 3f * size;

            Vector3 axisDir = Vector3.up;
            Collider col;
            if (p.sphere)
            {
                // La cabeza es enorme: esfera adelantada sobre el hueso.
                var sc = bone.gameObject.AddComponent<SphereCollider>();
                sc.radius = p.radius * toLocal;
                sc.center = Vector3.up * .3f * toLocal;
                col = sc;
            }
            else
            {
                Vector3 local = Vector3.up * .25f * toLocal;
                if (p.toward != null && bones.TryGetValue(p.toward, out var child))
                    local = bone.InverseTransformPoint(child.position);
                int axis = DominantAxis(local);
                axisDir = Vector3.zero;
                axisDir[axis] = Mathf.Sign(local[axis]);
                var cc = bone.gameObject.AddComponent<CapsuleCollider>();
                cc.direction = axis;
                cc.radius = p.radius * toLocal;
                cc.height = Mathf.Max(cc.radius * 2f, Mathf.Abs(local[axis]) + cc.radius);
                cc.center = local * .5f;
                col = cc;
            }
            colliders.Add(col);
            bodies.Add(rb);
            byBone[bone] = rb;

            // Articulación con el ancestro más cercano que tenga cuerpo.
            var parentBody = FindParentBody(bone, byBone);
            if (parentBody != null)
            {
                var j = bone.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = parentBody;
                j.axis = axisDir;
                j.swingAxis = Perpendicular(axisDir);
                j.lowTwistLimit = new SoftJointLimit { limit = -p.twist };
                j.highTwistLimit = new SoftJointLimit { limit = p.twist };
                j.swing1Limit = new SoftJointLimit { limit = p.swing };
                j.swing2Limit = new SoftJointLimit { limit = p.swing * .6f };
                j.enablePreprocessing = false;
                j.enableProjection = true;
            }
        }

        // Las partes del mismo cuerpo no chocan entre sí.
        for (int a = 0; a < colliders.Count; a++)
            for (int b = a + 1; b < colliders.Count; b++)
                Physics.IgnoreCollision(colliders[a], colliders[b], true);

        // Empujón del golpe final: más fuerte en el torso, un poco hacia arriba.
        foreach (var rb in bodies)
        {
            bool torso = rb.name == "Chest" || rb.name == "Head" || rb.name == "Spine";
            rb.AddForce(push * (torso ? 1f : .5f), ForceMode.VelocityChange);
        }
    }

    // Congela el ragdoll (para hundirlo moviendo el visual sin que la física lo pelee).
    public void Freeze()
    {
        if (!IsActive || IsFrozen) return;
        IsFrozen = true;
        foreach (var rb in bodies)
        {
            if (rb == null) continue;
            rb.isKinematic = true;
        }
        foreach (var c in colliders) if (c != null) c.enabled = false;
    }

    // Quieto hace un rato (o pasó demasiado tiempo): listo para congelar.
    public bool HasSettled(float minTime, float maxTime)
    {
        float t = Time.time - activatedAt;
        if (t >= maxTime) return true;
        if (t < minTime) return false;
        foreach (var rb in bodies)
            if (rb != null && rb.linearVelocity.sqrMagnitude > .25f) return false;
        return true;
    }

    static Rigidbody FindParentBody(Transform bone, Dictionary<Transform, Rigidbody> byBone)
    {
        for (var t = bone.parent; t != null; t = t.parent)
            if (byBone.TryGetValue(t, out var rb)) return rb;
        return null;
    }

    static int DominantAxis(Vector3 v)
    {
        var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        return a.x > a.y && a.x > a.z ? 0 : (a.y > a.z ? 1 : 2);
    }

    static Vector3 Perpendicular(Vector3 axis)
    {
        var p = Vector3.Cross(axis, Vector3.forward);
        if (p.sqrMagnitude < .01f) p = Vector3.Cross(axis, Vector3.right);
        return p.normalized;
    }
}
