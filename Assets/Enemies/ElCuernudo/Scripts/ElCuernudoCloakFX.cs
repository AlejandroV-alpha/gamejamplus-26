using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Efectos del camuflaje de El Cuernudo (solo visual, corre en todos los clientes):
//  - Se disuelve en partículas que salen de su propia piel + onda de polvo, y se vuelve invisible.
//  - Invisible, cada pisada deja una huella de garra en la arena y levanta polvo: eso lo delata.
//  - De vez en cuando su silueta parpadea un instante.
//  - Al revelarse, las partículas vuelven a brotar y reaparece con un destello.
// Los assets salen de Resources/ElCuernudoFX (los crea Herramientas > El Cuernudo).
public sealed class ElCuernudoCloakFX : MonoBehaviour
{
    const string Res = "ElCuernudoFX/";
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    static readonly Color Shimmer = new Color(.25f, 1f, .85f);

    [Header("Camuflaje")]
    public float fadeOutTime = .9f;
    public float fadeInTime = .55f;

    [Header("Huellas")]
    public float printLifetime = 12f;
    [Tooltip("Ancho y largo de la huella por unidad de tamaño.")]
    public Vector2 printSize = new Vector2(.42f, .52f);

    float size = 1f;
    float visibility = 1f;           // 1 visible, 0 invisible
    bool cloaked, transparentMode;
    float blinkUntil, nextBlinkAt;

    Renderer[] bodyRenderers;
    Material[][] opaqueMats, cloakMats;
    MaterialPropertyBlock mpb;
    SkinnedMeshRenderer skin;

    ParticleSystem motes, dust;
    Transform leftFoot, rightFoot;
    readonly bool[] footDown = new bool[2];
    readonly Vector3[] lastPrint = new Vector3[2];

    Material printMat;
    readonly List<Print> prints = new List<Print>();
    int nextPrint;
    Transform printRoot;

    sealed class Print
    {
        public GameObject go;
        public MeshRenderer mr;
        public float born = -999f;
    }

    public bool IsCloaked => cloaked;
    public float Visibility => visibility;

