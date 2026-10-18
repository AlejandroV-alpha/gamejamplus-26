using System.IO;
using GameJam.Combat;
using UnityEditor;
using UnityEngine;

namespace GameJam.EditorTools
{
    /// <summary>
    /// Genera los efectos de disparo e impacto (replica de VFX_Disparo_Impacto.blend) y los asigna a las pistolas.
    /// </summary>
    public static class GunVfxBuilder
    {
        private const string Root = "Assets/02_Prefabs/Gun/VFX";
        private const string MuzzlePath = Root + "/VFX_DisparoEnergia.prefab";
        private const string ImpactPath = Root + "/VFX_ImpactoEnergia.prefab";

        private static readonly string[] GunHolders =
        {
            "Assets/02_Prefabs/Gun/Gun.prefab",
            "Assets/02_Prefabs/Fusion/A_FusionAstronaut.prefab",
            "Assets/02_Prefabs/Fusion/A_CombatAstronaut.prefab",
        };

        private static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        private static readonly Color Orange = new Color(1f, 0.45f, 0.1f);

        private static Material glowAdd;
        private static Material sparkAdd;
        private static Material ringAdd;
        private static Material smokeAlpha;
        private static Material debrisLit;

        [MenuItem("Tools/GameJam/Crear Efectos de Disparo (VFX)")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);
            CreateMaterials();
            GameObject muzzle = SavePrefab(BuildMuzzle(), MuzzlePath);
            GameObject impact = SavePrefab(BuildImpact(), ImpactPath);
            GameObject bolt = SavePrefab(BuildBolt(), BoltPath);
            GameObject bloodBurst = SavePrefab(BuildBloodBurst(), BloodBurstPath);
            GameObject splat = SavePrefab(BuildSplat(), SplatPath);
            int guns = AssignToGuns(muzzle, impact, bolt.GetComponent<EnergyBolt>());
            bool reksai = AssignBlood(bloodBurst, splat);
            GameObject playerHurt = SavePrefab(BuildPlayerHurt(), PlayerHurtPath);
            AssignPlayerHurt(playerHurt, bloodBurst, splat);
            AssetDatabase.SaveAssets();
            Debug.Log("[GunVfx] Efectos creados en " + Root + ", asignados a " + guns + " pistola(s). Sangre en Reksai: " + reksai);
        }

        private const string BoltPath = Root + "/VFX_BalaEnergia.prefab";
        private const string BloodBurstPath = Root + "/VFX_SangreSalpicadura.prefab";
        private const string SplatPath = Root + "/VFX_ManchaSangre.prefab";
        private const string ReksaiPath = "Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Marte.prefab";

        private static readonly Color Blood = new Color(0.45f, 0.02f, 0.02f);
        private static Material bloodAlpha;
        private static Material splatMaterial;

        // ---------- Bala de energia (como en Blender: nucleo brillante + estela) ----------

