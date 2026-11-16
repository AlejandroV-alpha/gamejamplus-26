using System.IO;
using GameJam.Combat;
using UnityEditor;
using UnityEngine;

namespace GameJam.EditorTools
{
    /// <summary>
    /// Impactos de bala según la superficie: arena, metal/objetos duros y enemigos.
    /// Reutiliza los materiales de <see cref="GunVfxBuilder"/> y asigna los prefabs a las pistolas.
    /// </summary>
    public static class GunImpactVfxBuilder
    {
        private const string Root = "Assets/02_Prefabs/Gun/VFX";
        private const string SandPath = Root + "/VFX_Impacto_Arena.prefab";
        private const string HardPath = Root + "/VFX_Impacto_Metal.prefab";
        private const string EnemyPath = Root + "/VFX_Impacto_Enemigo.prefab";
        private const string GoldBoltPath = Root + "/VFX_BalaDorada.prefab";
        private const string GoldMuzzlePath = Root + "/VFX_DisparoDorado.prefab";

        private static readonly string[] GunHolders =
        {
            "Assets/02_Prefabs/Gun/Gun.prefab",
            "Assets/02_Prefabs/Fusion/A_FusionAstronaut.prefab",
            "Assets/02_Prefabs/Fusion/A_CombatAstronaut.prefab",
            "Assets/02_Prefabs/Fusion/A_SurvivalAstronaut.prefab", // jugador de la escena Mars
        };

        private static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        private static readonly Color HotWhite = new Color(1f, 0.97f, 0.85f);
        private static readonly Color Orange = new Color(1f, 0.45f, 0.1f);
        private static readonly Color Ember = new Color(0.55f, 0.08f, 0.02f);
        private static readonly Color Sand = new Color(0.78f, 0.42f, 0.24f);

        private static Material glowAdd, sparkAdd, ringAdd, smokeAlpha, sandLit, scorchAlpha;

        private const string DoneKey = "GunImpactVfxBuilder_v6_survival";

        [InitializeOnLoadMethod]
        private static void AutoBuildOnce()
        {
            if (EditorPrefs.GetBool(DoneKey, false)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(Root + "/M_VFX_Glow.mat")) return; // necesita los materiales de GunVfxBuilder
                EditorPrefs.SetBool(DoneKey, true);
                Build();
            };
        }

        [MenuItem("Tools/GameJam/Mejorar Impactos de Bala (VFX)")]
        public static void Build()
        {
            if (!LoadMaterials())
            {
                Debug.LogWarning("[ImpactVfx] Faltan los materiales de GunVfxBuilder. Ejecuta primero Tools/GameJam/Crear Efectos de Disparo (VFX).");
                return;
            }
            GameObject sand = Save(BuildSand(), SandPath);
            GameObject hard = Save(BuildHard(), HardPath);
            GameObject enemy = Save(BuildEnemy(), EnemyPath);
            GameObject bolt = Save(BuildGoldBolt(), GoldBoltPath);
            GameObject muzzle = Save(BuildGoldMuzzle(), GoldMuzzlePath);
            int guns = Assign(sand, hard, enemy);
            AssignGold(bolt, muzzle);
            AssetDatabase.SaveAssets();
            Debug.Log("[ImpactVfx] Estilo dorado: bala, disparo e impactos (arena, metal, enemigo) asignados a " + guns + " pistola(s).");
        }

        // ---------------------------------------------------------------- Arena

        private static GameObject BuildSand()
        {
            var root = new GameObject("VFX_Impacto_Arena");
            CartoonExplosion(root, .75f);

            // Punto incandescente que se enfría: blanco -> naranja -> rojo oscuro.
            var hot = Burst(root, "PuntoCaliente", glowAdd, 1, 1.1f, 1.1f, 0f, .32f, .32f, Color.white, Color.white);
            Flat(hot);
            ColorOverLife(hot, HotWhite, Orange, Ember);
            SizeOverLife(hot, Curve(0f, 1f, .15f, .8f, 1f, .5f));

            // Marca de quemadura en la arena que se desvanece.
            var scorch = Burst(root, "Quemadura", scorchAlpha, 1, 5f, 5f, 0f, .5f, .5f, new Color(.12f, .05f, .03f, .75f), new Color(.12f, .05f, .03f, .75f));
            Flat(scorch);
            RandomSpin(scorch);
            LateFade(scorch, .75f);

            // Nube de arena que sale disparada y se abre.
            var plume = Burst(root, "NubeArena", smokeAlpha, 9, .9f, 1.7f, .25f, .25f, .45f, new Color(Sand.r, Sand.g, Sand.b, .7f), new Color(.6f, .32f, .18f, .55f));
            var plumeMain = plume.main;
            plumeMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            plumeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Cone(plume, 28f, .05f);
            Drag(plume, 3f);
            SizeOverLife(plume, Curve(0f, .5f, .4f, 1.6f, 1f, 2.4f));
            SoftFade(plume);

            // Polvo bajo que rueda pegado al suelo.
            var skirt = Burst(root, "PolvoBajo", smokeAlpha, 6, 1f, 1.6f, 0f, .3f, .5f, new Color(Sand.r, Sand.g, Sand.b, .45f), new Color(.65f, .35f, .2f, .35f));
            var skirtMain = skirt.main;
            skirtMain.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 1.6f);
            skirtMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Ring(skirt, .05f);
            Drag(skirt, 2.5f);
            SizeOverLife(skirt, Curve(0f, .6f, 1f, 2f));
            SoftFade(skirt);

