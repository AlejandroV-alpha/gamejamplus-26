using UnityEngine;

[DisallowMultipleComponent]
public sealed class b_CrystalResourceGlow : MonoBehaviour
{
    public Material sparkleMaterial;
    public Color glowColor = new Color(.35f, .8f, 1f, .4f);
    [Range(0, 12)] public float particlesPerSecond = 4;
    [Range(.1f, 3)] public float radiusMultiplier = .65f;
    [Range(.1f, 4)] public float maximumRadius = 1.5f;
    [Header("Lights attached to floating particles")]
    [Range(0, 4)] public float particleLightIntensity = 1.4f;
    [Range(.1f, 4)] public float particleLightRange = 1.2f;

    Transform effectRoot;
    Renderer[] surfaces;
    ParticleSystem motes, halo;
    float radius;
    readonly Light[] particleLights = new Light[3];
    readonly ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[28];
    Camera viewCamera;

    void OnEnable()
    {
        if (sparkleMaterial == null || effectRoot != null) return;
        surfaces = GetComponentsInChildren<Renderer>(true);
        Bounds bounds = GetBounds();
        radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z)
            * radiusMultiplier, .18f, maximumRadius);
        effectRoot = new GameObject("Crystal Glow - Local Visual").transform;
        effectRoot.gameObject.layer = 2;
        effectRoot.SetParent(transform, false);
        PositionEffects(bounds);
        for (int i = 0; i < particleLights.Length; i++)
        {
            var lamp = new GameObject("Sparkle Light " + (i+1));
            lamp.layer = 2;
            lamp.transform.SetParent(effectRoot, false);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(glowColor.r, glowColor.g, glowColor.b, 1);
            light.range = particleLightRange;
            light.shadows = LightShadows.None;
            light.bounceIntensity = 0;
            light.enabled = false;
            particleLights[i] = light;
        }
        viewCamera = Camera.main;
        motes = CreateSystem("Floating Sparkles", 28, glowColor);
        var main = motes.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Clamp(radius*.055f,.018f,.06f),
            Mathf.Clamp(radius*.1f,.035f,.11f));
        var shape = motes.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        var velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalY = .65f;
        velocity.y = radius*.06f;
        var emission = motes.emission;
        emission.rateOverTime = particlesPerSecond;
        ApplyFade(motes);

        Color haloColor = glowColor;
        haloColor.a = .055f;
        halo = CreateSystem("Soft Base Glow", 3, haloColor);
        var haloMain = halo.main;
        haloMain.startLifetime = 3f;
        haloMain.startSize = radius*2.5f;
        var haloShape = halo.shape;
        haloShape.enabled = false;
        var haloEmission = halo.emission;
        haloEmission.rateOverTime = .6f;
        halo.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        ApplyFade(halo);
        halo.transform.position = new Vector3(bounds.center.x, bounds.min.y+.045f, bounds.center.z);
        motes.Play();
        halo.Play();
    }

    ParticleSystem CreateSystem(string label, int maximum, Color color)
    {
        var child = new GameObject(label);
        child.layer = 2;
        child.transform.SetParent(effectRoot, false);
        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startSpeed = 0;
        main.startColor = color;
        main.maxParticles = maximum;
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sparkleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    static void ApplyFade(ParticleSystem system)
    {
        var module = system.colorOverLifetime;
        module.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(1,.25f),new GradientAlphaKey(0,1)});
        module.color = gradient;
    }

    Bounds GetBounds()
    {
        var bounds = new Bounds(transform.position, Vector3.one*.4f);
        bool found = false;
        foreach (var renderer in surfaces)
        {
            if (renderer == null || renderer is ParticleSystemRenderer) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    void PositionEffects(Bounds bounds)
    {
        effectRoot.position = bounds.center;
        effectRoot.rotation = Quaternion.identity;
        Vector3 scale = transform.lossyScale;
        effectRoot.localScale = new Vector3(1/Mathf.Max(Mathf.Abs(scale.x),.001f),
            1/Mathf.Max(Mathf.Abs(scale.y),.001f),1/Mathf.Max(Mathf.Abs(scale.z),.001f));
    }

    void LateUpdate()
    {
        if (effectRoot == null) return;
        Bounds bounds = GetBounds();
        PositionEffects(bounds);
        if (halo != null) halo.transform.position = new Vector3(bounds.center.x, bounds.min.y+.045f, bounds.center.z);
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
        int count = motes != null ? motes.GetParticles(particleBuffer) : 0;
        for (int i = 0; i < particleLights.Length; i++)
        {
            var light = particleLights[i];
            if (light == null) continue;
            if (i >= count) { light.enabled = false; continue; }
            var particle = particleBuffer[i];
            Vector3 position = motes.transform.TransformPoint(particle.position);
            light.transform.position = position;
            float distance = viewCamera != null ? Vector3.Distance(viewCamera.transform.position, position) : 0;
            float fade = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(30,45,distance));
            Color particleColor = particle.GetCurrentColor(motes);
            float brightness = particleColor.a / Mathf.Max(glowColor.a, .001f);
            light.enabled = fade * brightness > .01f;
            light.intensity = particleLightIntensity * fade * brightness;
            light.range = particleLightRange;
        }
    }

    void OnDisable()
    {
        if (effectRoot == null) return;
        effectRoot.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(effectRoot.gameObject);
        else DestroyImmediate(effectRoot.gameObject);
        effectRoot = null;
        motes = null;
        halo = null;
        for (int i = 0; i < particleLights.Length; i++) particleLights[i] = null;
    }
}