        private static GameObject BuildBolt()
        {
            GameObject root = new GameObject("VFX_BalaEnergia");
            root.AddComponent<EnergyBolt>();

            ParticleSystem head = Burst(root, "Nucleo", glowAdd, 0, 0.05f, 0.05f, 0f, 0.35f, 0.45f, Color.white, Cyan);
            var headMain = head.main;
            headMain.loop = true;
            headMain.duration = 1f;
            headMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            var headEmission = head.emission;
            headEmission.SetBursts(new ParticleSystem.Burst[0]);
            headEmission.rateOverTime = 60f;

            ParticleSystem halo = Burst(root, "Halo", glowAdd, 0, 0.08f, 0.08f, 0f, 0.9f, 1.1f, new Color(0.2f, 0.7f, 1f, 0.5f), new Color(0.2f, 0.7f, 1f, 0.35f));
            var haloMain = halo.main;
            haloMain.loop = true;
            haloMain.duration = 1f;
            haloMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            var haloEmission = halo.emission;
            haloEmission.SetBursts(new ParticleSystem.Burst[0]);
            haloEmission.rateOverTime = 30f;

            ParticleSystem sparks = Burst(root, "ChispasEstela", sparkAdd, 0, 0.12f, 0.25f, 0.3f, 0.02f, 0.05f, Color.white, Cyan);
            var sparkMain = sparks.main;
            sparkMain.loop = true;
            sparkMain.duration = 1f;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            sparkMain.maxParticles = 300;
            var sparkEmission = sparks.emission;
            sparkEmission.SetBursts(new ParticleSystem.Burst[0]);
            sparkEmission.rateOverDistance = 4f;
            Stretch(sparks, 0.1f);
            ColorOverLife(sparks, Color.white, Cyan, new Color(0.2f, 0.4f, 1f));

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = sparkAdd;
            trail.time = 0.12f;
            trail.minVertexDistance = 0.1f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.12f, 1f, 0f);
            Gradient trailColor = new Gradient();
            trailColor.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Cyan, 0.4f), new GradientColorKey(new Color(0.1f, 0.4f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailColor;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.textureMode = LineTextureMode.Stretch;

            GameObject lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(root.transform, false);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Cyan;
            light.intensity = 3f;
            light.range = 3f;
            light.shadows = LightShadows.None;
            return root;
        }

        // ---------- Sangre ----------

        private static GameObject BuildBloodBurst()
        {
            Texture2D drop = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/T_GlowSuave.png");
            bloodAlpha = ParticleMaterial("M_VFX_Sangre", drop, false);

            GameObject root = new GameObject("VFX_SangreSalpicadura");

            ParticleSystem spray = Burst(root, "Chorro", bloodAlpha, 35, 0.4f, 0.9f, 2.2f, 0.05f, 0.14f, Blood, new Color(0.7f, 0.05f, 0.03f));
            var sprayMain = spray.main;
            sprayMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            Stretch(spray, 0.05f);
            Cone(spray, 40f, 0.05f);
            Collide(spray, 0f);
            var sprayCollision = spray.collision;
            sprayCollision.lifetimeLoss = 1f;

            ParticleSystem mist = Burst(root, "Niebla", bloodAlpha, 8, 0.3f, 0.6f, 0.2f, 0.25f, 0.5f, new Color(0.5f, 0.03f, 0.03f, 0.6f), new Color(0.35f, 0.02f, 0.02f, 0.4f));
            var mistMain = mist.main;
            mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            Cone(mist, 30f, 0.05f);
            SizeOverLife(mist, Curve(0f, 0.6f, 1f, 1.8f));
            FadeOut(mist);

            ParticleSystem chunks = Burst(root, "Gotas", bloodAlpha, 12, 0.6f, 1.1f, 2.5f, 0.08f, 0.16f, Blood, Blood);
            var chunkMain = chunks.main;
            chunkMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            Cone(chunks, 60f, 0.05f);
            Collide(chunks, 0f);
            var chunkCollision = chunks.collision;
            chunkCollision.lifetimeLoss = 1f;
            return root;
        }

        private static GameObject BuildSplat()
        {
            Texture2D tex = SaveTexture("T_ManchaSangre", (x, y) =>
            {
                float dx = x - 0.5f;
                float dy = y - 0.5f;
                float angle = Mathf.Atan2(dy, dx);
                float edge = 0.62f + 0.18f * Mathf.PerlinNoise(Mathf.Cos(angle) * 2f + 3f, Mathf.Sin(angle) * 2f + 3f)
                             + 0.08f * Mathf.Sin(angle * 7f);
                float r = Radius(x, y);
                float body = Mathf.Clamp01((edge - r) / 0.06f);
                float droplets = 0f;
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.39f;
                    float d = 0.78f + 0.12f * Mathf.PerlinNoise(i * 3.1f, 0.5f);
                    Vector2 c = new Vector2(0.5f + Mathf.Cos(a) * d * 0.5f, 0.5f + Mathf.Sin(a) * d * 0.5f);
                    float s = 0.025f + 0.025f * Mathf.PerlinNoise(i, 2f);
                    droplets = Mathf.Max(droplets, Mathf.Clamp01((s - Vector2.Distance(new Vector2(x, y), c)) / 0.01f));
                }
                float shade = Mathf.Lerp(0.75f, 1f, Mathf.PerlinNoise(x * 9f, y * 9f));
                return Mathf.Max(body, droplets) * shade;
            });

            string matPath = Root + "/M_VFX_ManchaSangre.mat";
            splatMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (splatMaterial == null)
            {
                splatMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(splatMaterial, matPath);
            }
            splatMaterial.SetTexture("_BaseMap", tex);
            splatMaterial.SetColor("_BaseColor", new Color(0.32f, 0.01f, 0.01f, 0.92f));
            splatMaterial.SetFloat("_Surface", 1f);
            splatMaterial.SetFloat("_Blend", 0f);
            splatMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            splatMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            splatMaterial.SetFloat("_ZWrite", 0f);
            splatMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            splatMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
            splatMaterial.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(splatMaterial);

            GameObject root = new GameObject("VFX_ManchaSangre");
            root.AddComponent<BloodSplat>();
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Mancha";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = splatMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        private const string PlayerHurtPath = Root + "/VFX_DanoJugador.prefab";
        private const string PlayerPath = "Assets/02_Prefabs/Fusion/A_FusionAstronaut.prefab";

        // ---------- Dano del jugador: chispas del traje, trozos y pulso rojo ----------

        private static GameObject BuildPlayerHurt()
        {
            GameObject root = new GameObject("VFX_DanoJugador");

            ParticleSystem flash = Burst(root, "Destello", glowAdd, 1, 0.1f, 0.1f, 0f, 0.8f, 0.8f, new Color(1f, 0.35f, 0.25f), new Color(1f, 0.35f, 0.25f));
            SizeOverLife(flash, Curve(0f, 0.5f, 0.3f, 1f, 1f, 0f));

            ParticleSystem pulse = Burst(root, "PulsoRojo", ringAdd, 1, 0.3f, 0.3f, 0f, 0.4f, 0.4f, new Color(1f, 0.15f, 0.1f), new Color(1f, 0.15f, 0.1f));
            Flat(pulse);
            SizeOverLife(pulse, Curve(0f, 0.3f, 1f, 4f));
            FadeOut(pulse);

            ParticleSystem sparks = Burst(root, "ChispasTraje", sparkAdd, 22, 0.2f, 0.45f, 1.2f, 0.02f, 0.05f, Color.white, Color.white);
            var sparkMain = sparks.main;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            Stretch(sparks, 0.06f);
            Hemisphere(sparks, 0.1f);
            ColorOverLife(sparks, Color.white, new Color(1f, 0.7f, 0.3f), new Color(1f, 0.2f, 0.05f));

            ParticleSystem bits = Burst(root, "TrozosTraje", debrisLit, 6, 0.6f, 1f, 2f, 0.03f, 0.06f, Color.white, Color.white);
            var bitsMain = bits.main;
            bitsMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            bitsMain.startRotation3D = true;
            ParticleSystemRenderer bitsRenderer = bits.GetComponent<ParticleSystemRenderer>();
            bitsRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            bitsRenderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Hemisphere(bits, 0.1f);
            Collide(bits, 0.3f);

            ParticleSystem vapor = Burst(root, "Vapor", smokeAlpha, 5, 0.5f, 0.9f, -0.1f, 0.2f, 0.4f, new Color(0.9f, 0.9f, 0.95f, 0.4f), new Color(0.8f, 0.85f, 0.9f, 0.25f));
            var vaporMain = vapor.main;
            vaporMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            Hemisphere(vapor, 0.1f);
            SizeOverLife(vapor, Curve(0f, 0.5f, 1f, 2f));
            FadeOut(vapor);
            return root;
        }

        private static void AssignPlayerHurt(GameObject hurt, GameObject burst, GameObject splat)
        {
            if (!File.Exists(PlayerPath))
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPath);
            EnemyBloodFx blood = contents.GetComponent<EnemyBloodFx>();
            if (blood == null)
            {
                blood = contents.AddComponent<EnemyBloodFx>();
            }
            SerializedObject bloodSo = new SerializedObject(blood);
            bloodSo.FindProperty("bloodBurst").objectReferenceValue = burst;
            bloodSo.FindProperty("groundSplat").objectReferenceValue = splat;
            bloodSo.FindProperty("splatsPerHit").intValue = 2;
            bloodSo.FindProperty("splatSpread").floatValue = 0.8f;
            bloodSo.FindProperty("splatSize").vector2Value = new Vector2(0.35f, 0.8f);
            bloodSo.ApplyModifiedPropertiesWithoutUndo();

            PlayerHurtReaction reaction = contents.GetComponent<PlayerHurtReaction>();
            if (reaction == null)
            {
                reaction = contents.AddComponent<PlayerHurtReaction>();
            }
            SerializedObject so = new SerializedObject(reaction);
            so.FindProperty("hurtEffect").objectReferenceValue = hurt;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static bool AssignBlood(GameObject burst, GameObject splat)
        {
            if (!File.Exists(ReksaiPath))
            {
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(ReksaiPath);
            EnemyBloodFx blood = contents.GetComponent<EnemyBloodFx>();
            if (blood == null)
            {
                blood = contents.AddComponent<EnemyBloodFx>();
            }
            SerializedObject so = new SerializedObject(blood);
            so.FindProperty("bloodBurst").objectReferenceValue = burst;
            so.FindProperty("groundSplat").objectReferenceValue = splat;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, ReksaiPath);
            PrefabUtility.UnloadPrefabContents(contents);
            return true;
        }

        // ---------- Materiales y texturas ----------

        private static void CreateMaterials()
        {
            Texture2D soft = SaveTexture("T_GlowSuave", (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Radius(x, y)), 2.2f));
            Texture2D spark = SaveTexture("T_Chispa", (x, y) =>
            {
                float across = Mathf.Abs(y - 0.5f) * 2f;
                float along = Mathf.Abs(x - 0.5f) * 2f;
                return Mathf.Pow(Mathf.Clamp01(1f - across), 3f) * Mathf.Clamp01(1f - along * along);
            });
            Texture2D ring = SaveTexture("T_Anillo", (x, y) =>
            {
                float r = Radius(x, y);
                return Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f));
            });
            Texture2D smoke = SaveTexture("T_Humo", (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 6f, y * 6f) * 0.6f + Mathf.PerlinNoise(x * 13f + 7f, y * 13f) * 0.4f;
                return Mathf.Clamp01(1f - Radius(x, y)) * Mathf.Lerp(0.4f, 1f, n);
            });

            glowAdd = ParticleMaterial("M_VFX_Glow", soft, true);
            sparkAdd = ParticleMaterial("M_VFX_Chispa", spark, true);
            ringAdd = ParticleMaterial("M_VFX_Anillo", ring, true);
            smokeAlpha = ParticleMaterial("M_VFX_Humo", smoke, false);

            string debrisPath = Root + "/M_VFX_Escombro.mat";
            debrisLit = AssetDatabase.LoadAssetAtPath<Material>(debrisPath);
            if (debrisLit == null)
            {
                debrisLit = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
                AssetDatabase.CreateAsset(debrisLit, debrisPath);
            }
            debrisLit.SetColor("_BaseColor", new Color(0.35f, 0.1f, 0.05f));
            debrisLit.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.05f) * 0.6f);
            debrisLit.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(debrisLit);
        }

        private static float Radius(float x, float y)
        {
            return Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) * 2f;
        }

        private static Texture2D SaveTexture(string name, System.Func<float, float, float> alpha)
        {
            const int size = 128;
            string path = Root + "/" + name + ".png";
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material ParticleMaterial(string name, Texture2D texture, bool additive)
        {
            string path = Root + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Prefabs ----------

        private static GameObject BuildMuzzle()
        {
            GameObject root = new GameObject("VFX_DisparoEnergia");

            ParticleSystem core = Burst(root, "Destello", glowAdd, 2, 0.07f, 0.08f, 0f, 0.55f, 0.8f, Color.white, Cyan);
            SizeOverLife(core, Curve(0f, 0.6f, 0.3f, 1f, 1f, 0f));

            ParticleSystem spikes = Burst(root, "Puntas", sparkAdd, 8, 0.06f, 0.09f, 0f, 0.5f, 0.7f, Color.white, Cyan);
            ParticleSystemRenderer spikeRenderer = spikes.GetComponent<ParticleSystemRenderer>();
            spikeRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            spikeRenderer.velocityScale = 0.04f;
            spikeRenderer.lengthScale = 2f;
            var spikeMain = spikes.main;
            spikeMain.startSpeed = new ParticleSystem.MinMaxCurve(6f, 10f);
            Cone(spikes, 30f, 0.01f);

            ParticleSystem sparks = Burst(root, "Chispas", sparkAdd, 18, 0.12f, 0.25f, 0.8f, 0.03f, 0.06f, Color.white, Cyan);
            var sparkMain = sparks.main;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
            Stretch(sparks, 0.05f);
            Cone(sparks, 22f, 0.02f);
            ColorOverLife(sparks, Color.white, Cyan, new Color(0.2f, 0.5f, 1f));

            ParticleSystem smoke = Burst(root, "Humo", smokeAlpha, 4, 0.6f, 1f, -0.05f, 0.15f, 0.3f, new Color(0.55f, 0.45f, 0.4f, 0.35f), new Color(0.4f, 0.32f, 0.3f, 0.25f));
            var smokeMain = smoke.main;
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Cone(smoke, 15f, 0.02f);
            SizeOverLife(smoke, Curve(0f, 0.5f, 1f, 2.5f));
            FadeOut(smoke);

            AddLight(root, Cyan, 6f, 4f, 0.08f);
            return root;
        }

        private static GameObject BuildImpact()
        {
            GameObject root = new GameObject("VFX_ImpactoEnergia");

            ParticleSystem flash = Burst(root, "Destello", glowAdd, 1, 0.12f, 0.12f, 0f, 1.4f, 1.4f, Color.white, new Color(0.7f, 0.95f, 1f));
            SizeOverLife(flash, Curve(0f, 0.4f, 0.25f, 1f, 1f, 0f));

            ParticleSystem shock = Burst(root, "OndaChoque", ringAdd, 1, 0.35f, 0.35f, 0f, 0.3f, 0.3f, Cyan, Cyan);
            Flat(shock);
            SizeOverLife(shock, Curve(0f, 0.2f, 1f, 6f));
            FadeOut(shock);

            ParticleSystem damageRing = Burst(root, "OndaDano", ringAdd, 1, 0.45f, 0.45f, 0f, 0.2f, 0.2f, Orange, Orange);
            Flat(damageRing);
            var ringMain = damageRing.main;
            ringMain.startDelay = 0.04f;
            SizeOverLife(damageRing, Curve(0f, 0.2f, 1f, 3.5f));
            FadeOut(damageRing);

            ParticleSystem sparks = Burst(root, "Chispas", sparkAdd, 45, 0.3f, 0.7f, 1.4f, 0.03f, 0.07f, Color.white, Color.white);
            var sparkMain = sparks.main;
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(4f, 14f);
            Stretch(sparks, 0.06f);
            Hemisphere(sparks, 0.05f);
            ColorOverLife(sparks, Color.white, Cyan, Orange);
            Collide(sparks, 0.4f);

            ParticleSystem embers = Burst(root, "BrasasDano", glowAdd, 25, 0.6f, 1.6f, 0.5f, 0.05f, 0.14f, new Color(1f, 0.9f, 0.5f), Orange);
            var emberMain = embers.main;
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            Hemisphere(embers, 0.1f);
            ColorOverLife(embers, new Color(1f, 0.9f, 0.5f), Orange, new Color(0.5f, 0.05f, 0f));
            FadeOut(embers);
            var emberNoise = embers.noise;
            emberNoise.enabled = true;
            emberNoise.strength = 0.6f;
            emberNoise.frequency = 1.5f;

            ParticleSystem debris = Burst(root, "Escombros", debrisLit, 12, 0.8f, 1.6f, 2f, 0.04f, 0.1f, Color.white, Color.white);
            var debrisMain = debris.main;
            debrisMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            debrisMain.startRotation3D = true;
            ParticleSystemRenderer debrisRenderer = debris.GetComponent<ParticleSystemRenderer>();
            debrisRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            debrisRenderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var spin = debris.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            spin.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            spin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            Hemisphere(debris, 0.08f);
            Collide(debris, 0.35f);

            ParticleSystem smoke = Burst(root, "Humo", smokeAlpha, 6, 1.2f, 2f, -0.03f, 0.4f, 0.8f, new Color(0.45f, 0.25f, 0.15f, 0.45f), new Color(0.2f, 0.13f, 0.1f, 0.35f));
            var smokeMain = smoke.main;
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Hemisphere(smoke, 0.1f);
            SizeOverLife(smoke, Curve(0f, 0.6f, 1f, 2.2f));
            FadeOut(smoke);

            AddLight(root, new Color(1f, 0.6f, 0.3f), 10f, 6f, 0.35f);
            return root;
        }

        // ---------- Ayudantes de ParticleSystem ----------

        private static ParticleSystem Burst(GameObject parent, string name, Material material, int count,
            float lifeMin, float lifeMax, float gravity, float sizeMin, float sizeMax, Color colorA, Color colorB)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(count * 2, 10);
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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

        private static void Hemisphere(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = radius;
        }

        private static void Stretch(ParticleSystem ps, float velocityScale)
        {
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = 1f;
        }

        private static void Flat(ParticleSystem ps)
        {
            // El eje Z del efecto es la normal del impacto: el anillo se dibuja pegado a la superficie.
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }

        private static void Collide(ParticleSystem ps, float bounce)
        {
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.bounce = bounce;
            collision.dampen = 0.3f;
            collision.lifetimeLoss = 0.1f;
        }

        private static void SizeOverLife(ParticleSystem ps, AnimationCurve curve)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static void ColorOverLife(ParticleSystem ps, Color start, Color middle, Color end)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, 0.3f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static AnimationCurve Curve(params float[] timeValuePairs)
        {
            AnimationCurve curve = new AnimationCurve();
            for (int i = 0; i + 1 < timeValuePairs.Length; i += 2)
            {
                curve.AddKey(timeValuePairs[i], timeValuePairs[i + 1]);
            }
            return curve;
        }

        private static void AddLight(GameObject root, Color color, float intensity, float range, float duration)
        {
            GameObject go = new GameObject("Luz");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            VfxLightFade fade = go.AddComponent<VfxLightFade>();
            SerializedObject so = new SerializedObject(fade);
            so.FindProperty("duration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------- Asignacion a las pistolas ----------

        private static int AssignToGuns(GameObject muzzle, GameObject impact, EnergyBolt bolt)
        {
            int count = 0;
            Gradient tracerColor = new Gradient();
            tracerColor.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Cyan, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 1f) });

            foreach (string path in GunHolders)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (Gun gun in contents.GetComponentsInChildren<Gun>(true))
                {
                    SerializedObject so = new SerializedObject(gun);
                    so.FindProperty("muzzleFlashEffect").objectReferenceValue = muzzle;
                    so.FindProperty("impactEffect").objectReferenceValue = impact;
                    so.FindProperty("boltEffect").objectReferenceValue = bolt;
                    so.FindProperty("muzzleFlashDuration").floatValue = 1.2f;
                    so.FindProperty("impactEffectDuration").floatValue = 2.5f;
                    so.FindProperty("tracerDuration").floatValue = 0.06f;
                    LineRenderer tracer = so.FindProperty("tracer").objectReferenceValue as LineRenderer;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    if (tracer != null)
                    {
                        tracer.sharedMaterial = sparkAdd;
                        tracer.colorGradient = tracerColor;
                        tracer.widthCurve = AnimationCurve.Linear(0f, 0.06f, 1f, 0.02f);
                        tracer.textureMode = LineTextureMode.Stretch;
                        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    changed = true;
                    count++;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return count;
        }
    }
}
