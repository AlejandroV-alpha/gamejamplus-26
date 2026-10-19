using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using State = GusanoExcavadorFusion.State;

/// <summary>
/// Efectos del gusano, solo visuales y en cada cliente: nubes de tierra al salir y enterrarse,
/// polvo mientras repta, agujeros y surcos que quedan en el suelo, y colisiones en el cuerpo
/// para que las balas lo salpiquen.
/// </summary>
public sealed class GusanoExcavadorFX : MonoBehaviour
{
    [SerializeField] Material dustMaterial;
    [SerializeField] Material holeMaterial;
    [SerializeField] Color dirtColor = new Color(.55f, .25f, .12f, .8f);
    [SerializeField, Min(.5f)] float holeSize = 7f;
    [SerializeField, Min(.2f)] float furrowSize = 2.6f;
    [SerializeField, Min(.2f)] float furrowSpacing = 1.6f;
    [SerializeField, Min(1f)] float marksLifetime = 35f;
    [SerializeField, Min(.2f)] float hitRadius = 1.3f;

    const int MaxMarks = 80;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    GusanoExcavadorFusion worm;
    ParticleSystem dirt;
    Transform head;
    readonly List<SphereCollider> hitBoxes = new List<SphereCollider>();
    readonly Queue<(Transform t, Renderer r, float born, float size)> marks = new Queue<(Transform, Renderer, float, float)>();
    MaterialPropertyBlock block;
    State last = (State)(-1);
    Vector3 lastFurrow;

    void Awake()
    {
        worm = GetComponent<GusanoExcavadorFusion>();
        block = new MaterialPropertyBlock();
        dirt = MakeDirtSystem();
        BuildHitBoxes();
    }

    void LateUpdate()
    {
        if (worm == null) return;
        State state = worm.CurrentState;
        bool visible = state != State.Hidden;
        // During a grab every hitbox remains shootable, without physically pushing the victim.
        foreach (var box in hitBoxes)
        {
            box.isTrigger = state == State.Hold;
            box.enabled = visible;
        }

        if (state != last)
        {
            OnStateChanged(state);
            last = state;
        }

        // Mientras repta levanta polvo y va abriendo un surco tras de si.
        if (state == State.Crawl || state == State.Attack)
        {
            Vector3 front = Ground(head != null ? head.position : transform.position);
            if (Random.value < .5f) Emit(front, 2, 1.2f, 2.5f);
            if ((front - lastFurrow).sqrMagnitude > furrowSpacing * furrowSpacing)
            {
                Mark(front, furrowSize * Random.Range(.8f, 1.15f), transform.forward, .55f);
                lastFurrow = front;
            }
        }
        FadeMarks();
    }

    void OnStateChanged(State state)
    {
        Vector3 ground = Ground(transform.position);
        switch (state)
        {
            case State.Emerge:
            case State.EmergeJump:
                // Explota la tierra y queda el agujero de salida.
                Emit(ground, 70, 3f, 9f);
                Mark(ground, holeSize, transform.forward, 1f);
                lastFurrow = ground;
                break;
            case State.Burrow:
                Emit(ground, 50, 2f, 6f);
                Mark(ground, holeSize * .85f, transform.forward, 1f);
                break;
        }
    }

    // ---------- Tierra ----------

