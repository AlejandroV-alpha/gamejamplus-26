using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prepara el Gusano de Marte (import, Animator, materiales, prefab Fusion) y en la escena Mars
// reemplaza al Gusano Excavador por este (mismo lugar). Se ejecuta una vez; menú: Herramientas > Gusano de Marte.
[InitializeOnLoad]
public static class GusanoMarteSetup
{
    const string Root = "Assets/Enemies/GusanoMarte";
    const string Fbx = Root + "/Models/GusanoMarte_Animado.fbx";
    const string Tex = Root + "/Textures/GusanoMarte_Color.png";
    const string DustTex = Root + "/Textures/T_PolvoGusano.png";
    const string ControllerPath = Root + "/Animations/GusanoMarte.controller";
    const string PrefabPath = Root + "/Prefabs/GusanoMarte_Fusion.prefab";
    const string OldWormPrefab = "Assets/Enemies/GusanoExcavador/Gusano_Excavador_Fusion.prefab";
    const string ReksaiPrefab = "Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Fusion.prefab";
    const string MarsScene = "Assets/00_Scenes/Mars.unity";
    const string SpawnName = "GusanoMarte_Spawn";
    const float EnemyScale = 2.6f; // ~5 m de largo, boca de ~2 m
    const string Key = "gamejamplus26_GusanoMarte_setup_v1";
    static readonly string[] Clips = { "Idle", "Slither", "Burrow", "Emerge", "Grab", "GrabHold", "Release" };

    static GusanoMarteSetup()
    {
        if (Application.isBatchMode || EditorPrefs.GetBool(Key, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(Key, false)) return;
            EditorPrefs.SetBool(Key, true);
            BuildPrefab();
            ReplaceInMars();
        };
    }

    public static void BatchRun()
    {
        EditorPrefs.SetBool(Key, true);
        BuildPrefab();
        ReplaceInMars();
    }

    [MenuItem("Herramientas/Gusano de Marte/Reconstruir prefab")]
    public static void BuildPrefab()
    {
        ConfigureImport();
        var controller = BuildController();
        var body = BodyMaterial();
        var dust = DustMaterial();

        var root = new GameObject("GusanoMarte_Fusion");
        root.transform.localScale = Vector3.one * EnemyScale;
        root.AddComponent<NetworkObject>();
        var worm = root.AddComponent<GusanoMarteFusion>();
        var so = new SerializedObject(worm);
        so.FindProperty("dustMaterial").objectReferenceValue = dust;
        so.ApplyModifiedPropertiesWithoutUndo();
        var blood = root.AddComponent<GameJam.Combat.EnemyBloodFx>();
        var reksai = AssetDatabase.LoadAssetAtPath<GameObject>(ReksaiPrefab);
        var reksaiBlood = reksai ? reksai.GetComponentInChildren<GameJam.Combat.EnemyBloodFx>(true) : null;
        if (reksaiBlood)
        {
            EditorUtility.CopySerialized(reksaiBlood, blood);
            var bso = new SerializedObject(blood);
            var bs = bso.FindProperty("burstScale");
            if (bs != null) bs.floatValue = 2f;
            bso.ApplyModifiedPropertiesWithoutUndo();
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
            r.sharedMaterials = Enumerable.Repeat(body, r.sharedMaterials.Length).ToArray();
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

        // Colisionadores en los huesos: se le puede disparar aunque se alce o se mueva.
        foreach (var t in model.GetComponentsInChildren<Transform>())
        {
            float radius = t.name == "Head" ? .42f : t.name == "Seg1" || t.name == "Seg2" ? .36f : t.name == "Seg3" || t.name == "Seg4" ? .3f : 0f;
            if (radius <= 0f) continue;
            var sc = t.gameObject.AddComponent<SphereCollider>();
            sc.radius = radius * EnemyScale / Mathf.Max(.0001f, t.lossyScale.x);
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[Gusano de Marte] Prefab listo: " + PrefabPath);
    }

    // Quita el Gusano Excavador de Mars y pone el Gusano de Marte en su lugar. Guarda la escena.
    [MenuItem("Herramientas/Gusano de Marte/Reemplazar gusano en Mars")]
    public static void ReplaceInMars()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MarsScene);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(MarsScene, OpenSceneMode.Additive);

        var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OldWormPrefab);
        Vector3 pos = new Vector3(-44.64f, 5.36f, -223.21f);
        Quaternion rot = Quaternion.identity;
        int removed = 0;
        foreach (var g in scene.GetRootGameObjects().ToArray())
        {
            if (g == null) continue;
            bool isOld = g.GetComponentInChildren<GusanoExcavadorFusion>(true) != null
                || (oldPrefab && PrefabUtility.GetCorrespondingObjectFromOriginalSource(g) == oldPrefab);
            if (!isOld) continue;
            pos = g.transform.position;
            rot = g.transform.rotation;
            Object.DestroyImmediate(g);
            removed++;
        }
        var oldSpawn = scene.GetRootGameObjects().FirstOrDefault(g => g != null && g.name == SpawnName);
        if (oldSpawn) Object.DestroyImmediate(oldSpawn);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var go = new GameObject(SpawnName);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<NetworkObject>();
        var spawner = go.AddComponent<ReksaiFusionAreaSpawner>();
        spawner.enemyPrefab = prefab.GetComponent<NetworkObject>();
        spawner.activationDistance = 0f;
        go.transform.SetPositionAndRotation(pos, rot);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"[Gusano de Marte] Gusanos excavadores quitados de Mars: {removed}. Gusano de Marte colocado en {pos} y escena guardada.");
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
            c.loopTime = n == "Idle" || n == "Slither" || n == "GrabHold";
            c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    static AnimatorController BuildController()
    {
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!ac) ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ac.layers[0].stateMachine;
        foreach (var old in sm.states.ToArray()) sm.RemoveState(old.state);
        var all = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        foreach (var n in Clips)
        {
            var st = sm.AddState(n);
            st.motion = all.First(c => c.name == n || c.name.EndsWith("|" + n));
            if (n == "Idle") sm.defaultState = st;
        }
        EditorUtility.SetDirty(ac);
        return ac;
    }

    static Material BodyMaterial()
    {
        string path = Root + "/Materials/M_GusanoMarte.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", .3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material DustMaterial()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(DustTex);
        if (imp != null && !imp.alphaIsTransparency) { imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport(); }
        string path = Root + "/Materials/M_PolvoGusanoMarte.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DustTex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static void AlignForward(GameObject model)
    {
        var all = model.GetComponentsInChildren<Transform>();
        var head = all.FirstOrDefault(x => x.name == "Head");
        var tail = all.FirstOrDefault(x => x.name == "Tail");
        if (!head || !tail) { Debug.LogWarning("[Gusano de Marte] No encontré Head/Tail para orientar el modelo."); return; }
        Vector3 fwd = head.position - tail.position; fwd.y = 0;
        float yaw = Vector3.SignedAngle(fwd.normalized, model.transform.parent.forward, Vector3.up);
        model.transform.Rotate(0, yaw, 0, Space.World);
    }
}
