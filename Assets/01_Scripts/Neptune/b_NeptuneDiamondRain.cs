using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed partial class b_NeptuneDiamondRain : MonoBehaviour
{
    public Material diamondMaterial;
    public Material impactMaterial;
    public AudioClip impactSound;
    public float firstRainDelay = 18;
    public float rainInterval = 65;
    public float rainDuration = 22;
    [Range(0, 120)] public float diamondsPerSecond = 48;
    [Range(0, .2f)] public float impactVolume = .035f;

    ParticleSystem rain, impacts;
    AudioSource audioSource;
    Camera viewCamera;
    Mesh diamondMesh;
    AudioClip generatedChime;
    double elapsed, networkClock;
    float nextSound;
    bool hasNetworkClock;
    public bool IsRaining { get; private set; }
    public float SecondsUntilRain { get; private set; }
    public float SecondsRemaining { get; private set; }
    readonly List<ParticleCollisionEvent> collisions = new List<ParticleCollisionEvent>(32);

    void OnEnable()
    {
        if (diamondMaterial == null || impactMaterial == null) return;
        elapsed = 0;
        nextSound = 0;
        diamondMesh = CreateDiamondMesh();
        rain = MakeSystem("Diamond Rain - Cosmetic", 320);
        var main = rain.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4, 5);
        main.startSize = new ParticleSystem.MinMaxCurve(.07f, .17f);
        main.startColor = new Color(.65f, .88f, 1);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI*2);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI*2);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI*2);
        var shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(48, 5, 48);
        var velocity = rain.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(2f, 4f);
        velocity.y = new ParticleSystem.MinMaxCurve(-12f, -8f);
        velocity.z = new ParticleSystem.MinMaxCurve(-2f, -1f);
        var rotation = rain.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = .9f; rotation.y = 1.4f; rotation.z = .6f;
        var collision = rain.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.enableDynamicColliders = false;
        collision.collidesWith = 1; // Terrain and static ice; player is on IgnoreRaycast.
        collision.lifetimeLoss = 1;
        collision.sendCollisionMessages = true;
        var rainRenderer = rain.GetComponent<ParticleSystemRenderer>();
        rainRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        rainRenderer.mesh = diamondMesh;
        rainRenderer.sharedMaterial = diamondMaterial;
        var relay = rain.gameObject.AddComponent<b_DiamondRainCollision>();
        relay.owner = this;

        impacts = MakeSystem("Diamond Impacts - Small Glints", 192);
        var impactMain = impacts.main;
        impactMain.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .4f);
        impactMain.startSize = new ParticleSystem.MinMaxCurve(.05f, .13f);
        impactMain.startColor = new Color(.65f, .9f, 1, .35f);
        var impactShape = impacts.shape;
        impactShape.enabled = false;
        var fade = impacts.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        impacts.GetComponent<ParticleSystemRenderer>().sharedMaterial = impactMaterial;
        var sound = new GameObject("Diamond Impact Sound - Local");
        sound.transform.SetParent(transform, false);
        audioSource = sound.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1;
        audioSource.minDistance = 3;
        audioSource.maxDistance = 22;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.dopplerLevel = 0;
        audioSource.priority = 190;
        if (impactSound == null) generatedChime = CreateChime();
        viewCamera = Camera.main;
        FollowCamera();
        PreparePrefabRain();
        PrepareMapRain();
        if (prefabDrops != null) rainRenderer.enabled = false;
        rain.Play();
        impacts.Play();
    }

    ParticleSystem MakeSystem(string label, int maximum)
    {
        var child = new GameObject(label);
        child.layer = 2;
        child.transform.SetParent(transform, false);
        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startSpeed = 0;
        main.maxParticles = maximum;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = system.emission;
        emission.rateOverTime = 0;
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    void LateUpdate()
    {
        if (rain == null) return;
        elapsed = hasNetworkClock ? networkClock : elapsed + Time.deltaTime;
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
        FollowCamera();
        float envelope = EvaluateStorm(elapsed);
        var emission = rain.emission;
        emission.rateOverTime = prefabDrops == null && viewCamera != null ? diamondsPerSecond * envelope : 0;
        UpdatePrefabRain(envelope, Time.deltaTime);
        UpdateMapRain(envelope);
    }

    public void SetStormTime(double time)
    {
        if (double.IsNaN(time) || double.IsInfinity(time)) return;
        hasNetworkClock = true;
        networkClock = System.Math.Max(0, time);
        EvaluateStorm(networkClock);
    }

    public void ClearStormTime() { hasNetworkClock = false; }

    float EvaluateStorm(double time)
    {
        float duration = Mathf.Clamp(rainDuration, 3, 30);
        float period = Mathf.Max(duration+10, rainInterval);
        float delay = Mathf.Max(0, firstRainDelay);
        float phase = time < delay ? -1 : (float)((time-delay)%period);
        IsRaining = phase >= 0 && phase < duration;
        SecondsUntilRain = time < delay ? (float)(delay-time) : IsRaining ? 0 : period-phase;
        SecondsRemaining = IsRaining ? duration-phase : 0;
        return IsRaining ? Mathf.SmoothStep(0,1,phase)*Mathf.SmoothStep(0,1,(duration-phase)/2) : 0;
    }

    void FollowCamera()
    {
        if (rain == null || viewCamera == null) return;
        Vector3 target = viewCamera.transform.position + new Vector3(-6, 17, 2);
        if ((rain.transform.position-target).sqrMagnitude > 100*100) rain.Clear();
        if ((rain.transform.position-target).sqrMagnitude > 100*100) ClearPrefabRain();
        rain.transform.position = target;
    }

    public void EmitImpacts(GameObject other)
    {
        if (rain == null || impacts == null) return;
        int count = rain.GetCollisionEvents(other, collisions);
        for (int i = 0; i < Mathf.Min(count, 6); i++)
        {
            var hit = collisions[i];
            EmitImpact(hit.intersection, hit.normal, new Color(.65f, .9f, 1, .35f));
        }
    }

    void EmitImpact(Vector3 position, Vector3 normal, Color color)
    {
        if (impacts == null) return;
        var emission = new ParticleSystem.EmitParams {
            position = position + normal*.04f,
            velocity = normal*.6f + Random.insideUnitSphere*.4f,
            startColor = color
        };
        impacts.Emit(emission, 3);
        if (viewCamera == null || Time.time < nextSound || audioSource == null ||
            Vector3.SqrMagnitude(position-viewCamera.transform.position) > 18*18) return;
        nextSound = Time.time + .45f;
        audioSource.transform.position = position;
        audioSource.pitch = Random.Range(.92f, 1.12f);
        audioSource.PlayOneShot(impactSound != null ? impactSound : generatedChime, impactVolume);
    }

    static Mesh CreateDiamondMesh()
    {
        var points = new[] { Vector3.up*.8f, Vector3.right*.45f, Vector3.forward*.45f,
            Vector3.left*.45f, Vector3.back*.45f, Vector3.down*.65f };
        int[] faces = { 0,2,1, 0,3,2, 0,4,3, 0,1,4, 5,1,2, 5,2,3, 5,3,4, 5,4,1 };
        var vertices = new Vector3[24];
        var indices = new int[24];
        for (int i=0; i<24; i++) { vertices[i]=points[faces[i]]; indices[i]=i; }
        var mesh = new Mesh { name="Small Faceted Diamond" };
        mesh.vertices=vertices; mesh.triangles=indices;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    static AudioClip CreateChime()
    {
        const int rate=22050;
        var samples=new float[(int)(rate*.16f)];
        for (int i=0; i<samples.Length; i++)
        {
            float t=i/(float)rate;
            samples[i]=(Mathf.Sin(t*1600*2*Mathf.PI)*.45f + Mathf.Sin(t*2471*2*Mathf.PI)*.18f)
                * Mathf.Exp(-t*38) * Mathf.Clamp01(t/.003f);
        }
        var clip=AudioClip.Create("Diamond Chime",samples.Length,1,rate,false);
        clip.SetData(samples,0);
        return clip;
    }

    void OnDisable()
    {
        hasNetworkClock = false;
        IsRaining = false;
        DisposeMapRain();
        DisposePrefabRain();
        if (rain != null) Remove(rain.gameObject);
        if (impacts != null) Remove(impacts.gameObject);
        if (audioSource != null) { audioSource.Stop(); Remove(audioSource.gameObject); }
        if (diamondMesh != null) Remove(diamondMesh);
        if (generatedChime != null) Remove(generatedChime);
        rain=null; impacts=null; audioSource=null; diamondMesh=null; generatedChime=null;
    }

    static void Remove(Object asset)
    {
        if (asset is GameObject go) go.SetActive(false);
        if (Application.isPlaying) Destroy(asset); else DestroyImmediate(asset);
    }
}

public sealed class b_DiamondRainCollision : MonoBehaviour
{
    public b_NeptuneDiamondRain owner;
    void OnParticleCollision(GameObject other) { if (owner != null) owner.EmitImpacts(other); }
}