            // Terrones de arena que vuelan y caen.
            var clumps = Burst(root, "Terrones", sandLit, 14, .7f, 1.2f, 2.2f, .03f, .07f, Color.white, new Color(.85f, .75f, .7f));
            var clumpMain = clumps.main;
            clumpMain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            clumpMain.startRotation3D = true;
            MeshRender(clumps);
            Spin(clumps);
            Cone(clumps, 38f, .04f);
            Collide(clumps, .15f);

            AddLight(root, Gold, 6f, 4f, .12f);
            return root;
        }

        // ---------------------------------------------------------------- Metal / objetos duros

        private static GameObject BuildHard()
        {
            var root = new GameObject("VFX_Impacto_Metal");
            CartoonExplosion(root, .65f);

            var hot = Burst(root, "PuntoCaliente", glowAdd, 1, .9f, .9f, 0f, .2f, .2f, Color.white, Color.white);
            Flat(hot);
            ColorOverLife(hot, HotWhite, Orange, Ember);

            var scorch = Burst(root, "Quemadura", scorchAlpha, 1, 4f, 4f, 0f, .26f, .26f, new Color(.05f, .04f, .04f, .7f), new Color(.05f, .04f, .04f, .7f));
            Flat(scorch);
            RandomSpin(scorch);
            LateFade(scorch, .7f);

            // Gotas de metal fundido que caen brillando.
            var drops = Burst(root, "MetalFundido", glowAdd, 6, .6f, 1f, 1.8f, .04f, .07f, HotWhite, Orange);
            var dropMain = drops.main;
            dropMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            Cone(drops, 45f, .02f);
            ColorOverLife(drops, HotWhite, Orange, Ember);
            Collide(drops, .2f);

            // Voluta de humo que sube.
            var smoke = Burst(root, "Humo", smokeAlpha, 4, 1f, 1.6f, -.04f, .15f, .3f, new Color(.35f, .33f, .32f, .45f), new Color(.25f, .23f, .22f, .35f));
            var smokeMain = smoke.main;
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(.3f, .8f);
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Cone(smoke, 15f, .03f);
            Drag(smoke, 1.5f);
            SizeOverLife(smoke, Curve(0f, .5f, 1f, 2.6f));
            SoftFade(smoke);

            AddLight(root, Gold, 6f, 4f, .1f);
            return root;
        }

        // ---------------------------------------------------------------- Enemigo

        private static GameObject BuildEnemy()
        {
            var root = new GameObject("VFX_Impacto_Enemigo");
            CartoonExplosion(root, .6f);

            // Salpicadura de energía dorada que sale de la herida.
            var splash = Burst(root, "Salpicadura", sparkAdd, 18, .18f, .4f, .9f, .03f, .06f, Color.white, Gold);
            var splashMain = splash.main;
            splashMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f);
            Stretch(splash, .05f);
            Cone(splash, 32f, .03f);
            ColorOverLife(splash, Color.white, Gold, DeepOrange);

            // Chisporroteo: brasas pequeñas que tiemblan sobre la piel quemada.
            var sizzle = Burst(root, "Chisporroteo", glowAdd, 10, .4f, .8f, -.1f, .03f, .06f, new Color(1f, .9f, .5f), Orange);
            var sizzleMain = sizzle.main;
            sizzleMain.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.4f);
            Cone(sizzle, 55f, .06f);
            ColorOverLife(sizzle, new Color(1f, .9f, .5f), Orange, Ember);
            var noise = sizzle.noise;
            noise.enabled = true;
            noise.strength = .8f;
            noise.frequency = 3f;

            // Vapor de la carne quemada.
            var steam = Burst(root, "Vapor", smokeAlpha, 4, .7f, 1.2f, -.06f, .18f, .3f, new Color(.55f, .65f, .65f, .35f), new Color(.4f, .45f, .45f, .25f));
            var steamMain = steam.main;
            steamMain.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1f);
            steamMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Cone(steam, 25f, .04f);
            Drag(steam, 2f);
            SizeOverLife(steam, Curve(0f, .5f, 1f, 2.2f));
            SoftFade(steam);

            AddLight(root, Gold, 6f, 3.5f, .1f);
            return root;
        }

        // ---------------------------------------------------------------- Explosión de Cartoon FX Remaster

        private const string CartoonExplosionPath = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab";

        // Anida la "CFXR Explosion 1" (nube de fuego, rayos amarillos y halo) dentro del impacto.
        // El eje Z del impacto es la normal de la superficie; la explosión espera el eje Y hacia arriba.
        private static void CartoonExplosion(GameObject root, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CartoonExplosionPath);
            if (prefab == null)
            {
                Debug.LogWarning("[ImpactVfx] No encontré " + CartoonExplosionPath + "; uso el estallido propio.");
                GoldBurst(root, scale / .75f);
                return;
            }
            var boom = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            boom.transform.SetParent(root.transform, false);
            boom.transform.localPosition = Vector3.zero;
            boom.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            boom.transform.localScale = Vector3.one * scale;

            // Sin sacudida de cámara: con cada bala la pantalla temblaría sin parar.
            var effect = boom.GetComponent("CFXR_Effect");
            if (effect != null)
            {
                var so = new SerializedObject(effect);
                var shake = so.FindProperty("cameraShake.enabled");
                if (shake != null) shake.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---------------------------------------------------------------- Estilo dorado: bala, disparo y estallido

        private static readonly Color Gold = new Color(1f, 0.78f, 0.18f);
        private static readonly Color PaleGold = new Color(1f, 0.95f, 0.6f);
        private static readonly Color DeepOrange = new Color(1f, 0.38f, 0.04f);

        // Cometa dorado: núcleo blanco-amarillo, corona dorada, cola de fuego que se afina y chispas.
        private static GameObject BuildGoldBolt()
        {
            var root = new GameObject("VFX_BalaDorada");
            root.AddComponent<EnergyBolt>();

            var core = Burst(root, "Nucleo", glowAdd, 0, .05f, .05f, 0f, .26f, .32f, Color.white, PaleGold);
            Looping(core, 70f, 0f, true);

            var corona = Burst(root, "Corona", glowAdd, 0, .07f, .07f, 0f, .65f, .8f, new Color(Gold.r, Gold.g, Gold.b, .55f), new Color(1f, .6f, .1f, .45f));
            Looping(corona, 40f, 0f, true);

            // Llamas que quedan atrás y se apagan: dan la cola de fuego.
            var flames = Burst(root, "Llamas", glowAdd, 0, .12f, .28f, -.1f, .16f, .3f, PaleGold, Gold);
            Looping(flames, 0f, 7f, false);
            flames.GetComponent<ParticleSystemRenderer>().maxParticleSize = .5f;
            ColorOverLife(flames, PaleGold, DeepOrange, new Color(.5f, .05f, 0f));
            SizeOverLife(flames, Curve(0f, 1f, 1f, 0f));

            var sparks = Burst(root, "Chispas", sparkAdd, 0, .12f, .3f, .4f, .02f, .04f, Color.white, Gold);
            Looping(sparks, 0f, 3f, false);
            var sparkMain = sparks.main;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 2.5f);
            Cone(sparks, 25f, .03f);
            Stretch(sparks, .08f);
            ColorOverLife(sparks, Color.white, Gold, DeepOrange);

            AddTrail(root, .1f, .24f, 1f);
            var outer = new GameObject("EstelaExterior");
            outer.transform.SetParent(root.transform, false);
            AddTrail(outer, .06f, .5f, .4f);

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Gold;
            light.intensity = 3.5f;
            light.range = 3.5f;
            light.shadows = LightShadows.None;
            return root;
        }

        private static void Looping(ParticleSystem ps, float rateOverTime, float rateOverDistance, bool local)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = 300;
            if (local) main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            emission.rateOverTime = rateOverTime;
            emission.rateOverDistance = rateOverDistance;
        }

        private static void AddTrail(GameObject go, float time, float width, float alpha)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = sparkAdd;
            trail.time = time;
            trail.minVertexDistance = .1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(.25f, width * .7f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(PaleGold, .15f), new GradientColorKey(Gold, .45f), new GradientColorKey(DeepOrange, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * .7f, .5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.textureMode = LineTextureMode.Stretch;
        }

        private static GameObject BuildGoldMuzzle()
        {
            var root = new GameObject("VFX_DisparoDorado");

            var core = Burst(root, "Destello", glowAdd, 2, .06f, .08f, 0f, .5f, .75f, Color.white, PaleGold);
            SizeOverLife(core, Curve(0f, .6f, .3f, 1f, 1f, 0f));

            var spikes = Burst(root, "Puntas", sparkAdd, 9, .05f, .09f, 0f, .45f, .65f, Color.white, Gold);
            var spikeRenderer = spikes.GetComponent<ParticleSystemRenderer>();
            spikeRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            spikeRenderer.velocityScale = .04f;
            spikeRenderer.lengthScale = 2f;
            var spikeMain = spikes.main;
            spikeMain.startSpeed = new ParticleSystem.MinMaxCurve(6f, 11f);
            Cone(spikes, 28f, .01f);

            var sparks = Burst(root, "Chispas", sparkAdd, 16, .12f, .25f, .8f, .025f, .05f, Color.white, Gold);
            var sparkMain = sparks.main;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
            Stretch(sparks, .05f);
            Cone(sparks, 22f, .02f);
            ColorOverLife(sparks, Color.white, Gold, DeepOrange);

            var smoke = Burst(root, "Humo", smokeAlpha, 3, .5f, .9f, -.05f, .15f, .28f, new Color(.55f, .45f, .4f, .3f), new Color(.4f, .32f, .3f, .2f));
            var smokeMain = smoke.main;
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.1f);
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Cone(smoke, 15f, .02f);
            SizeOverLife(smoke, Curve(0f, .5f, 1f, 2.4f));
            SoftFade(smoke);

            AddLight(root, Gold, 6f, 4f, .08f);
            return root;
        }

        // Estallido de fuego estilizado (como el video de referencia):
        //  1. pilar de fuego que sale del impacto y se encoge hasta ser una llama de vela,
        //  2. lenguas de fuego onduladas + rayos rectos hacia afuera,
        //  3. anillos naranjas que se expanden por el suelo,
        //  4. chispas alargadas que vuelan en arco y, al caer, prenden un fueguito con su anillito.
        private static void GoldBurst(GameObject root, float scale)
        {
            var flash = Burst(root, "DestelloDorado", glowAdd, 1, .08f, .08f, 0f, 1f * scale, 1f * scale, Color.white, PaleGold);
            SizeOverLife(flash, Curve(0f, .5f, .25f, 1f, 1f, 0f));


            // Explosión: llamaradas que se inflan hacia afuera (bola de fuego).
            var boom = Burst(root, "Explosion", glowAdd, 18, .35f, .65f, -.05f, .45f * scale, .85f * scale, Color.white, Gold);
            var boomMain = boom.main;
            boomMain.startSpeed = new ParticleSystem.MinMaxCurve(2f * scale, 4.5f * scale);
            boomMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Cone(boom, 75f, .08f);
            Drag(boom, 5f);
            SizeOverLife(boom, Curve(0f, .4f, .25f, 1.4f, 1f, 1.9f));
            Gradient4(boom, Color.white, PaleGold, DeepOrange, new Color(.45f, .05f, 0f), .95f);
            var boomNoise = boom.noise;
            boomNoise.enabled = true;
            boomNoise.strength = 1.2f;
            boomNoise.frequency = 1.5f;

            var boomFlames = Burst(root, "LlamaradasExplosion", flameAdd, 10, .4f, .7f, -.2f, .4f * scale, .7f * scale, PaleGold, Gold);
            var bfMain = boomFlames.main;
            bfMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * scale, 3f * scale);
            Cone(boomFlames, 55f, .1f);
            Drag(boomFlames, 4f);
            SizeOverLife(boomFlames, Curve(0f, .5f, .3f, 1.2f, 1f, .6f));
            Gradient4(boomFlames, Color.white, Gold, DeepOrange, new Color(.4f, .04f, 0f), .9f);

            // 1. Pilar de fuego: alto y brillante, se achica hasta quedar como una vela.
            var pillar = Burst(root, "PilarFuego", flameAdd, 1, .75f, .75f, 0f, 1f, 1f, Color.white, Color.white);
            var pillarMain = pillar.main;
            pillarMain.startSize3D = true;
            pillarMain.startSizeX = .55f * scale;
            pillarMain.startSizeY = 1.5f * scale;
            pillarMain.startSizeZ = 1f;
            var pillarSize = pillar.sizeOverLifetime;
            pillarSize.enabled = true;
            pillarSize.separateAxes = true;
            pillarSize.x = new ParticleSystem.MinMaxCurve(1f, Curve(0f, .7f, .12f, 1f, .45f, .45f, 1f, .15f));
            pillarSize.y = new ParticleSystem.MinMaxCurve(1f, Curve(0f, .2f, .1f, 1.1f, .4f, .75f, 1f, .25f));
            pillarSize.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
            ColorOverLife(pillar, Color.white, PaleGold, DeepOrange);
            pillar.GetComponent<ParticleSystemRenderer>().pivot = new Vector3(0f, .45f, 0f); // la base queda en el impacto

            // Halo naranja detrás del pilar.
            var pillarGlow = Burst(root, "HaloPilar", glowAdd, 1, .45f, .45f, 0f, 1.6f * scale, 1.6f * scale, new Color(1f, .5f, .1f, .55f), new Color(1f, .5f, .1f, .55f));
            SizeOverLife(pillarGlow, Curve(0f, .6f, .2f, 1f, 1f, .3f));
            FadeOut(pillarGlow);

            // 2a. Lenguas de fuego onduladas que salen hacia los costados.
            var licks = Burst(root, "LenguasFuego", sparkAdd, 16, .22f, .36f, 0f, .22f * scale, .34f * scale, Color.white, PaleGold);
            var lickMain = licks.main;
            lickMain.startSpeed = new ParticleSystem.MinMaxCurve(4f * scale, 6.5f * scale);
            var lickRenderer = licks.GetComponent<ParticleSystemRenderer>();
            lickRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            lickRenderer.velocityScale = .09f;
            lickRenderer.lengthScale = 1.6f;
            Cone(licks, 72f, .05f);
            Drag(licks, 7f);
            var lickNoise = licks.noise;
            lickNoise.enabled = true;
            lickNoise.strength = 3f;
            lickNoise.frequency = 2.5f;
            lickNoise.scrollSpeed = 2f;
            ColorOverLife(licks, PaleGold, Gold, DeepOrange);

            // 2b. Rayos rectos y largos.
            var rays = Burst(root, "Rayos", sparkAdd, 26, .14f, .26f, 0f, .05f * scale, .09f * scale, Color.white, PaleGold);
            var rayMain = rays.main;
            rayMain.startSpeed = new ParticleSystem.MinMaxCurve(9f * scale, 16f * scale);
            var rayRenderer = rays.GetComponent<ParticleSystemRenderer>();
            rayRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rayRenderer.velocityScale = .05f;
            rayRenderer.lengthScale = 3f;
            Cone(rays, 50f, .03f);
            Drag(rays, 6f);
            ColorOverLife(rays, Color.white, Gold, DeepOrange);
            SizeOverLife(rays, Curve(0f, 1f, 1f, .2f));

            // 3. Anillos naranjas por el suelo: uno rápido y grande, otro más lento.
            var ring = Burst(root, "AnilloGrande", ringAdd, 1, .5f, .5f, 0f, .35f * scale, .35f * scale, DeepOrange, DeepOrange);
            Flat(ring);
            SizeOverLife(ring, Curve(0f, .3f, .35f, 4.5f, 1f, 6f));
            FadeOut(ring);
            var ring2 = Burst(root, "AnilloInterno", ringAdd, 1, .7f, .7f, 0f, .3f * scale, .3f * scale, Gold, Gold);
            Flat(ring2);
            var ring2Main = ring2.main;
            ring2Main.startDelay = .06f;
            SizeOverLife(ring2, Curve(0f, .3f, .4f, 2.4f, 1f, 3.2f));
            FadeOut(ring2);

            var baseGlow = Burst(root, "ResplandorBase", glowAdd, 1, .45f, .45f, 0f, .8f * scale, .8f * scale, new Color(1f, .55f, .12f, .7f), new Color(1f, .55f, .12f, .7f));
            Flat(baseGlow);
            SizeOverLife(baseGlow, Curve(0f, .5f, .3f, 1.3f, 1f, 1.5f));
            FadeOut(baseGlow);

            // 4. Chispas que vuelan lejos en arco; al tocar el suelo mueren y prenden un fueguito + anillito.
            var sparks = Burst(root, "ChispasLejanas", sparkAdd, 40, 1.1f, 1.8f, 1.6f, .04f * scale, .07f * scale, Color.white, Gold);
            var sparkMain = sparks.main;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(4.5f * scale, 9f * scale);
            var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sparkRenderer.velocityScale = .06f;
            sparkRenderer.lengthScale = 2.5f;
            Cone(sparks, 62f, .05f);
            ColorOverLife(sparks, PaleGold, Gold, DeepOrange);
            Collide(sparks, 0f);
            var sparkCollision = sparks.collision;
            sparkCollision.lifetimeLoss = 1f; // mueren al tocar el suelo

            var smallFire = Burst(sparks.gameObject, "Fueguito", flameAdd, 1, .7f, 1.1f, 0f, 1f, 1f, PaleGold, Gold);
            var fireMain = smallFire.main;
            fireMain.playOnAwake = false;
            fireMain.startSize3D = true;
            fireMain.startSizeX = new ParticleSystem.MinMaxCurve(.12f * scale, .18f * scale);
            fireMain.startSizeY = new ParticleSystem.MinMaxCurve(.22f * scale, .32f * scale);
            fireMain.startSizeZ = 1f;
            fireMain.maxParticles = 100;
            smallFire.GetComponent<ParticleSystemRenderer>().pivot = new Vector3(0f, .4f, 0f);
            var fireSize = smallFire.sizeOverLifetime;
            fireSize.enabled = true;
            fireSize.separateAxes = true;
            fireSize.x = new ParticleSystem.MinMaxCurve(1f, Curve(0f, .6f, .15f, 1f, 1f, .2f));
            fireSize.y = new ParticleSystem.MinMaxCurve(1f, Curve(0f, .3f, .15f, 1f, 1f, .1f));
            fireSize.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
            ColorOverLife(smallFire, PaleGold, Gold, DeepOrange);

            var smallRing = Burst(sparks.gameObject, "Anillito", ringAdd, 1, 1.4f, 1.8f, 0f, .12f * scale, .12f * scale, DeepOrange, Gold);
            var smallRingMain = smallRing.main;
            smallRingMain.playOnAwake = false;
            smallRingMain.maxParticles = 100;
            // Siempre acostado en el suelo, aunque la chispa venga de una pared.
            smallRing.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            SizeOverLife(smallRing, Curve(0f, .3f, .2f, 1.4f, 1f, 2f));
            LateFade(smallRing, .5f);

            var sub = sparks.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(smallFire, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
            sub.AddSubEmitter(smallRing, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);


            // Fuego residual: sigue saliendo un rato y sube ondulando como humo de fuego.
            var fireSmoke = Burst(root, "FuegoResidual", flameAdd, 0, 1.4f, 2.4f, -.12f, .3f * scale, .55f * scale, PaleGold, Gold);
            Emitting(fireSmoke, 1.6f, 16f, 220);
            var fsMain = fireSmoke.main;
            fsMain.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.1f);
            fsMain.startRotation = new ParticleSystem.MinMaxCurve(-.3f, .3f);
            Cone(fireSmoke, 18f, .12f * scale);
            SizeOverLife(fireSmoke, Curve(0f, .4f, .2f, 1f, 1f, 1.8f));
            Gradient4(fireSmoke, PaleGold, Gold, DeepOrange, new Color(.35f, .04f, 0f), .75f);
            var fsNoise = fireSmoke.noise;
            fsNoise.enabled = true;
            fsNoise.strength = .9f;
            fsNoise.frequency = 1.2f;
            fsNoise.scrollSpeed = .8f;

            // Brasas de fuego que el humo arrastra hacia arriba.
            var risingEmbers = Burst(root, "ChispasHumo", glowAdd, 0, 1.2f, 2.2f, -.25f, .03f * scale, .06f * scale, PaleGold, Gold);
            Emitting(risingEmbers, 1.8f, 14f, 160);
            var reMain = risingEmbers.main;
            reMain.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.6f);
            Cone(risingEmbers, 25f, .15f * scale);
            ColorOverLife(risingEmbers, PaleGold, Gold, new Color(.6f, .1f, 0f));
            var reNoise = risingEmbers.noise;
            reNoise.enabled = true;
            reNoise.strength = .7f;
            reNoise.frequency = 2f;

            // Humo oscuro detrás del fuego para que resalte.
            var darkSmoke = Burst(root, "HumoOscuro", smokeAlpha, 0, 2f, 3.2f, -.06f, .35f * scale, .6f * scale, new Color(.18f, .12f, .1f, .55f), new Color(.1f, .08f, .07f, .45f));
            Emitting(darkSmoke, 1.8f, 7f, 80);
            var dsMain = darkSmoke.main;
            dsMain.startDelay = .15f;
            dsMain.startSpeed = new ParticleSystem.MinMaxCurve(.4f, .9f);
            dsMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Cone(darkSmoke, 20f, .15f * scale);
            Drag(darkSmoke, 1f);
            SizeOverLife(darkSmoke, Curve(0f, .4f, 1f, 3f));
            SoftFade(darkSmoke);
            var dsNoise = darkSmoke.noise;
            dsNoise.enabled = true;
            dsNoise.strength = .5f;
            dsNoise.frequency = .8f;

            // Brasas que flotan hacia arriba al final.
            var embers = Burst(root, "Brasas", glowAdd, 26, 1.2f, 2.6f, -.18f, .03f * scale, .06f * scale, PaleGold, Gold);
            var emberMain = embers.main;
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 1.8f);
            Cone(embers, 45f, .08f);
            ColorOverLife(embers, PaleGold, Gold, new Color(.6f, .1f, 0f));
            var noise = embers.noise;
            noise.enabled = true;
            noise.strength = .5f;
            noise.frequency = 2f;
        }


        // Emite de forma continua durante un tiempo (en vez de una sola ráfaga).
        private static void Emitting(ParticleSystem ps, float duration, float rate, int maxParticles)
        {
            var main = ps.main;
            main.duration = duration;
            main.maxParticles = maxParticles;
            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate, AnimationCurve.Linear(0f, 1f, 1f, .2f));
        }

        // Color de fuego en 4 pasos con fundido de entrada suave y salida lenta.
        private static void Gradient4(ParticleSystem ps, Color a, Color b, Color c, Color d, float alpha)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, .2f), new GradientColorKey(c, .55f), new GradientColorKey(d, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, .06f), new GradientAlphaKey(alpha * .7f, .55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static Material flameAdd;

        // Llama en forma de gota: ancha y redonda abajo, se afina arriba, núcleo más brillante.
        private static Material FlameMaterial()
        {
            const int w = 64, h = 128;
            string texPath = Root + "/T_Llama.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w * 2f - 1f;   // -1..1
                    float v = (y + .5f) / h;             // 0 abajo .. 1 arriba
                    float half = Mathf.Lerp(.85f, 0f, Mathf.Pow(v, .8f)) * Mathf.Clamp01(v * 6f + .35f);
                    float d = Mathf.Abs(u) / Mathf.Max(.001f, half);
                    float body = Mathf.Clamp01(1f - d);
                    body = Mathf.Pow(body, .6f) * Mathf.Clamp01((1f - v) * 1.6f + .1f);
                    float core = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(.001f, half * .45f)), 1.5f) * (1f - v);
                    float a = Mathf.Clamp01(body * .8f + core);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();

            string matPath = Root + "/M_VFX_Llama.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!m) { m = new Material(glowAdd); AssetDatabase.CreateAsset(m, matPath); }
            m.CopyPropertiesFromMaterial(glowAdd);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            EditorUtility.SetDirty(m);
            return m;
        }

        private static int AssignGold(GameObject bolt, GameObject muzzle)
        {
            int count = 0;
            var tracerColor = new Gradient();
            tracerColor.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Gold, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.6f, 1f) });
            foreach (string path in GunHolders)
            {
                if (!File.Exists(path)) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (Gun gun in contents.GetComponentsInChildren<Gun>(true))
                {
                    var so = new SerializedObject(gun);
                    so.FindProperty("boltEffect").objectReferenceValue = bolt.GetComponent<EnergyBolt>();
                    so.FindProperty("muzzleFlashEffect").objectReferenceValue = muzzle;
                    var tracer = so.FindProperty("tracer").objectReferenceValue as LineRenderer;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    if (tracer != null) tracer.colorGradient = tracerColor;
                    changed = true;
                    count++;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return count;
        }

        // ---------------------------------------------------------------- Materiales

        private static bool LoadMaterials()
        {
            glowAdd = AssetDatabase.LoadAssetAtPath<Material>(Root + "/M_VFX_Glow.mat");
            sparkAdd = AssetDatabase.LoadAssetAtPath<Material>(Root + "/M_VFX_Chispa.mat");
            ringAdd = AssetDatabase.LoadAssetAtPath<Material>(Root + "/M_VFX_Anillo.mat");
            smokeAlpha = AssetDatabase.LoadAssetAtPath<Material>(Root + "/M_VFX_Humo.mat");
            if (!glowAdd || !sparkAdd || !ringAdd || !smokeAlpha) return false;

            string sandPath = Root + "/M_VFX_Terron.mat";
            sandLit = AssetDatabase.LoadAssetAtPath<Material>(sandPath);
            if (!sandLit)
            {
                sandLit = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
                AssetDatabase.CreateAsset(sandLit, sandPath);
            }
            sandLit.SetColor("_BaseColor", Sand);
            sandLit.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(sandLit);

            // Quemadura: mancha oscura irregular (mismo shader de partículas que el humo, otra textura).
            string scorchMatPath = Root + "/M_VFX_Quemadura.mat";
            scorchAlpha = AssetDatabase.LoadAssetAtPath<Material>(scorchMatPath);
            if (!scorchAlpha)
            {
                scorchAlpha = new Material(smokeAlpha);
                AssetDatabase.CreateAsset(scorchAlpha, scorchMatPath);
            }
            scorchAlpha.CopyPropertiesFromMaterial(smokeAlpha);
            scorchAlpha.SetTexture("_BaseMap", ScorchTexture());
            EditorUtility.SetDirty(scorchAlpha);
            flameAdd = FlameMaterial();
            return true;
        }

        private static Texture2D ScorchTexture()
        {
            const int size = 128;
            string path = Root + "/T_Quemadura.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size, v = (y + .5f) / size;
                    float dx = u - .5f, dy = v - .5f;
                    float ang = Mathf.Atan2(dy, dx);
                    // Borde irregular con rayos cortos, centro más oscuro.
                    float edge = .62f + .1f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2f + 3f, Mathf.Sin(ang) * 2f + 3f)
                                 + .06f * Mathf.Sin(ang * 9f + 1.3f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f / edge;
                    float a = Mathf.Clamp01(1f - r);
                    a = Mathf.Pow(a, .7f) * Mathf.Lerp(.65f, 1f, Mathf.PerlinNoise(u * 9f, v * 9f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- Ayudantes

        private static ParticleSystem Burst(GameObject parent, string name, Material material, int count,
            float lifeMin, float lifeMax, float gravity, float sizeMin, float sizeMax, Color colorA, Color colorB)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .2f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(count * 2, 4);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static void Cone(ParticleSystem ps, float angle, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
        }

        private static void Ring(ParticleSystem ps, float radius)
        {
            // Sale de lado, rozando la superficie (el eje Z del efecto es la normal).
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0f;
        }

        private static void Flat(ParticleSystem ps)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.Local;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }

        private static void MeshRender(ParticleSystem ps)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }

        private static void Spin(ParticleSystem ps)
        {
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-10f, 10f);
            spin.y = new ParticleSystem.MinMaxCurve(-10f, 10f);
            spin.z = new ParticleSystem.MinMaxCurve(-10f, 10f);
        }

        private static void RandomSpin(ParticleSystem ps)
        {
            var main = ps.main;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        }

        private static void Stretch(ParticleSystem ps, float velocityScale)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = velocityScale;
            r.lengthScale = 1f;
        }

        private static void Drag(ParticleSystem ps, float drag)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
        }

        private static void Collide(ParticleSystem ps, float bounce)
        {
            var c = ps.collision;
            c.enabled = true;
            c.type = ParticleSystemCollisionType.World;
            c.mode = ParticleSystemCollisionMode.Collision3D;
            c.quality = ParticleSystemCollisionQuality.Low;
            c.bounce = bounce;
            c.dampen = .35f;
            c.lifetimeLoss = .05f;
        }

        private static void SizeOverLife(ParticleSystem ps, AnimationCurve curve)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void FadeOut(ParticleSystem ps)
        {
            SetAlpha(ps, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.7f, .4f), new GradientAlphaKey(0f, 1f) });
        }

        private static void SoftFade(ParticleSystem ps)
        {
            SetAlpha(ps, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f), new GradientAlphaKey(.6f, .5f), new GradientAlphaKey(0f, 1f) });
        }

        private static void LateFade(ParticleSystem ps, float holdUntil)
        {
            SetAlpha(ps, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .02f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
        }

        private static void SetAlpha(ParticleSystem ps, GradientAlphaKey[] alpha)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
            col.color = g;
        }

        private static void ColorOverLife(ParticleSystem ps, Color start, Color middle, Color end)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, .3f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static AnimationCurve Curve(params float[] tv)
        {
            var c = new AnimationCurve();
            for (int i = 0; i + 1 < tv.Length; i += 2) c.AddKey(tv[i], tv[i + 1]);
            return c;
        }

        private static void AddLight(GameObject root, Color color, float intensity, float range, float duration)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, .15f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            var fade = go.AddComponent<VfxLightFade>();
            var so = new SerializedObject(fade);
            so.FindProperty("duration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Save(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static int Assign(GameObject sand, GameObject hard, GameObject enemy)
        {
            int count = 0;
            foreach (string path in GunHolders)
            {
                if (!File.Exists(path)) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (Gun gun in contents.GetComponentsInChildren<Gun>(true))
                {
                    var so = new SerializedObject(gun);
                    so.FindProperty("impactEffect").objectReferenceValue = hard;
                    so.FindProperty("impactEffectSand").objectReferenceValue = sand;
                    so.FindProperty("impactEffectEnemy").objectReferenceValue = enemy;
                    so.FindProperty("impactEffectDuration").floatValue = 6.5f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                    count++;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return count;
        }
    }
}