    ParticleSystem MakeDirtSystem()
    {
        var go = new GameObject("Gusano_Tierra");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;
        main.startSpeed = 0f;
        main.gravityModifier = .6f;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 2.2f));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f), new GradientAlphaKey(.6f, .5f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 1.5f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dustMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    void Emit(Vector3 at, int count, float sizeMax, float speed)
    {
        if (dustMaterial == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector2 c = Random.insideUnitCircle;
            Vector3 dir = new Vector3(c.x, Random.Range(.4f, 1.2f), c.y);
            var p = new ParticleSystem.EmitParams
            {
                position = at + new Vector3(c.x, .2f, c.y) * 1.5f,
                velocity = dir * Random.Range(.3f, 1f) * speed,
                startSize = Random.Range(sizeMax * .4f, sizeMax),
                startLifetime = Random.Range(1.2f, 2.6f),
                startColor = Color.Lerp(dirtColor, dirtColor * .7f, Random.value),
                rotation = Random.Range(0f, 360f),
            };
            dirt.Emit(p, 1);
        }
    }

    // ---------- Agujeros y surcos ----------

    void Mark(Vector3 at, float size, Vector3 forward, float darkness)
    {
        if (holeMaterial == null) return;
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Gusano_Marca";
        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = holeMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
        Vector3 normal = GroundNormal(at);
        Vector3 f = Vector3.ProjectOnPlane(forward, normal).normalized;
        if (f.sqrMagnitude < .01f) f = Vector3.forward;
        quad.transform.SetPositionAndRotation(at + normal * .03f,
            Quaternion.LookRotation(-normal, f) * Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f)));
        quad.transform.localScale = Vector3.one * size;
        SetAlpha(r, darkness);
        marks.Enqueue((quad.transform, r, Time.time, darkness));
        while (marks.Count > MaxMarks) Destroy(marks.Dequeue().t.gameObject);
    }

    void FadeMarks()
    {
        int count = marks.Count;
        for (int i = 0; i < count; i++)
        {
            var m = marks.Dequeue();
            if (m.t == null) continue;
            float age = Time.time - m.born;
            if (age > marksLifetime) { Destroy(m.t.gameObject); continue; }
            float fade = 1f - Mathf.Clamp01((age - marksLifetime * .7f) / (marksLifetime * .3f));
            SetAlpha(m.r, m.size * fade);
            marks.Enqueue(m);
        }
    }

    void SetAlpha(Renderer r, float a)
    {
        Color c = holeMaterial.HasProperty(BaseColor) ? holeMaterial.GetColor(BaseColor) : Color.white;
        c.a *= a;
        r.GetPropertyBlock(block);
        block.SetColor(BaseColor, c);
        r.SetPropertyBlock(block);
    }

    // ---------- Colisiones para las balas ----------

    void BuildHitBoxes()
    {
        Transform[] bones = GetComponentsInChildren<Transform>(true)
            .Where(t => t.GetComponent<Renderer>() == null && t.childCount > 0 && t != transform).ToArray();
        // Un hueso de cada tres basta para cubrir el cuerpo sin cientos de colisiones.
        for (int i = 0; i < bones.Length; i += 3)
        {
            var box = bones[i].gameObject.AddComponent<SphereCollider>();
            box.radius = hitRadius / Mathf.Max(.01f, bones[i].lossyScale.x);
            box.isTrigger = false;
            hitBoxes.Add(box);
        }
        head = bones.LastOrDefault(b => b.name.ToLower().Contains("cabeza") || b.name.ToLower().Contains("head"))
            ?? (bones.Length > 0 ? bones[bones.Length - 1] : null);
    }

    static Vector3 Ground(Vector3 point)
    {
        var terrain = Terrain.activeTerrains.FirstOrDefault(t => point.x >= t.transform.position.x && point.z >= t.transform.position.z
            && point.x <= t.transform.position.x + t.terrainData.size.x && point.z <= t.transform.position.z + t.terrainData.size.z);
        if (terrain != null) point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        return point;
    }

    static Vector3 GroundNormal(Vector3 point)
    {
        var terrain = Terrain.activeTerrains.FirstOrDefault();
        if (terrain == null) return Vector3.up;
        Vector3 local = point - terrain.transform.position;
        return terrain.terrainData.GetInterpolatedNormal(local.x / terrain.terrainData.size.x, local.z / terrain.terrainData.size.z);
    }

    void OnDestroy()
    {
        foreach (var m in marks) if (m.t != null) Destroy(m.t.gameObject);
    }
}
