using System.Linq;
using GameJam.Crafting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.EditorTools
{
    /// <summary>
    /// Pone la caja animada de Blender como visual de la CraftingTable de la escena abierta
    /// y le agrega la interaccion con interfaz de crafteo.
    /// </summary>
    public static class CraftingBoxSetup
    {
        private const string Folder = "Assets/06j_Models/Caja_Craft";
        private const string ModelPath = Folder + "/Caja_Craft_Juego.fbx";
        private const string ControllerPath = Folder + "/Caja_Craft.controller";
        private const string MaterialPath = Folder + "/M_Caja_Craft.mat";
        private const float ModelScale = 9f;

        [MenuItem("Tools/GameJam/Agregar Caja de Crafteo a la escena")]
        public static void Setup()
        {
            ConfigureImporter();
            Material material = CreateMaterial();
            AnimatorController controller = CreateController();

            CraftingTable table = Object.FindObjectsByType<CraftingTable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.gameObject.scene == SceneManager.GetActiveScene());
            if (table == null)
            {
                Debug.LogError("[CajaCrafteo] La escena abierta no tiene una CraftingTable.");
                return;
            }

            Transform old = table.transform.Find("Caja_Visual");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // Oculta el visual anterior de la mesa sin tocar red ni colisiones.
            foreach (Renderer renderer in table.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, table.transform);
            visual.name = "Caja_Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            StandUpright(visual);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.enabled = true;
            }

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            FitCollider(table, visual);

            CraftingBox box = table.GetComponent<CraftingBox>();
            if (box == null)
            {
                box = table.gameObject.AddComponent<CraftingBox>();
            }
            SerializedObject so = new SerializedObject(box);
            so.FindProperty("lidAnimator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(table.GetComponent<BoxCollider>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);

            EditorSceneManager.MarkSceneDirty(table.gameObject.scene);
            EditorSceneManager.SaveScene(table.gameObject.scene);
            Selection.activeGameObject = table.gameObject;
            Debug.Log("[CajaCrafteo] Caja agregada a " + table.name + " en " + table.gameObject.scene.name);
        }

        private static void ConfigureImporter()
        {
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.globalScale = ModelScale;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = ClipName(clip.takeName);
                clip.loopTime = false;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static string ClipName(string take)
        {
            foreach (string name in new[] { "Abierta", "Cerrada", "Abrir", "Cerrar" })
            {
                if (take.Contains(name))
                {
                    return name;
                }
            }
            return take;
        }

        private static Material CreateMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                // Este shader multiplica por el color de vertices, que es donde viene la pintura del modelo.
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController CreateController()
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Get(string name) => clips.FirstOrDefault(c => c.name == name);

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Abrir", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Cerrar", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState closed = machine.AddState("Cerrada");
            closed.motion = Get("Cerrada");
            AnimatorState opening = machine.AddState("Abrir");
            opening.motion = Get("Abrir");
            AnimatorState opened = machine.AddState("Abierta");
            opened.motion = Get("Abierta");
            AnimatorState closing = machine.AddState("Cerrar");
            closing.motion = Get("Cerrar");
            machine.defaultState = closed;

            Trigger(closed, opening, "Abrir");
            Trigger(closing, opening, "Abrir");
            Trigger(opened, closing, "Cerrar");
            Trigger(opening, closing, "Cerrar");
            ExitTo(opening, opened);
            ExitTo(closing, closed);
            return controller;
        }

        private static void Trigger(AnimatorState from, AnimatorState to, string trigger)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void ExitTo(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
        }

        /// <summary>
        /// Gira el modelo hasta que la tapa quede encima de la base y lo apoya en el suelo.
        /// Corrige la conversion de ejes Blender (Z arriba) a Unity (Y arriba).
        /// </summary>
        private static void StandUpright(GameObject visual)
        {
            Renderer lid = visual.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.Contains("Tapa"));
            Renderer box = visual.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.Contains("Base"));
            if (lid == null || box == null)
            {
                return;
            }

            Vector3[] candidates =
            {
                Vector3.zero, new Vector3(-90f, 0f, 0f), new Vector3(90f, 0f, 0f), new Vector3(180f, 0f, 0f),
                new Vector3(0f, 0f, 90f), new Vector3(0f, 0f, -90f),
            };
            Vector3 best = Vector3.zero;
            float bestHeight = float.MinValue;
            foreach (Vector3 euler in candidates)
            {
                visual.transform.localRotation = Quaternion.Euler(euler);
                float height = lid.bounds.center.y - box.bounds.center.y;
                if (height > bestHeight)
                {
                    bestHeight = height;
                    best = euler;
                }
            }
            visual.transform.localRotation = Quaternion.Euler(best);

            // Baja la mesa hasta el suelo real y apoya la base sobre su pivote.
            Transform table = visual.transform.parent;
            Vector3 from = table.position + Vector3.up * 50f;
            float groundY = table.position.y;
            float closest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(table) || hit.distance >= closest)
                {
                    continue;
                }
                closest = hit.distance;
                groundY = hit.point.y;
            }
            Undo.RecordObject(table, "Apoyar caja en el suelo");
            table.position = new Vector3(table.position.x, groundY, table.position.z);
            // La mesa es una instancia de prefab: sin esto Unity descarta el cambio al guardar y en Play vuelve arriba.
            PrefabUtility.RecordPrefabInstancePropertyModifications(table);
            // Se hunde un poco para que no se vea apoyada en el aire en suelos irregulares.
            visual.transform.position += Vector3.up * (table.position.y - box.bounds.min.y - 0.05f);
        }

        /// <summary>
        /// Ajusta la colision de la mesa al tamaño de la caja nueva.
        /// </summary>
        private static void FitCollider(CraftingTable table, GameObject visual)
        {
            BoxCollider collider = table.GetComponent<BoxCollider>();
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (collider == null || renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }
            collider.center = table.transform.InverseTransformPoint(bounds.center);
            Vector3 size = table.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }
    }
}
