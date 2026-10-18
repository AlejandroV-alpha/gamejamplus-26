using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.EditorTools
{
    /// <summary>
    /// Configura el FBX del gusano (clips suavizados en Blender), crea su Animator
    /// y lo coloca en la cuenca de la escena abierta (s_Mars).
    /// </summary>
    public static class GusanoExcavadorSetup
    {
        private const string Folder = "Assets/Enemies/GusanoExcavador";
        private const string ModelPath = Folder + "/Gusano_Excavador.fbx";
        private const string ControllerPath = Folder + "/Gusano_Excavador.controller";
        private const string PrefabPath = Folder + "/Gusano_Excavador_Fusion.prefab";
        private static readonly string[] Clips = { "Mover", "Atacar", "Enterrarse", "Salir", "Salir_y_Saltar" };

        [MenuItem("Tools/GameJam/Agregar Gusano Excavador a la escena")]
        public static void Setup()
        {
            ConfigureImporter();
            AnimatorController controller = CreateController();
            GameObject prefab = CreatePrefab(controller);
            PlaceInScene(prefab);
        }

        private static void ConfigureImporter()
        {
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = Clips.FirstOrDefault(c => clip.takeName.EndsWith(c)) ?? clip.takeName;
                if (clip.takeName.EndsWith("Salir_y_Saltar")) clip.name = "Salir_y_Saltar";
                clip.loopTime = clip.name == "Mover";
                clip.loopPose = clip.name == "Mover";
                // El descenso y el salto viven en el hueso Raiz y se conservan en la pose.
                clip.lockRootHeightY = false;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static AnimatorController CreateController()
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (string name in Clips)
            {
                AnimatorState state = machine.AddState(name);
                state.motion = clips.FirstOrDefault(c => c.name == name);
                if (name == "Mover") machine.defaultState = state;
            }
            return controller;
        }

        private static GameObject CreatePrefab(AnimatorController controller)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject root = new GameObject("Gusano_Excavador");
            try
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Visual";
                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                root.AddComponent<NetworkObject>();
                root.AddComponent<GusanoExcavadorFusion>();

                // Tierra, agujeros y surcos.
                GusanoExcavadorFX fx = root.AddComponent<GusanoExcavadorFX>();
                SerializedObject so = new SerializedObject(fx);
                so.FindProperty("dustMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/02_Prefabs/Gun/VFX/M_VFX_Humo.mat");
                so.FindProperty("holeMaterial").objectReferenceValue = CreateHoleMaterial();
                so.ApplyModifiedPropertiesWithoutUndo();

                // Salpicadura al recibir balas: misma sangre y manchas que el Reksai.
                GameJam.Combat.EnemyBloodFx blood = root.AddComponent<GameJam.Combat.EnemyBloodFx>();
                GameObject reksai = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Marte.prefab");
                var source = reksai != null ? reksai.GetComponent<GameJam.Combat.EnemyBloodFx>() : null;
                if (source != null) EditorUtility.CopySerialized(source, blood);
                // Gusano gigante: chorros y manchas mucho mas grandes para que se note cada balazo.
                SerializedObject bloodSo = new SerializedObject(blood);
                bloodSo.FindProperty("burstScale").floatValue = 4f;
                bloodSo.FindProperty("splatsPerHit").intValue = 5;
                bloodSo.FindProperty("splatSpread").floatValue = 3f;
                bloodSo.FindProperty("splatSize").vector2Value = new Vector2(1.5f, 3.5f);
                bloodSo.ApplyModifiedPropertiesWithoutUndo();

                // Dientes y mandibulas con vida propia.
                visual.AddComponent<GusanoTeethMotion>();
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Agujero de tierra removida: centro oscuro, borde irregular y terrones alrededor.
        /// </summary>
        private static Material CreateHoleMaterial()
        {
            const int size = 256;
            string texPath = Folder + "/T_Gusano_Agujero.png";
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size - .5f, v = (y + .5f) / size - .5f;
                    float angle = Mathf.Atan2(v, u);
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float edge = .78f + .14f * Mathf.PerlinNoise(Mathf.Cos(angle) * 2.5f + 5f, Mathf.Sin(angle) * 2.5f + 5f);
                    float grain = Mathf.PerlinNoise(x * .08f, y * .08f);
                    float alpha = Mathf.Clamp01((edge - r) / .12f);
                    float clods = r > edge * .8f && r < edge * 1.15f ? Mathf.Clamp01((Mathf.PerlinNoise(x * .2f, y * .2f) - .55f) * 6f) : 0f;
                    alpha = Mathf.Max(alpha * (.7f + .3f * grain), clods * .9f);
                    // Hondo y oscuro en el centro, tierra revuelta mas clara hacia el borde.
                    float depth = Mathf.SmoothStep(0f, 1f, r / edge);
                    Color c = Color.Lerp(new Color(.08f, .03f, .02f), new Color(.45f, .2f, .1f), depth) * (.8f + .4f * grain);
                    c.a = alpha;
                    tex.SetPixel(x, y, c);
                }
            }
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            string matPath = Folder + "/M_Gusano_Agujero.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 20;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void PlaceInScene(GameObject prefab)
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GusanoExcavadorFusion old in Object.FindObjectsByType<GusanoExcavadorFusion>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(g => g.gameObject.scene == scene).ToArray())
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            Transform basin = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "j_WormBasin");
            Vector3 position = basin != null ? basin.position : Vector3.zero;
            Terrain terrain = Terrain.activeTerrains.FirstOrDefault();
            if (terrain != null) position.y = terrain.SampleHeight(position) + terrain.transform.position.y;

            GameObject worm = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            worm.transform.position = position;
            Undo.RegisterCreatedObjectUndo(worm, "Gusano Excavador");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = worm;
            Debug.Log("[Gusano] Colocado en " + (basin != null ? "j_WormBasin" : "el origen") + " de " + scene.name + " en " + position);
        }
    }
}