    public void Init(float worldSize)
    {
        size = Mathf.Max(.1f, worldSize);
        mpb = new MaterialPropertyBlock();

        bodyRenderers = GetComponentsInChildren<Renderer>(true);
        skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
        var cloakBase = Resources.Load<Material>(Res + "ElCuernudo_Cloak");
        opaqueMats = new Material[bodyRenderers.Length][];
        cloakMats = new Material[bodyRenderers.Length][];
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            opaqueMats[i] = bodyRenderers[i].sharedMaterials;
            cloakMats[i] = new Material[opaqueMats[i].Length];
            for (int m = 0; m < opaqueMats[i].Length; m++)
                cloakMats[i][m] = cloakBase != null ? cloakBase : opaqueMats[i][m];
        }

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "LeftFoot") leftFoot = t;
            else if (t.name == "RightFoot") rightFoot = t;
        }

        motes = BuildMotes();
        dust = BuildDust();
        BuildPrintPool();
        nextBlinkAt = Time.time + Random.Range(2.5f, 5f);
    }

    void OnDestroy()
    {
        if (printRoot != null) Destroy(printRoot.gameObject);
        if (dust != null) Destroy(dust.gameObject);
    }

    // ---------------------------------------------------------------- estado

    public void SetCloaked(bool value)
    {
        if (value == cloaked) return;
        cloaked = value;
        if (!value)
        {
            // Que la malla esté activa para que las partículas puedan brotar de su piel.
            visibility = Mathf.Max(visibility, .02f);
            ApplyVisibility();
        }
        BurstFromBody(value ? 1f : .8f);
        DustRing(value ? 1f : .7f);
        if (value) nextBlinkAt = Time.time + Random.Range(2.5f, 5f);
    }

    // Reaparece al instante (por ejemplo al morir) sin ráfaga.
    public void ForceVisible()
    {
        cloaked = false;
        visibility = 1f;
        ApplyVisibility();
    }

    void LateUpdate()
    {
        if (bodyRenderers == null) return;

        float target = cloaked ? 0f : 1f;
        float speed = 1f / Mathf.Max(.05f, cloaked ? fadeOutTime : fadeInTime);
        visibility = Mathf.MoveTowards(visibility, target, speed * Time.deltaTime);

        // Parpadeo fugaz de la silueta mientras está invisible.
        if (cloaked && visibility <= 0f && Time.time >= nextBlinkAt)
        {
            blinkUntil = Time.time + Random.Range(.08f, .16f);
            nextBlinkAt = Time.time + Random.Range(3f, 6f);
        }
        ApplyVisibility();
        UpdateFootprints();
        FadePrints();
    }

    void ApplyVisibility()
    {
        bool blink = cloaked && Time.time < blinkUntil;
        bool fullyVisible = visibility >= 1f && !blink;
        bool hidden = visibility <= 0f && !blink;

        bool wantTransparent = !fullyVisible;
        if (wantTransparent != transparentMode)
        {
            transparentMode = wantTransparent;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null && !(bodyRenderers[i] is ParticleSystemRenderer))
                    bodyRenderers[i].sharedMaterials = transparentMode ? cloakMats[i] : opaqueMats[i];
        }

        // Al disolverse brilla con un borde verde-agua que se apaga; tiembla un poco.
        float v = Mathf.Clamp01(visibility);
        float flicker = 1f + .25f * Mathf.Sin(Time.time * 55f) * (1f - v) * v * 4f;
        float alpha = blink ? .14f : Mathf.Clamp01(v * v * flicker);
        float glow = blink ? .6f : Mathf.Sin(v * Mathf.PI) * 1.6f;

        foreach (var r in bodyRenderers)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            r.enabled = !hidden;
            r.shadowCastingMode = v > .5f && !blink ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (!transparentMode) { r.SetPropertyBlock(null); continue; }
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
            mpb.SetColor(EmissionId, Shimmer * glow);
            r.SetPropertyBlock(mpb);
        }
    }

    // ---------------------------------------------------------------- partículas

    ParticleSystem BuildMotes()
    {
        var go = new GameObject("CloakMotes");
        go.transform.SetParent(skin != null ? skin.transform : transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.1f * size, .6f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(.03f * size, .09f * size);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.3f, 1f, .85f), new Color(1f, .3f, .7f));
        main.gravityModifier = -.08f;
        main.maxParticles = 1500;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        if (skin != null)
        {
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = skin;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.normalOffset = .02f;
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .8f * size;
        }

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = .6f * size;
        noise.frequency = .8f;
        noise.scrollSpeed = .6f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.y = new ParticleSystem.MinMaxCurve(.2f * size, .9f * size);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.3f, 1f, .85f), .35f), new GradientColorKey(new Color(.9f, .3f, .9f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .08f), new GradientAlphaKey(.7f, .5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = Resources.Load<Material>(Res + "ElCuernudo_FX_Glow");
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    ParticleSystem BuildDust()
    {
        // En espacio de mundo y sin padre: el polvo de las huellas se queda donde pisó.
        var go = new GameObject("ElCuernudo_Dust");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.25f * size, .6f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.78f, .42f, .25f, .55f), new Color(.6f, .3f, .18f, .4f));
        main.gravityModifier = -.01f;
        main.maxParticles = 600;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = false;

        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 2.5f;
        drag.multiplyDragByParticleSize = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 1.8f));

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-.6f, .6f);

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = Resources.Load<Material>(Res + "ElCuernudo_FX_Dust");
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    void BurstFromBody(float power)
    {
        if (motes == null) return;
        motes.Emit(Mathf.RoundToInt(420 * power));
    }

    void DustRing(float power)
    {
        if (dust == null) return;
        Vector3 c = transform.position + Vector3.up * .1f * size;
        int n = Mathf.RoundToInt(36 * power);
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n + Random.Range(-.1f, .1f);
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            p.position = c + dir * .5f * size;
            p.velocity = dir * Random.Range(2.2f, 3.6f) * size + Vector3.up * Random.Range(.1f, .5f) * size;
            p.startSize = Random.Range(.4f, .8f) * size;
            dust.Emit(p, 1);
        }
    }

    void DustPuff(Vector3 at, Vector3 forward)
    {
        if (dust == null) return;
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < 7; i++)
        {
            var side = Random.insideUnitCircle;
            var dir = (new Vector3(side.x, 0f, side.y) + forward * .4f).normalized;
            p.position = at + Vector3.up * .05f * size;
            p.velocity = dir * Random.Range(.6f, 1.3f) * size + Vector3.up * Random.Range(.2f, .6f) * size;
            p.startSize = Random.Range(.18f, .36f) * size;
            dust.Emit(p, 1);
        }
    }

    // ---------------------------------------------------------------- huellas

    void BuildPrintPool()
    {
        printMat = Resources.Load<Material>(Res + "ElCuernudo_Footprint");
        if (printMat == null) return;
        printRoot = new GameObject("ElCuernudo_Footprints").transform;
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        for (int i = 0; i < 28; i++)
        {
            var go = new GameObject("Print");
            go.transform.SetParent(printRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = printMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            prints.Add(new Print { go = go, mr = mr });
        }
    }

    void UpdateFootprints()
    {
        StepFoot(0, leftFoot);
        StepFoot(1, rightFoot);
    }

    void StepFoot(int i, Transform foot)
    {
        if (foot == null) return;
        if (!GroundUnder(foot.position, out var hit)) return;
        float h = foot.position.y - hit.point.y;
        if (!footDown[i] && h < .09f * size)
        {
            footDown[i] = true;
            if ((hit.point - lastPrint[i]).sqrMagnitude > (.3f * size) * (.3f * size))
            {
                lastPrint[i] = hit.point;
                var fwd = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
                // La huella queda un poco por delante del tobillo, donde apoyan las garras.
                var at = hit.point + fwd * .18f * size;
                PlacePrint(at, hit.normal, fwd, i == 1);
                if (cloaked) DustPuff(at, fwd);
            }
        }
        else if (footDown[i] && h > .14f * size) footDown[i] = false;
    }

    bool GroundUnder(Vector3 from, out RaycastHit best)
    {
        best = default;
        float bestDist = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(from + Vector3.up * .5f * size, Vector3.down, 2f * size, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform.root) || hit.distance >= bestDist) continue;
            bestDist = hit.distance;
            best = hit;
        }
        return bestDist < float.MaxValue;
    }

    void PlacePrint(Vector3 at, Vector3 normal, Vector3 fwd, bool right)
    {
        if (prints.Count == 0) return;
        var p = prints[nextPrint];
        nextPrint = (nextPrint + 1) % prints.Count;
        p.go.SetActive(true);
        p.go.transform.position = at + normal * .015f;
        p.go.transform.rotation = Quaternion.LookRotation(-normal, fwd) * Quaternion.Euler(0, 0, right ? -6f : 6f);
        p.go.transform.localScale = new Vector3((right ? -1f : 1f) * printSize.x * size, printSize.y * size, 1f);
        p.born = Time.time;
    }

    void FadePrints()
    {
        foreach (var p in prints)
        {
            if (!p.go.activeSelf) continue;
            float age = (Time.time - p.born) / printLifetime;
            if (age >= 1f) { p.go.SetActive(false); continue; }
            // Entra marcada, aguanta y se desvanece en el último tercio.
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.65f, 1f, age));
            p.mr.GetPropertyBlock(mpb);
            mpb.SetFloat(OpacityId, opacity);
            p.mr.SetPropertyBlock(mpb);
        }
    }
}
