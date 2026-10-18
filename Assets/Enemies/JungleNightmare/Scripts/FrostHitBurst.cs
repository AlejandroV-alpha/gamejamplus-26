using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Explosion de frio sobre el jugador golpeado: estallido de vaho helado, cristales,
/// anillo de escarcha y una neblina que lo envuelve un momento, como si se congelara.
/// Se crea por codigo con los materiales de hielo del propio Jungle Nightmare.
/// </summary>
public sealed class FrostHitBurst : MonoBehaviour
{
    const float Lifetime = 3f;

    public static void Spawn(Vector3 position, Transform follow, Material smoke, Material crystal, float strength)
    {
        var go = new GameObject("FrostHitBurst");
        go.transform.position = position;
        var burst = go.AddComponent<FrostHitBurst>();
        burst.follow = follow;
        burst.offset = follow != null ? position - follow.position : Vector3.zero;

        // 1. Estallido de vaho: bocanadas grandes que salen disparadas y se expanden.
        Emit(Make(go, "Vaho", smoke, false, true), 26, strength, (ref ParticleSystem.EmitParams p) =>
        {
            Vector3 dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * .6f;
            p.velocity = dir * Random.Range(1.5f, 4f) * strength;
            p.startSize = Random.Range(.35f, .7f) * strength;
            p.startLifetime = Random.Range(.8f, 1.4f);
            p.startColor = new Color(.75f, .92f, 1f, .7f);
        });

        // 2. Cristales de hielo que saltan y caen.
        Emit(Make(go, "Cristales", crystal, true, true), 30, strength, (ref ParticleSystem.EmitParams p) =>
        {
            p.velocity = (Random.onUnitSphere + Vector3.up * .8f) * Random.Range(2f, 5f) * strength;
            p.startSize = Random.Range(.04f, .1f) * strength;
            p.startLifetime = Random.Range(.6f, 1.1f);
            p.startColor = new Color(.7f, .95f, 1f, 1f);
            p.rotation3D = Random.insideUnitSphere * 180f;
            p.angularVelocity3D = Random.insideUnitSphere * 360f;
        });

        // 3. Anillo de escarcha que se abre a la altura del pecho.
        Emit(Make(go, "Anillo", smoke, false, false), 20, strength, (ref ParticleSystem.EmitParams p) =>
        {
            float a = Random.value * Mathf.PI * 2f;
            p.velocity = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 5f * strength;
            p.startSize = .3f * strength;
            p.startLifetime = .45f;
            p.startColor = new Color(.85f, .97f, 1f, .8f);
        });

        // 4. Neblina helada que envuelve al jugador mientras "se congela".
        burst.mist = Make(go, "Neblina", smoke, false, false);
        var mistMain = burst.mist.main;
        mistMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = burst.mist.emission;
        emission.enabled = true;
        emission.rateOverTime = 22f * strength;
        var shape = burst.mist.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .45f * strength;
        mistMain.startSize = new ParticleSystem.MinMaxCurve(.25f * strength, .5f * strength);
        mistMain.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1f);
        mistMain.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .4f);
        mistMain.startColor = new Color(.7f, .9f, 1f, .45f);
        mistMain.gravityModifier = -.05f;
        burst.mist.Play();

        // Destello frio breve.
        var light = new GameObject("Destello").AddComponent<Light>();
        light.transform.SetParent(go.transform, false);
        light.type = LightType.Point;
        light.color = new Color(.5f, .85f, 1f);
        light.range = 5f * strength;
        light.intensity = 6f;
        light.shadows = LightShadows.None;
        burst.flash = light;

        Destroy(go, Lifetime);
    }

    Transform follow;
    Vector3 offset;
    ParticleSystem mist;
    Light flash;
    float age;

    void LateUpdate()
    {
        age += Time.deltaTime;
        if (follow != null) transform.position = follow.position + offset;
        if (flash != null) flash.intensity = Mathf.Lerp(6f, 0f, age / .35f);
        if (mist != null && age > 1.4f && mist.isEmitting) mist.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    static ParticleSystem Make(GameObject parent, string name, Material material, bool crystal, bool gravity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.startSpeed = 0f;
        main.gravityModifier = gravity ? (crystal ? 1.2f : -.08f) : 0f;
        main.startRotation3D = crystal;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.7f, .9f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .08f), new GradientAlphaKey(.6f, .5f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, crystal ? 1f : .5f, 1f, crystal ? .3f : 2.6f));

        if (!crystal)
        {
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (crystal)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }
        ps.Play();
        return ps;
    }

    static void Emit(ParticleSystem ps, int count, float strength, EmitSetup setup)
    {
        Vector3 origin = ps.transform.position;
        for (int i = 0; i < count; i++)
        {
            var p = new ParticleSystem.EmitParams { position = origin };
            setup(ref p);
            ps.Emit(p, 1);
        }
    }

    delegate void EmitSetup(ref ParticleSystem.EmitParams p);
}
