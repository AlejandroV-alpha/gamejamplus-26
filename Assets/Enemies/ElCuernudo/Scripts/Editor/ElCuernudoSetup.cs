using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prepara El Cuernudo (import, material, Animator, prefab Fusion) y coloca su spawner en s_Mars.
// Se ejecuta una vez automáticamente; para repetirlo: menú Herramientas > El Cuernudo > ...
[InitializeOnLoad]
public static class ElCuernudoSetup
{
    const string Root = "Assets/Enemies/ElCuernudo";
    const string Fbx = Root + "/Models/ElCuernudo_Animado.fbx";
    const string ControllerPath = Root + "/Animations/ElCuernudo.controller";
    const string MaterialPath = Root + "/Materials/ElCuernudo_Mat.mat";
    const string PrefabPath = Root + "/Prefabs/ElCuernudo_Fusion.prefab";
    const string ReksaiPrefab = "Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Fusion.prefab";
    const string MarsScene = "Assets/00_Scenes/s_Mars.unity";
    const string SpawnName = "ElCuernudo_Spawn";
    const float EnemyScale = 2.6f; // ~5 m de alto
    const string DoneKey = "gamejamplus26_ElCuernudo_setup_v1";
    const string AnimKey = "gamejamplus26_ElCuernudo_anims_v5";

    const string FxDir = Root + "/Resources/ElCuernudoFX";
    const string FxKey = "gamejamplus26_ElCuernudo_fx_v1";

    static ElCuernudoSetup()
    {
        if (!EditorPrefs.GetBool(FxKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorPrefs.SetBool(FxKey, true);
            EditorApplication.delayCall += CreateFxAssets;
        }
        // Animaciones nuevas (Walk + Run mejorado): solo reimporta y actualiza el Animator.
        if (EditorPrefs.GetBool(DoneKey, false) && !EditorPrefs.GetBool(AnimKey, false)
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorPrefs.SetBool(AnimKey, true);
            EditorApplication.delayCall += UpdateAnimations;
        }
        if (EditorPrefs.GetBool(DoneKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            return;
        }
        Schedule();
    }

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.EnteredEditMode || EditorPrefs.GetBool(DoneKey, false)) return;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        Schedule();
    }

    static void Schedule()
    {
        EditorPrefs.SetBool(DoneKey, true);
        EditorApplication.delayCall += () =>
        {
            BuildPrefab();
            PlaceInMars();
        };
    }

    [MenuItem("Herramientas/El Cuernudo/Actualizar animaciones")]
    public static void UpdateAnimations()
    {
        ConfigureImport();
        BuildController();
        AssetDatabase.SaveAssets();
        Debug.Log("[El Cuernudo] Animaciones actualizadas (Idle, Walk, Run, Attack, AttackL, Attack2, Death).");
    }

    // ---------------------------------------------------------------- Camuflaje: texturas y materiales

    [MenuItem("Herramientas/El Cuernudo/Crear efectos de camuflaje")]
    public static void CreateFxAssets()
    {
        if (!AssetDatabase.IsValidFolder(Root + "/Resources")) AssetDatabase.CreateFolder(Root, "Resources");
        if (!AssetDatabase.IsValidFolder(FxDir)) AssetDatabase.CreateFolder(Root + "/Resources", "ElCuernudoFX");

        var print = SaveTexture("T_ClawPrint", 256, ClawPrint, false);
        var dot = SaveTexture("T_SoftDot", 64, SoftDot, true);
        var cloud = SaveTexture("T_DustCloud", 128, DustCloud, true);

        // Huella: mismo shader que las pisadas de los astronautas, con la garra de 3 dedos.
        var fp = Mat("ElCuernudo_Footprint", "GameJam/Mars/SoftFootprint");
        fp.SetTexture("_BaseMap", print);
        fp.SetFloat("_Strength", 1.25f);
        fp.SetFloat("_DrawDistance", 60f);

        // Partículas brillantes que brotan de la piel al disolverse.
        var glow = Mat("ElCuernudo_FX_Glow", "Universal Render Pipeline/Particles/Unlit");
        glow.SetTexture("_BaseMap", dot);
        glow.SetColor("_BaseColor", new Color(1.6f, 1.6f, 1.6f, 1f));
        MakeTransparent(glow, true);

        // Polvo de arena marciana (onda al camuflarse y nubecitas en cada pisada).
        var dust = Mat("ElCuernudo_FX_Dust", "Universal Render Pipeline/Particles/Unlit");
        dust.SetTexture("_BaseMap", cloud);
        dust.SetColor("_BaseColor", Color.white);
        MakeTransparent(dust, false);

        // Cuerpo transparente con brillo para el fundido.
        var body = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var cloak = Mat("ElCuernudo_Cloak", "Universal Render Pipeline/Lit");
        if (body) cloak.CopyPropertiesFromMaterial(body);
        MakeTransparent(cloak, false);
        cloak.EnableKeyword("_EMISSION");
        cloak.SetColor("_EmissionColor", Color.black);
        cloak.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        foreach (var m in new[] { fp, glow, dust, cloak }) EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        Debug.Log("[El Cuernudo] Efectos de camuflaje creados en " + FxDir);
    }

