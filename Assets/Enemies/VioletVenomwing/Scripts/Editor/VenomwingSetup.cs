using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prepara el Violet Venomwing (import, Animator, materiales, prefab Fusion) y coloca su spawner en la escena Mars.
// Se ejecuta una vez; para repetirlo: Herramientas > Venomwing.
[InitializeOnLoad]
public static class VenomwingSetup
{
    const string Root = "Assets/Enemies/VioletVenomwing";
    const string Fbx = Root + "/Models/VioletVenomwing_Animado.fbx";
    const string Tex = Root + "/Textures/VioletVenomwing_Color.png";
    const string ControllerPath = Root + "/Animations/VioletVenomwing.controller";
    const string PrefabPath = Root + "/Prefabs/Venomwing_Fusion.prefab";
    const string ReksaiPrefab = "Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Fusion.prefab";
    const string MarsScene = "Assets/00_Scenes/Mars.unity";
    const string SpawnName = "Venomwing_Spawn";
    const float EnemyScale = 1.6f; // ~3 m de envergadura
    const string Key = "gamejamplus26_Venomwing_setup_v1";

    static VenomwingSetup()
    {
        if (EditorPrefs.GetBool(Key, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(Key, false)) return;
            EditorPrefs.SetBool(Key, true);
            BuildPrefab();
            PlaceInMars();
        };
    }

    // Para ejecutar sin abrir el editor: Unity.exe -batchmode -projectPath ... -executeMethod VenomwingSetup.BatchRun -quit
    public static void BatchRun()
    {
        EditorPrefs.SetBool(Key, true);
        BuildPrefab();
        PlaceInMars();
    }

    [MenuItem("Herramientas/Venomwing/Reconstruir prefab")]
    public static void BuildPrefab()
    {
        ConfigureImport();
        var controller = BuildController();
        var body = BodyMaterial();
        var (glow, puddle, blob) = AcidMaterials();

        var root = new GameObject("Venomwing_Fusion");
        root.transform.localScale = Vector3.one * EnemyScale;
        root.AddComponent<NetworkObject>();
        var col = root.AddComponent<SphereCollider>();
        col.radius = .6f;
        col.center = new Vector3(0f, .15f, 0f);
        var enemy = root.AddComponent<VenomwingFusionEnemy>();
        var so = new SerializedObject(enemy);
        so.FindProperty("acidGlowMaterial").objectReferenceValue = glow;
        so.FindProperty("acidPuddleMaterial").objectReferenceValue = puddle;
        so.FindProperty("acidBlobMaterial").objectReferenceValue = blob;
        so.ApplyModifiedPropertiesWithoutUndo();

        var blood = root.AddComponent<GameJam.Combat.EnemyBloodFx>();
        var reksai = AssetDatabase.LoadAssetAtPath<GameObject>(ReksaiPrefab);
        var reksaiBlood = reksai ? reksai.GetComponentInChildren<GameJam.Combat.EnemyBloodFx>(true) : null;
        if (reksaiBlood) EditorUtility.CopySerialized(reksaiBlood, blood);

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
            r.sharedMaterials = Enumerable.Repeat(body, r.sharedMaterials.Length).ToArray();

        // Reutiliza el asset si ya existe para no cambiar su GUID (lo referencia el spawner de Mars).
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[Venomwing] Prefab listo: " + PrefabPath);
    }

