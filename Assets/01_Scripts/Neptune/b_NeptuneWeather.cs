using UnityEngine;

[DisallowMultipleComponent]
public sealed class b_NeptuneWeather : MonoBehaviour
{
    public Material iceParticleMaterial;
    [Range(0, 150)] public float calmEmission = 32f;
    [Range(0, 250)] public float gustEmission = 95f;
    public float gustInterval = 36f;
    public float gustDuration = 9f;
    public float calmFogDensity = .006f;
    public float gustFogDensity = .0085f;
    public Color fogColor = new Color(.15f, .13f, .3f, 1);
    public float GustStrength { get; private set; }

    ParticleSystem crystals;
    Camera viewCamera;
    float elapsed;
    bool previousFog;
    FogMode previousMode;
    float previousDensity;
    Color previousColor;

    void OnEnable()
    {
        previousFog = RenderSettings.fog;
        previousMode = RenderSettings.fogMode;
        previousDensity = RenderSettings.fogDensity;
        previousColor = RenderSettings.fogColor;
        elapsed = 0;
        GustStrength = 0;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = calmFogDensity;
        if (iceParticleMaterial == null)
        {
            Debug.LogWarning("Assign the Neptune ice particle material.", this);
            return;
        }
        var effect = new GameObject("Ice Crystals - Local Weather");
        effect.layer = 2;
        effect.transform.SetParent(transform, false);
        crystals = effect.AddComponent<ParticleSystem>();
        crystals.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = crystals.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.035f, .13f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = new Color(.72f, .87f, 1f, .28f);
        main.maxParticles = 900;
        main.cullingMode = ParticleSystemCullingMode.Automatic;
        var shape = crystals.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(65, 20, 65);
        var velocity = crystals.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(7f, 11f);
        velocity.y = new ParticleSystem.MinMaxCurve(-1.1f, -.4f);
        velocity.z = new ParticleSystem.MinMaxCurve(-3f, -1f);
        var noise = crystals.noise;
        noise.enabled = true;
        noise.strength = .45f;
        noise.frequency = .12f;
        noise.scrollSpeed = .15f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var fading = crystals.colorOverLifetime;
        fading.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f),
                new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
        fading.color = gradient;
        var emission = crystals.emission;
        emission.rateOverTime = calmEmission;
        var renderer = effect.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = iceParticleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.5f;
        renderer.velocityScale = .015f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        viewCamera = Camera.main;
        if (viewCamera != null) FollowCamera(true);
        crystals.Play();
    }

    void LateUpdate()
    {
        elapsed += Time.deltaTime;
        float interval = Mathf.Max(10f, gustInterval);
        float duration = Mathf.Clamp(gustDuration, 1f, interval - 1f);
        float phase = elapsed % interval;
        // Start with calm weather; gusts fade in and out instead of switching abruptly.
        float gust = phase > interval - duration
            ? Mathf.Pow(Mathf.Sin((phase - interval + duration) / duration * Mathf.PI), 2) : 0;
        GustStrength = gust;
        RenderSettings.fogDensity = Mathf.Lerp(calmFogDensity, gustFogDensity, gust);
        if (crystals == null) return;
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
        if (viewCamera != null) FollowCamera(false);
        var emission = crystals.emission;
        emission.rateOverTime = Mathf.Lerp(calmEmission, gustEmission, gust);
        var velocity = crystals.velocityOverLifetime;
        velocity.speedModifier = Mathf.Lerp(1f, 1.6f, gust);
    }

    void FollowCamera(bool initial)
    {
        Vector3 target = viewCamera.transform.position + new Vector3(-12, 5, 0);
        if (!initial && (crystals.transform.position - target).sqrMagnitude > 80f * 80f)
            crystals.Clear();
        crystals.transform.position = target;
    }

    void OnDisable()
    {
        RenderSettings.fog = previousFog;
        RenderSettings.fogMode = previousMode;
        RenderSettings.fogDensity = previousDensity;
        RenderSettings.fogColor = previousColor;
        if (crystals == null) return;
        crystals.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // Disable immediately so re-enabling the component cannot double the effect this frame.
        crystals.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(crystals.gameObject);
        else DestroyImmediate(crystals.gameObject);
        crystals = null;
    }
}