    static Material Mat(string name, string shader)
    {
        string path = FxDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find(shader);
        if (sh == null) Debug.LogWarning("[El Cuernudo] No encontré el shader " + shader);
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        else if (sh && m.shader != sh) m.shader = sh;
        return m;
    }

    static void MakeTransparent(Material m, bool additive)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    delegate Color Painter(float u, float v);

    static Texture2D SaveTexture(string name, int res, Painter paint, bool alpha)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        var px = new Color[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                px[y * res + x] = paint((x + .5f) / res, (y + .5f) / res);
        tex.SetPixels(px);
        tex.Apply();
        string path = FxDir + "/" + name + ".png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = alpha;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }

    static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }

    static float ValueNoise(float x, float y)
    {
        float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Segment(Vector2 p, Vector2 a, Vector2 b, out float t)
    {
        Vector2 ab = b - a;
        t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    // Huella de garra: almohadilla + 3 dedos abiertos con uñas. Negro = hundido (el shader multiplica).
    static Color ClawPrint(float u, float v)
    {
        var p = new Vector2(u, v);
        float ink = 0f;
        var hp = new Vector2((u - .5f) / .19f, (v - .28f) / .15f);
        float heel = 1f - Smooth(.75f, 1.05f, hp.magnitude);
        ink = Mathf.Max(ink, heel * (.65f + .35f * (1f - Mathf.Min(1f, hp.magnitude))));
        var root = new Vector2(.5f, .38f);
        float[] angles = { -30f, 0f, 30f };
        float[] lengths = { .34f, .42f, .34f };
        for (int i = 0; i < 3; i++)
        {
            float a = angles[i] * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            var b = root + dir * lengths[i];
            float d = Segment(p, root + dir * .05f, b, out float t);
            float width = Mathf.Lerp(.07f, .045f, t);
            ink = Mathf.Max(ink, (1f - Smooth(width * .7f, width, d)) * (.7f + .3f * t));
            var c = b + dir * .1f;
            float dc = Segment(p, b, c, out float tc);
            float cw = Mathf.Lerp(.03f, .006f, tc);
            ink = Mathf.Max(ink, 1f - Smooth(cw * .5f, cw, dc));
        }
        var cp = new Vector2((u - .5f) / .36f, (v - .47f) / .46f);
        float halo = (1f - Smooth(.6f, 1f, cp.magnitude)) * .18f * ValueNoise(u * 18f, v * 18f);
        ink = Mathf.Max(ink, halo);
        ink *= .85f + .15f * ValueNoise(u * 40f, v * 40f);
        float shade = 1f - Mathf.Clamp01(ink);
        return new Color(shade, shade, shade, 1f);
    }

    static Color SoftDot(float u, float v)
    {
        float r = new Vector2(u - .5f, v - .5f).magnitude * 2f;
        float a = Mathf.Exp(-r * r * 4f) * (1f - Smooth(.85f, 1f, r));
        float core = Mathf.Exp(-r * r * 30f);
        return new Color(1f, 1f, 1f, Mathf.Clamp01(a + core));
    }

    static Color DustCloud(float u, float v)
    {
        float r = new Vector2(u - .5f, v - .5f).magnitude * 2f;
        float n = ValueNoise(u * 5f, v * 5f) * .6f + ValueNoise(u * 11f + 7f, v * 11f + 3f) * .4f;
        float a = (1f - Smooth(.35f, 1f, r)) * Mathf.Lerp(.45f, 1f, n);
        return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
    }

    [MenuItem("Herramientas/El Cuernudo/Reconstruir prefab")]
    public static void BuildPrefab()
    {
        ConfigureImport();
        var controller = BuildController();
        var material = BuildMaterial();

        var root = new GameObject("ElCuernudo_Fusion");
        root.transform.localScale = Vector3.one * EnemyScale;
        root.AddComponent<NetworkObject>();
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.radius = .55f; capsule.height = 2f; capsule.center = new Vector3(0, 1f, 0);
        root.AddComponent<ElCuernudoFusionEnemy>();
        var blood = root.AddComponent<GameJam.Combat.EnemyBloodFx>();
        var reksai = AssetDatabase.LoadAssetAtPath<GameObject>(ReksaiPrefab);
        var reksaiBlood = reksai ? reksai.GetComponentInChildren<GameJam.Combat.EnemyBloodFx>(true) : null;
        if (reksaiBlood)
        {
            EditorUtility.CopySerialized(reksaiBlood, blood);
            var so = new SerializedObject(blood);
            so.FindProperty("burstScale").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Visual";
        model.transform.SetParent(root.transform, false);
        var anim = model.GetComponent<Animator>();
        if (!anim) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        AlignForward(model);
        foreach (var r in model.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[El Cuernudo] Prefab listo: " + PrefabPath);
    }

    [MenuItem("Herramientas/El Cuernudo/Colocar en s_Mars")]
    public static void PlaceInMars()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != MarsScene)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(MarsScene, OpenSceneMode.Single);
        }

        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SpawnName);
        if (old) Object.DestroyImmediate(old);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var go = new GameObject(SpawnName);
        go.AddComponent<NetworkObject>();
        var spawner = go.AddComponent<ReksaiFusionAreaSpawner>();
        spawner.enemyPrefab = prefab.GetComponent<NetworkObject>();
        spawner.activationDistance = 0f;

        // Lo pone a ~35 m del punto de aparición de los jugadores, mirando hacia ellos.
        var playerSpawn = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "j_FusionSpawn");
        Vector3 basePos = playerSpawn ? playerSpawn.position : Vector3.zero;
        Vector3 dir = playerSpawn ? Vector3.ProjectOnPlane(playerSpawn.forward, Vector3.up).normalized : Vector3.forward;
        if (dir.sqrMagnitude < .1f) dir = Vector3.forward;
        Vector3 pos = basePos + dir * 35f;
        var terrain = Terrain.activeTerrains.FirstOrDefault(t => pos.x >= t.transform.position.x && pos.z >= t.transform.position.z
            && pos.x <= t.transform.position.x + t.terrainData.size.x && pos.z <= t.transform.position.z + t.terrainData.size.z);
        if (terrain) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(-dir);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log($"[El Cuernudo] Spawner colocado en s_Mars en {pos}. Guarda la escena (Ctrl+S) para conservarlo.");
    }

    const string MarsMainScene = "Assets/00_Scenes/Mars.unity";
    const string MarsPlaceKey = "gamejamplus26_ElCuernudo_mars_v1";

    [InitializeOnLoadMethod]
    static void PlaceInMarsMainOnce()
    {
        if (EditorPrefs.GetBool(MarsPlaceKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(MarsPlaceKey, false)) return;
            EditorPrefs.SetBool(MarsPlaceKey, true);
            PlaceInMarsMain();
        };
    }

    // Coloca el spawner en la escena Mars (la de exploración) y la guarda.
    // Si Mars no está abierta, se abre en segundo plano y se cierra al terminar: no toca la escena actual.
    [MenuItem("Herramientas/El Cuernudo/Colocar en Mars")]
    public static void PlaceInMarsMain()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MarsMainScene);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(MarsMainScene, OpenSceneMode.Additive);

        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SpawnName);
        if (old) Object.DestroyImmediate(old);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var go = new GameObject(SpawnName);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<NetworkObject>();
        var spawner = go.AddComponent<ReksaiFusionAreaSpawner>();
        spawner.enemyPrefab = prefab.GetComponent<NetworkObject>();
        spawner.activationDistance = 0f;

        // Igual que en s_Mars: ~35 m delante del punto de aparición de los jugadores, mirando hacia ellos.
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var playerSpawn = all.FirstOrDefault(t => t.name == "j_FusionSpawn");
        Vector3 basePos = playerSpawn ? playerSpawn.position : Vector3.zero;
        Vector3 dir = playerSpawn ? Vector3.ProjectOnPlane(playerSpawn.forward, Vector3.up).normalized : Vector3.forward;
        if (dir.sqrMagnitude < .1f) dir = Vector3.forward;
        Vector3 pos = basePos + dir * 35f;
        var terrain = all.Select(t => t.GetComponent<Terrain>()).Where(t => t != null && t.terrainData != null)
            .FirstOrDefault(t => pos.x >= t.transform.position.x && pos.z >= t.transform.position.z
                && pos.x <= t.transform.position.x + t.terrainData.size.x && pos.z <= t.transform.position.z + t.terrainData.size.z);
        if (terrain) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(-dir);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"[El Cuernudo] Spawner colocado y guardado en Mars en {pos}.");
    }

    static void ConfigureImport()
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.importAnimation = true;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        // Sin compresión: la reducción de keyframes movía los pies y los hacía patinar o flotar.
        imp.animationCompression = ModelImporterAnimationCompression.Off;
        var clips = imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            string n = c.name.Split('|').Last();
            c.loopTime = n == "Idle" || n == "Run" || n == "Walk";
            c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    static AnimationClip Clip(string name) =>
        AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .First(c => c.name == name || c.name.EndsWith("|" + name));

    static AnimatorController BuildController()
    {
        // Se reutiliza el asset (mismo GUID) para no romper la referencia del prefab.
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!ac) ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ac.layers[0].stateMachine;
        foreach (var old in sm.states.ToArray()) sm.RemoveState(old.state);
        // Sin transiciones: el script hace CrossFade a cada estado según el modo replicado.
        foreach (var n in new[] { "Idle", "Walk", "Run", "Attack", "AttackL", "Attack2", "Death" })
        {
            var st = sm.AddState(n);
            st.motion = Clip(n);
            if (n == "Idle") sm.defaultState = st;
        }
        EditorUtility.SetDirty(ac);
        return ac;
    }

    static Material BuildMaterial()
    {
        string tex = Root + "/Textures/";
        var n = (TextureImporter)AssetImporter.GetAtPath(tex + "ElCuernudo_Normal.png");
        if (n.textureType != TextureImporterType.NormalMap) { n.textureType = TextureImporterType.NormalMap; n.SaveAndReimport(); }
        var m = (TextureImporter)AssetImporter.GetAtPath(tex + "ElCuernudo_Metallic.png");
        if (m.sRGBTexture) { m.sRGBTexture = false; m.SaveAndReimport(); }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "ElCuernudo_Color.png"));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "ElCuernudo_Normal.png"));
        mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "ElCuernudo_Metallic.png"));
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetFloat("_Smoothness", .25f);
        mat.SetFloat("_GlossMapScale", .25f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Gira el modelo para que su frente coincida con el +Z del padre.
    static void AlignForward(GameObject model)
    {
        var all = model.GetComponentsInChildren<Transform>();
        var l = all.FirstOrDefault(x => x.name == "LeftHand");
        var r = all.FirstOrDefault(x => x.name == "RightHand");
        if (!l || !r) { Debug.LogWarning("[El Cuernudo] No encontré las manos para orientar el modelo."); return; }
        Vector3 left = l.position - r.position; left.y = 0;
        Vector3 fwd = Vector3.Cross(Vector3.up, left.normalized);
        float yaw = Vector3.SignedAngle(fwd, model.transform.parent.forward, Vector3.up);
        model.transform.Rotate(0, yaw, 0, Space.World);
    }
}
