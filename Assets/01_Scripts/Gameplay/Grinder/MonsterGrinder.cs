using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace GameJam.Grinder
{
    /// <summary>
    /// Trituradora de monstruos. Se le acerca un cadaver arrastrado, lo traga entre dos rodillos dentados,
    /// tiembla, salpica y al final escupe minerales por el tubo hacia la bandeja amarilla.
    /// La autoridad decide cuantos minerales salen (MaterialPickup en red); lo demas es presentacion local.
    /// </summary>
    public sealed class MonsterGrinder : NetworkBehaviour
    {
        public const float GrindSeconds = 4.2f;
        private const float EjectInterval = .55f, ChuteTravel = .5f;

        [Header("Puntos del modelo")]
        [SerializeField] private Transform mouth;
        [SerializeField] private Transform rollerA, rollerB;
        [SerializeField] private Transform shakeRoot;
        [SerializeField] private Transform[] chutePath;   // del interior al borde del tubo
        [SerializeField] private Transform traySpot;
        [SerializeField] private Transform ventPoint;
        [Header("Luces")]
        [SerializeField] private Renderer[] leds;          // naranja, amarillo, verde
        [SerializeField] private Renderer lightBar;
        [Header("Minerales que puede soltar")]
        [SerializeField] private NetworkObject[] mineralPrefabs;
        [SerializeField, Min(.5f)] private float intakeRadius = 2.6f;

        [Networked] private float BusyUntil { get; set; }
        [Networked] private int Pending { get; set; }
        [Networked] private float EjectAt { get; set; }
        [Networked] private int EjectCount { get; set; }
        [Networked] private int FeedCount { get; set; }

        private static readonly List<MonsterGrinder> all = new List<MonsterGrinder>();
        public Vector3 MouthPoint => mouth != null ? mouth.position : transform.position + Vector3.up * 1.4f;
        public Vector3 TrayPoint => traySpot != null ? traySpot.position : transform.position;
        public bool CanAccept => Object != null && Object.IsValid && !Busy;
        private bool Busy => Runner != null && (Runner.SimulationTime < BusyUntil || Pending > 0);

        private float spin, activity, gemLaunchedFor = -1;
        private int seenFeed = -1, seenEject = -1;
        private Vector3 shakeRest;
        private Color[] ledBase;
        private Material[] ledMats;
        private Material barMat;
        private ParticleSystem gore, smoke, puff;
        private AudioSource grindLoop, sfx;
        private static AudioClip grindClip, clunkClip, popClip;
        private readonly List<(Transform t, float start)> gems = new List<(Transform, float)>();

        public static MonsterGrinder Nearest(Vector3 p)
        {
            MonsterGrinder best = null; float bestD = float.MaxValue;
            foreach (var g in all)
            {
                if (g == null) continue;
                float d = (g.MouthPoint - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = g; }
            }
            return best;
        }

        public bool InIntake(Vector3 p)
        {
            Vector3 d = p - MouthPoint; d.y = 0;
            return d.magnitude < intakeRadius;
        }

        public override void Spawned()
        {
            if (!all.Contains(this)) all.Add(this);
            seenFeed = FeedCount; seenEject = EjectCount;
            SetupPresentation();
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => all.Remove(this);
        private void OnDestroy() => all.Remove(this);

        // ------------------------------------------------------------------ autoridad

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Feed(NetworkObject remains, int value)
        {
            float now = Runner.SimulationTime;
            Pending += Mathf.Clamp(value, 1, 10);
            BusyUntil = now + GrindSeconds;
            if (Pending == value) EjectAt = now + GrindSeconds * .72f;
            FeedCount++;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || Pending <= 0 || Runner.SimulationTime < EjectAt) return;
            if (mineralPrefabs != null && mineralPrefabs.Length > 0)
            {
                var prefab = mineralPrefabs[Random.Range(0, mineralPrefabs.Length)];
                Vector3 spot = traySpot != null ? traySpot.position : transform.position;
                Vector2 jitter = Random.insideUnitCircle * .22f;
                if (prefab != null)
                    Runner.Spawn(prefab, spot + new Vector3(jitter.x, .25f, jitter.y), Quaternion.Euler(0, Random.Range(0, 360f), 0));
            }
            Pending--;
            EjectCount++;
            EjectAt = Runner.SimulationTime + EjectInterval;
        }

        // ------------------------------------------------------------------ presentacion

        private void SetupPresentation()
        {
            if (shakeRoot != null) shakeRest = shakeRoot.localPosition;
            if (leds != null)
            {
                ledMats = new Material[leds.Length]; ledBase = new Color[leds.Length];
                for (int i = 0; i < leds.Length; i++)
                {
                    if (leds[i] == null) continue;
                    ledMats[i] = leds[i].material;
                    ledBase[i] = ledMats[i].GetColor("_BaseColor");
                    ledMats[i].EnableKeyword("_EMISSION");
                }
            }
            if (lightBar != null) { barMat = lightBar.material; barMat.EnableKeyword("_EMISSION"); }

            gore = BuildGore();
            smoke = BuildSmoke();
            puff = BuildPuff();

            if (grindClip == null) BuildClips();
            grindLoop = gameObject.AddComponent<AudioSource>();
            grindLoop.clip = grindClip; grindLoop.loop = true; grindLoop.volume = 0f;
            grindLoop.spatialBlend = 1f; grindLoop.minDistance = 3f; grindLoop.maxDistance = 35f;
            grindLoop.Play();
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.spatialBlend = 1f; sfx.minDistance = 3f; sfx.maxDistance = 35f;
        }

        public override void Render()
        {
            float now = Runner.SimulationTime;
            bool busy = Busy;
            bool grinding = now < BusyUntil;
            activity = Mathf.MoveTowards(activity, grinding ? 1f : busy ? .45f : 0f, Time.deltaTime * (grinding ? 3f : 1.2f));

            // Nuevo cadaver: golpe seco y salpicadura inicial.
            if (FeedCount != seenFeed)
            {
                if (seenFeed >= 0 && clunkClip != null) sfx.PlayOneShot(clunkClip, .9f);
                seenFeed = FeedCount;
                if (gore != null) gore.Emit(40);
            }

            // Rodillos: giro lento en reposo, rapidisimo al triturar, en sentidos opuestos.
            spin += Time.deltaTime * Mathf.Lerp(35f, 900f, activity);
            if (rollerA != null) rollerA.localRotation = Quaternion.Euler(spin, 0, 0);
            if (rollerB != null) rollerB.localRotation = Quaternion.Euler(-spin, 0, 0);

            // Temblor de la carcasa.
            if (shakeRoot != null)
            {
                float a = .035f * activity;
                shakeRoot.localPosition = shakeRest + new Vector3(Mathf.Sin(Time.time * 61f), Mathf.Abs(Mathf.Sin(Time.time * 37f)) * .6f, Mathf.Cos(Time.time * 53f)) * a;
            }

            // Particulas mientras tritura.
            if (gore != null) { var em = gore.emission; em.rateOverTime = grinding ? 38f * activity : 0f; }
            if (smoke != null) { var em = smoke.emission; em.rateOverTime = Mathf.Lerp(0f, 14f, activity); }

            // Sonido continuo con un ligero vaiven de tono.
            if (grindLoop != null)
            {
                grindLoop.volume = Mathf.Lerp(.04f, .75f, activity);
                grindLoop.pitch = Mathf.Lerp(.7f, 1f + .06f * Mathf.Sin(Time.time * 9f), activity);
            }

            UpdateLights(busy, grinding);

            // Mineral cosmetico bajando por el tubo justo antes de que aparezca el de verdad.
            if (Pending > 0 && EjectAt - now < ChuteTravel && gemLaunchedFor != EjectAt)
            {
                gemLaunchedFor = EjectAt;
                LaunchGem();
            }
            if (EjectCount != seenEject)
            {
                seenEject = EjectCount;
                if (puff != null && traySpot != null) { puff.transform.position = traySpot.position + Vector3.up * .2f; puff.Emit(14); }
                if (popClip != null) sfx.PlayOneShot(popClip, .8f);
            }
            AnimateGems();
        }

        private void UpdateLights(bool busy, bool grinding)
        {
            float t = Time.time;
            if (ledMats != null)
                for (int i = 0; i < ledMats.Length; i++)
                {
                    if (ledMats[i] == null) continue;
                    float on;
                    if (grinding) on = ((int)(t * 8f) % 3) == i ? 1f : .15f;              // luces en carrera
                    else if (busy) on = i == 1 ? .5f + .5f * Mathf.Sin(t * 10f) : .15f;    // expulsando
                    else on = i == 2 ? .75f + .25f * Mathf.Sin(t * 2f) : .12f;             // lista: verde
                    ledMats[i].SetColor("_EmissionColor", ledBase[i] * (on * 3f));
                }
            if (barMat != null)
            {
                Color c = grinding ? Color.Lerp(new Color(1f, .45f, .2f), new Color(1f, .85f, .5f), .5f + .5f * Mathf.Sin(t * 14f))
                    : new Color(.9f, .97f, 1f);
                barMat.SetColor("_EmissionColor", c * (grinding ? 2.6f : 1.4f));
            }
        }

        private void LaunchGem()
        {
            if (chutePath == null || chutePath.Length < 2) return;
            var gem = new GameObject("ChuteMineral", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            gem.GetComponent<MeshFilter>().sharedMesh = GemMesh();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Color c = Color.HSVToRGB(Random.value < .5f ? Random.Range(.48f, .58f) : Random.Range(.0f, .08f), .75f, 1f);
            mat.SetColor("_BaseColor", c);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c * 1.6f);
            gem.GetComponent<MeshRenderer>().sharedMaterial = mat;
            gem.localScale = Vector3.one * .22f;
            gem.position = chutePath[0].position;
            gems.Add((gem, Time.time));
        }

        private void AnimateGems()
        {
            for (int i = gems.Count - 1; i >= 0; i--)
            {
                var (gem, start) = gems[i];
                float t = (Time.time - start) / ChuteTravel;
                if (gem == null || t >= 1f)
                {
                    if (gem != null) Destroy(gem.GetComponent<MeshRenderer>().sharedMaterial);
                    if (gem != null) Destroy(gem.gameObject);
                    gems.RemoveAt(i);
                    continue;
                }
                // Recorre el tubo acelerando, como si cayera.
                float e = t * t;
                float f = e * (chutePath.Length - 1);
                int seg = Mathf.Min((int)f, chutePath.Length - 2);
                gem.position = Vector3.Lerp(chutePath[seg].position, chutePath[seg + 1].position, f - seg);
                gem.rotation = Quaternion.Euler(t * 720f, t * 540f, 0);
            }
        }

        // ------------------------------------------------------------------ efectos construidos en codigo

        private static Material particleLit, particleUnlit;

        private ParticleSystem MakeSystem(string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0;
            return ps;
        }

        private ParticleSystem BuildGore()
        {
            if (particleLit == null) { particleLit = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit")); particleLit.SetFloat("_Smoothness", .7f); particleLit.enableInstancing = true; }
            var ps = MakeSystem("GrinderGore", MouthPoint + Vector3.up * .2f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.07f, .2f);
            main.gravityModifier = 2.2f;
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0, 6.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.45f, .05f, .1f), new Color(.35f, .55f, .15f));
            main.maxParticles = 400;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 28; shape.radius = .5f;
            shape.rotation = new Vector3(-90, 0, 0);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = rot.y = rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.bounce = .25f; col.lifetimeLoss = .3f;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(.8f, 1), new Keyframe(1, 0)));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            r.sharedMaterial = particleLit;
            ps.Play();
            return ps;
        }

        private ParticleSystem BuildSmoke()
        {
            if (particleUnlit == null) particleUnlit = SoftParticleMaterial();
            var ps = MakeSystem("GrinderSmoke", ventPoint != null ? ventPoint.position : MouthPoint);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.35f, .7f);
            main.startColor = new Color(.82f, .78f, .72f, .45f);
            main.gravityModifier = -.05f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12; shape.radius = .15f;
            shape.rotation = new Vector3(-90, 0, 0);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .6f, 1, 2.2f));
            var colr = ps.colorOverLifetime; colr.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(0, 1) });
            colr.color = g;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleUnlit;
            ps.Play();
            return ps;
        }

        private ParticleSystem BuildPuff()
        {
            if (particleUnlit == null) particleUnlit = SoftParticleMaterial();
            var ps = MakeSystem("ChutePuff", traySpot != null ? traySpot.position : transform.position);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.12f, .3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.55f, 1f, 1f, .9f), new Color(1f, .9f, .4f, .9f));
            main.gravityModifier = .4f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = .15f;
            shape.rotation = new Vector3(-90, 0, 0);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleUnlit;
            ps.Play();
            return ps;
        }

        private static Material SoftParticleMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(shader);
            // Textura redonda suave generada (sin depender de assets).
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
                }
            tex.Apply();
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }

        private static Mesh gemMesh;
        private static Mesh GemMesh()
        {
            if (gemMesh != null) return gemMesh;
            Vector3[] p = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            int[][] faces = { new[] { 0, 4, 2 }, new[] { 0, 2, 5 }, new[] { 0, 5, 3 }, new[] { 0, 3, 4 }, new[] { 1, 2, 4 }, new[] { 1, 5, 2 }, new[] { 1, 3, 5 }, new[] { 1, 4, 3 } };
            var v = new List<Vector3>(); var tris = new List<int>();
            foreach (var f in faces) { foreach (var i in f) { tris.Add(v.Count); v.Add(p[i] * (i < 2 ? 1.3f : 1f)); } }
            gemMesh = new Mesh { name = "Gem" };
            gemMesh.SetVertices(v); gemMesh.SetTriangles(tris, 0); gemMesh.RecalculateNormals();
            return gemMesh;
        }

        // Sonidos sintetizados: molienda (ruido grave modulado), golpe metalico y "pop" del mineral.
        private static void BuildClips()
        {
            const int rate = 44100;
            var rng = new System.Random(7);
            int n = rate * 2;
            var d = new float[n];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += (noise - lp) * .08f; lp2 += (lp - lp2) * .2f;
                float crunch = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * Mathf.PI * 11f)), 6f);          // dientes que muerden
                float motor = Mathf.Sin(2 * Mathf.PI * 55f * t) * .35f + Mathf.Sin(2 * Mathf.PI * 110f * t) * .15f;
                d[i] = (lp2 * (1.4f + 2.2f * crunch) + motor * .5f) * .6f;
            }
            // Fundido en los extremos para que el bucle no haga clic.
            for (int i = 0; i < 2000; i++) { float k = i / 2000f; d[i] *= k; d[n - 1 - i] *= k; }
            grindClip = AudioClip.Create("GrinderLoop", n, 1, rate, false); grindClip.SetData(d, 0);

            int m = (int)(rate * .5f); var c = new float[m];
            for (int i = 0; i < m; i++)
            {
                float t = i / (float)rate;
                c[i] = (Mathf.Sin(2 * Mathf.PI * 70f * t) * Mathf.Exp(-t * 9f) + ((float)rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 40f) * .6f
                    + Mathf.Sin(2 * Mathf.PI * 410f * t) * Mathf.Exp(-t * 14f) * .25f) * .8f;
            }
            clunkClip = AudioClip.Create("GrinderClunk", m, 1, rate, false); clunkClip.SetData(c, 0);

            int k2 = (int)(rate * .25f); var p = new float[k2];
            for (int i = 0; i < k2; i++)
            {
                float t = i / (float)rate;
                p[i] = Mathf.Sin(2 * Mathf.PI * (900f + 900f * t) * t) * Mathf.Exp(-t * 18f) * .5f;
            }
            popClip = AudioClip.Create("GrinderPop", k2, 1, rate, false); popClip.SetData(p, 0);
        }
    }
}