    // Coloca el spawner en Mars y guarda la escena (si no está abierta, se abre en segundo plano).
    [MenuItem("Herramientas/Venomwing/Colocar en Mars")]
    public static void PlaceInMars()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MarsScene);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(MarsScene, OpenSceneMode.Additive);

        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SpawnName);
        if (old) Object.DestroyImmediate(old);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var go = new GameObject(SpawnName);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<NetworkObject>();
        var spawner = go.AddComponent<ReksaiFusionAreaSpawner>();
        spawner.enemyPrefab = prefab.GetComponent<NetworkObject>();
        spawner.activationDistance = 0f;

        // Delante y a la derecha del punto de aparición de los jugadores (El Cuernudo está delante, al centro).
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var playerSpawn = all.FirstOrDefault(t => t.name == "j_FusionSpawn");
        Vector3 basePos = playerSpawn ? playerSpawn.position : Vector3.zero;
        Vector3 fwd = playerSpawn ? Vector3.ProjectOnPlane(playerSpawn.forward, Vector3.up).normalized : Vector3.forward;
        if (fwd.sqrMagnitude < .1f) fwd = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 pos = basePos + fwd * 28f + right * 22f;
        var terrain = all.Select(t => t.GetComponent<Terrain>()).Where(t => t != null && t.terrainData != null)
            .FirstOrDefault(t => pos.x >= t.transform.position.x && pos.z >= t.transform.position.z
                && pos.x <= t.transform.position.x + t.terrainData.size.x && pos.z <= t.transform.position.z + t.terrainData.size.z);
        if (terrain) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 3f;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(basePos - pos == Vector3.zero ? -fwd : Vector3.ProjectOnPlane(basePos - pos, Vector3.up));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"[Venomwing] Spawner colocado y guardado en Mars en {pos}.");
    }

    static void ConfigureImport()
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.importAnimation = true;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.animationCompression = ModelImporterAnimationCompression.Off;
        var clips = imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            string n = c.name.Split('|').Last();
            c.loopTime = n == "Idle" || n == "FlyFast";
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
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!ac) ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ac.layers[0].stateMachine;
        foreach (var old in sm.states.ToArray()) sm.RemoveState(old.state);
        foreach (var n in new[] { "Idle", "FlyFast", "Attack", "Spit" })
        {
            var st = sm.AddState(n);
            st.motion = Clip(n);
            if (n == "Idle") sm.defaultState = st;
        }
        EditorUtility.SetDirty(ac);
        return ac;
    }

    static Material BodyMaterial()
    {
        var m = Mat("M_Venomwing", "Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", .35f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static (Material glow, Material puddle, Material blob) AcidMaterials()
    {
        var dot = SaveTexture("T_AcidoGlow", 64, (u, v) =>
        {
            float r = new Vector2(u - .5f, v - .5f).magnitude * 2f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Exp(-r * r * 4f) * (1f - Smooth(.85f, 1f, r)) + Mathf.Exp(-r * r * 25f)));
        });
        var puddleTex = SaveTexture("T_CharcoAcido", 128, (u, v) =>
        {
            float dx = u - .5f, dy = v - .5f, ang = Mathf.Atan2(dy, dx);
            float edge = .78f + .1f * Mathf.PerlinNoise(Mathf.Cos(ang) * 1.5f + 5f, Mathf.Sin(ang) * 1.5f + 5f) + .05f * Mathf.Sin(ang * 7f);
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f / edge;
            float body = 1f - Smooth(.82f, 1f, r);
            float rim = Mathf.Exp(-Mathf.Pow((r - .9f) / .07f, 2f));
            float spots = Mathf.PerlinNoise(u * 14f, v * 14f);
            var c = Color.Lerp(new Color(.15f, .55f, .05f), new Color(.6f, 1f, .25f), Mathf.Clamp01(rim * .9f + spots * .35f));
            c.a = Mathf.Clamp01(body * (.65f + .25f * spots) + rim * .3f);
            return c;
        });

        var glow = Mat("M_AcidoGlow", "Universal Render Pipeline/Particles/Unlit");
        glow.SetTexture("_BaseMap", dot);
        glow.SetColor("_BaseColor", new Color(1.4f, 1.6f, 1.2f, 1f));
        Transparent(glow, true);

        var puddle = Mat("M_CharcoAcido", "Universal Render Pipeline/Particles/Unlit");
        puddle.SetTexture("_BaseMap", puddleTex);
        puddle.SetColor("_BaseColor", Color.white);
        Transparent(puddle, false);
        puddle.SetFloat("_Cull", 0f);

        var blob = Mat("M_AcidoBola", "Universal Render Pipeline/Unlit");
        blob.SetColor("_BaseColor", new Color(.55f, 1f, .2f));

        foreach (var m in new[] { glow, puddle, blob }) EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return (glow, puddle, blob);
    }

    static Material Mat(string name, string shader)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find(shader);
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        else if (sh && m.shader != sh) m.shader = sh;
        return m;
    }

    static void Transparent(Material m, bool additive)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

    static Texture2D SaveTexture(string name, int res, System.Func<float, float, Color> paint)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                tex.SetPixel(x, y, paint((x + .5f) / res, (y + .5f) / res));
        tex.Apply();
        string path = Root + "/Textures/" + name + ".png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Gira el modelo para que la cabeza mire al +Z del padre (usa las alas para saber izquierda/derecha).
    static void AlignForward(GameObject model)
    {
        var all = model.GetComponentsInChildren<Transform>();
        var l = all.FirstOrDefault(x => x.name == "WingUpper_L");
        var r = all.FirstOrDefault(x => x.name == "WingUpper_R");
        if (!l || !r) { Debug.LogWarning("[Venomwing] No encontré las alas para orientar el modelo."); return; }
        Vector3 left = l.position - r.position; left.y = 0;
        Vector3 fwd = Vector3.Cross(Vector3.up, left.normalized);
        float yaw = Vector3.SignedAngle(fwd, model.transform.parent.forward, Vector3.up);
        model.transform.Rotate(0, yaw, 0, Space.World);
    }
}
