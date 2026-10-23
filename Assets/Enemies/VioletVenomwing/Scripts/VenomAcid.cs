using UnityEngine;
using UnityEngine.Rendering;

// Ácido del Venomwing (solo visual en cada cliente): bola verde en arco con estela y gotas,
// salpicadura al impactar y charco burbujeante. El daño lo aplica la autoridad con onImpact.
public class VenomAcid : MonoBehaviour
{
    public static readonly Color Acid = new Color(.45f, 1f, .15f);
    public static readonly Color AcidDark = new Color(.1f, .45f, .05f);

    // Materiales (los asigna el setup de la escena).
    public struct Mats { public Material glow, puddle, blob; }

    Mats mats;
    Vector3 velocity;
    float gravity, life;
    float puddleRadius, puddleTime;
    System.Action<Vector3> onImpact;
    Transform owner;
    ParticleSystem drips;

    public static VenomAcid Launch(Vector3 from, Vector3 to, float flightTime, Mats mats, Transform owner,
        float puddleRadius, float puddleTime, System.Action<Vector3> onImpact)
    {
        var go = new GameObject("AcidoEscupido");
        go.transform.position = from;
        var s = go.AddComponent<VenomAcid>();
        s.mats = mats;
        s.owner = owner;
        s.onImpact = onImpact;
        s.puddleRadius = puddleRadius;
        s.puddleTime = puddleTime;
        s.gravity = 14f;
        float t = Mathf.Max(.2f, flightTime);
        // Tiro balístico que llega a "to" en el tiempo pedido.
        s.velocity = (to - from) / t + Vector3.up * (.5f * s.gravity * t);
        s.Build();
        return s;
    }

    void Build()
    {
        // Núcleo: bola verde brillante que tiembla.
        var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(blob.GetComponent<Collider>());
        blob.transform.SetParent(transform, false);
        blob.transform.localScale = Vector3.one * .32f;
        var br = blob.GetComponent<MeshRenderer>();
        br.sharedMaterial = mats.blob;
        br.shadowCastingMode = ShadowCastingMode.Off;
        blob.AddComponent<VenomAcidWobble>();

        // Halo.
        var halo = MakeSystem("Halo", transform, mats.glow, true);
        var hm = halo.main;
        hm.loop = true; hm.startLifetime = .12f; hm.startSize = .9f; hm.startSpeed = 0f;
        hm.startColor = new Color(Acid.r, Acid.g, Acid.b, .55f);
        hm.simulationSpace = ParticleSystemSimulationSpace.Local;
        var he = halo.emission; he.rateOverTime = 40f;
        halo.Play();

        // Estela.
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = mats.glow;
        trail.time = .25f;
        trail.minVertexDistance = .05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, .35f), new Keyframe(1f, 0f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(.8f, 1f, .5f), 0f), new GradientColorKey(Acid, .3f), new GradientColorKey(AcidDark, 1f) },
                  new[] { new GradientAlphaKey(.9f, 0f), new GradientAlphaKey(.5f, .5f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = ShadowCastingMode.Off;

        // Gotas que se desprenden y caen.
        drips = MakeSystem("Gotas", transform, mats.glow, true);
        var dm = drips.main;
        dm.loop = true; dm.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .6f);
        dm.startSize = new ParticleSystem.MinMaxCurve(.06f, .13f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .8f);
        dm.gravityModifier = 1.2f;
        dm.startColor = new ParticleSystem.MinMaxGradient(Acid, AcidDark);
        dm.simulationSpace = ParticleSystemSimulationSpace.World;
        var de = drips.emission; de.rateOverTime = 0f; de.rateOverDistance = 9f;
        var ds = drips.shape; ds.enabled = true; ds.shapeType = ParticleSystemShapeType.Sphere; ds.radius = .1f;
        FadeOut(drips);
        drips.Play();

        var light = new GameObject("Luz").AddComponent<Light>();
        light.transform.SetParent(transform, false);
        light.type = LightType.Point; light.color = Acid; light.intensity = 2.5f; light.range = 3.5f;
    }

    void Update()
    {
        life += Time.deltaTime;
        velocity += Vector3.down * gravity * Time.deltaTime;
        Vector3 step = velocity * Time.deltaTime;
        foreach (var hit in Physics.SphereCastAll(transform.position, .16f, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (owner && hit.collider.transform.IsChildOf(owner)) continue;
            Impact(hit.point, hit.normal, hit.collider);
            return;
        }
        transform.position += step;
        if (life > 4f) { Impact(transform.position, Vector3.up, null); }
    }

    void Impact(Vector3 point, Vector3 normal, Collider col)
    {
        if (!enabled) return;
        var player = col != null ? col.GetComponentInParent<GameJam.Multiplayer.MarsFusionAvatar>() : null;

        Splash(point, normal);
        // El charco va siempre al suelo (si pegó en el jugador, cae debajo de él).
        Vector3 ground = point;
        Vector3 up = normal;
        if (player || normal.y < .5f)
        {
            if (Physics.Raycast(point + Vector3.up * .5f, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)
                && !g.collider.GetComponentInParent<GameJam.Multiplayer.MarsFusionAvatar>())
            { ground = g.point; up = g.normal; }
        }
        VenomAcidPuddle.Create(ground, up, mats, puddleRadius, puddleTime);
        onImpact?.Invoke(ground);

        // La estela y las gotas se desvanecen solas antes de borrar el objeto.
        foreach (Transform c in transform) if (c.GetComponent<MeshRenderer>()) c.gameObject.SetActive(false);
        drips.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        var l = GetComponentInChildren<Light>(); if (l) l.enabled = false;
        enabled = false;
        Destroy(gameObject, .7f);
    }

    void Splash(Vector3 point, Vector3 normal)
    {
        var root = new GameObject("SalpicaduraAcido");
        root.transform.SetPositionAndRotation(point + normal * .05f, Quaternion.LookRotation(normal));

        var drops = MakeSystem("Gotas", root.transform, mats.glow, false);
        var m = drops.main;
        m.duration = .2f; m.loop = false;
        m.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .9f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        m.startSize = new ParticleSystem.MinMaxCurve(.08f, .18f);
        m.gravityModifier = 1.6f;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(.8f, 1f, .4f), Acid);
        var e = drops.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 34) });
        var sh = drops.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 60f; sh.radius = .1f;
        var r = drops.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .05f; r.lengthScale = 1.5f;
        FadeOut(drops);
        var col = drops.collision;
        col.enabled = true; col.type = ParticleSystemCollisionType.World; col.bounce = 0f; col.lifetimeLoss = .6f;
        drops.Play();

        var mist = MakeSystem("Vapor", root.transform, mats.glow, false);
        var mm = mist.main;
        mm.duration = .3f; mm.loop = false;
        mm.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.2f);
        mm.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.4f);
        mm.startSize = new ParticleSystem.MinMaxCurve(.5f, 1f);
        mm.gravityModifier = -.1f;
        mm.startColor = new Color(Acid.r, Acid.g, Acid.b, .35f);
        var me = mist.emission; me.rateOverTime = 0f; me.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });
        var ms = mist.shape; ms.enabled = true; ms.shapeType = ParticleSystemShapeType.Hemisphere; ms.radius = .3f;
        var sz = mist.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 1.6f));
        FadeOut(mist);
        mist.Play();

        var flash = new GameObject("Luz").AddComponent<Light>();
        flash.transform.SetParent(root.transform, false);
        flash.type = LightType.Point; flash.color = Acid; flash.intensity = 4f; flash.range = 4f;
        root.AddComponent<VenomAcidLightFade>();
        Destroy(root, 2.5f);
    }

    public static ParticleSystem MakeSystem(string name, Transform parent, Material mat, bool keepStopped)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var shape = ps.shape; shape.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    public static void FadeOut(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.7f, .5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }
}

// Charco de ácido (visual): se expande, burbujea y se seca.
public class VenomAcidPuddle : MonoBehaviour
{
    static readonly int OpacityColor = Shader.PropertyToID("_BaseColor");
    float radius, lifetime, born;
    MeshRenderer disc;
    MaterialPropertyBlock mpb;
    ParticleSystem bubbles;

    public static void Create(Vector3 at, Vector3 up, VenomAcid.Mats mats, float radius, float lifetime)
    {
        var go = new GameObject("CharcoAcido");
        go.transform.SetPositionAndRotation(at + up * .03f, Quaternion.LookRotation(-up) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
        var p = go.AddComponent<VenomAcidPuddle>();
        p.radius = radius; p.lifetime = lifetime; p.born = Time.time;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(go.transform, false);
        quad.transform.localScale = Vector3.one * radius * 2.2f;
        p.disc = quad.GetComponent<MeshRenderer>();
        p.disc.sharedMaterial = mats.puddle;
        p.disc.shadowCastingMode = ShadowCastingMode.Off;
        p.mpb = new MaterialPropertyBlock();

        // Burbujas verdes que suben y revientan.
        p.bubbles = VenomAcid.MakeSystem("Burbujas", go.transform, mats.glow, true);
        p.bubbles.transform.rotation = Quaternion.LookRotation(up);
        var m = p.bubbles.main;
        m.loop = true; m.duration = 1f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1.1f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .6f);
        m.startSize = new ParticleSystem.MinMaxCurve(.06f, .16f);
        m.gravityModifier = -.05f;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(.7f, 1f, .3f, .9f), VenomAcid.Acid);
        var e = p.bubbles.emission; e.rateOverTime = 14f;
        var s = p.bubbles.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Circle; s.radius = radius * .8f;
        s.rotation = new Vector3(-90f, 0f, 0f);
        VenomAcid.FadeOut(p.bubbles);
        p.bubbles.Play();
        Object.Destroy(go, lifetime + 1.5f);
    }

    void Update()
    {
        float age = Time.time - born;
        float grow = Mathf.SmoothStep(.3f, 1f, Mathf.Clamp01(age / .25f));
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lifetime * .7f, lifetime, age));
        disc.transform.localScale = Vector3.one * radius * 2.2f * grow;
        disc.GetPropertyBlock(mpb);
        mpb.SetColor(OpacityColor, new Color(1f, 1f, 1f, fade));
        disc.SetPropertyBlock(mpb);
        if (age > lifetime) { bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting); enabled = false; return; }
    }
}

public class VenomAcidWobble : MonoBehaviour
{
    Vector3 baseScale;
    void Start() { baseScale = transform.localScale; }
    void Update()
    {
        float t = Time.time * 18f;
        transform.localScale = new Vector3(baseScale.x * (1f + .15f * Mathf.Sin(t)), baseScale.y * (1f + .15f * Mathf.Sin(t + 2f)), baseScale.z * (1f + .15f * Mathf.Sin(t + 4f)));
    }
}

public class VenomAcidLightFade : MonoBehaviour
{
    Light l; float i0, t;
    void Start() { l = GetComponentInChildren<Light>(); if (l) i0 = l.intensity; }
    void Update() { t += Time.deltaTime; if (l) l.intensity = Mathf.Lerp(i0, 0f, t / .35f); }
}
