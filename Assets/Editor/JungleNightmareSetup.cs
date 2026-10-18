using System.Linq;
using Fusion;
using GameJam.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.EditorTools
{
    /// <summary>
    /// Convierte a Jungle Nightmare en enemigo de red y lo hace aparecer donde esta en la escena.
    /// </summary>
    public static class JungleNightmareSetup
    {
        private const string SourcePath = "Assets/Enemies/JungleNightmare/Prefabs/JungleNightmare_Frost.prefab";
        private const string FusionPath = "Assets/Enemies/JungleNightmare/Prefabs/JungleNightmare_Fusion.prefab";
        private const string ReksaiPath = "Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Marte.prefab";

        [MenuItem("Tools/GameJam/Convertir Jungle Nightmare en enemigo")]
        public static void Setup()
        {
            GameObject prefab = BuildFusionPrefab();

            Scene scene = SceneManager.GetActiveScene();
            GameObject[] placed = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("JungleNightmare") && !t.name.Contains("Spawn")
                    && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                .Select(t => t.gameObject).ToArray();

            int created = 0;
            foreach (GameObject instance in placed)
            {
                GameObject spawner = new GameObject(instance.name + "_Spawn");
                Undo.RegisterCreatedObjectUndo(spawner, "Spawner Jungle Nightmare");
                SceneManager.MoveGameObjectToScene(spawner, scene);
                spawner.transform.SetPositionAndRotation(instance.transform.position, instance.transform.rotation);
                spawner.AddComponent<NetworkObject>();
                ReksaiFusionAreaSpawner area = spawner.AddComponent<ReksaiFusionAreaSpawner>();
                area.enemyPrefab = prefab.GetComponent<NetworkObject>();
                Undo.DestroyObjectImmediate(instance);
                created++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[JungleNightmare] Prefab de red listo y " + created + " punto(s) de aparicion en " + scene.name);
        }

        private const string MediumPath = "Assets/Enemies/JungleNightmare/Prefabs/JungleNightmare_Mediano.prefab";
        private const string LargePath = "Assets/Enemies/JungleNightmare/Prefabs/JungleNightmare_Grande.prefab";
        private const float MediumScale = 1.5f;
        private const float LargeScale = 2.3f;

        [MenuItem("Tools/GameJam/Jungle Nightmare: manada mediana y grande")]
        public static void SetupPack()
        {
            BuildFusionPrefab();
            NetworkObject medium = BuildVariant(MediumPath, MediumScale, 6f);
            NetworkObject large = BuildVariant(LargePath, LargeScale, 8f);

            Scene scene = SceneManager.GetActiveScene();
            ReksaiFusionAreaSpawner[] existing = Object.FindObjectsByType<ReksaiFusionAreaSpawner>(FindObjectsSortMode.None)
                .Where(s => s.gameObject.scene == scene && s.enemyPrefab != null && s.enemyPrefab.name.StartsWith("JungleNightmare")).ToArray();
            if (existing.Length == 0)
            {
                Debug.LogError("[JungleNightmare] Primero ejecuta 'Convertir Jungle Nightmare en enemigo' en esta escena.");
                return;
            }

            // El punto original pasa a ser un grande; alrededor aparece la manada.
            Vector3 center = existing[0].transform.position;
            existing[0].enemyPrefab = large;
            EditorUtility.SetDirty(existing[0]);

            Transform old = GameObject.Find("JungleNightmare_Manada")?.transform;
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            GameObject group = new GameObject("JungleNightmare_Manada");
            Undo.RegisterCreatedObjectUndo(group, "Manada Jungle Nightmare");
            SceneManager.MoveGameObjectToScene(group, scene);

            // 5 medianos y 2 grandes repartidos en dos anillos.
            (float angle, float radius, bool big)[] layout =
            {
                (20f, 14f, false), (95f, 18f, false), (170f, 15f, true), (245f, 20f, false),
                (310f, 16f, false), (60f, 32f, true), (210f, 34f, false),
            };
            int index = 0;
            foreach (var slot in layout)
            {
                Vector3 offset = Quaternion.Euler(0f, slot.angle, 0f) * Vector3.forward * slot.radius;
                Vector3 position = Ground(center + offset);
                GameObject spawner = new GameObject("JungleNightmare_" + (slot.big ? "Grande" : "Mediano") + "_" + (++index));
                spawner.transform.SetParent(group.transform, true);
                spawner.transform.SetPositionAndRotation(position, Quaternion.LookRotation(center - position, Vector3.up));
                spawner.AddComponent<NetworkObject>();
                spawner.AddComponent<ReksaiFusionAreaSpawner>().enemyPrefab = slot.big ? large : medium;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = group;
            Debug.Log("[JungleNightmare] Manada creada: 5 medianos y 3 grandes en " + scene.name);
        }

        /// <summary>
        /// Variante de tamaño con huellas mas duraderas para que se vea el rastro brillante.
        /// </summary>
        private static NetworkObject BuildVariant(string path, float scale, float footprintSeconds)
        {
            GameObject baseFusion = AssetDatabase.LoadAssetAtPath<GameObject>(FusionPath);
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(baseFusion);
            try
            {
                root.name = System.IO.Path.GetFileNameWithoutExtension(path);
                root.transform.localScale = Vector3.one * scale;
                foreach (JungleNightmare.FrostGroundFX fx in root.GetComponentsInChildren<JungleNightmare.FrostGroundFX>(true))
                {
                    fx.footprintLifetime = footprintSeconds;
                    fx.glowIntensity = 4f;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<NetworkObject>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Vector3 Ground(Vector3 point)
        {
            float best = float.MaxValue;
            Vector3 result = point;
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 60f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < best)
                {
                    best = hit.distance;
                    result = hit.point;
                }
            }
            return result;
        }

        private static GameObject BuildFusionPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
                if (root.GetComponent<JungleNightmareFusionEnemy>() == null) root.AddComponent<JungleNightmareFusionEnemy>();

                // Colision a la medida del cuerpo para recibir balas.
                Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
                if (capsule == null) capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = root.transform.InverseTransformPoint(bounds.center);
                capsule.height = Mathf.Max(0.5f, bounds.size.y);
                capsule.radius = Mathf.Max(0.3f, Mathf.Min(bounds.extents.x, bounds.extents.z));

                // Reutiliza la sangre y las manchas de suelo del Reksai.
                EnemyBloodFx blood = root.GetComponent<EnemyBloodFx>();
                if (blood == null) blood = root.AddComponent<EnemyBloodFx>();
                GameObject reksai = AssetDatabase.LoadAssetAtPath<GameObject>(ReksaiPath);
                EnemyBloodFx reksaiBlood = reksai != null ? reksai.GetComponent<EnemyBloodFx>() : null;
                if (reksaiBlood != null) EditorUtility.CopySerialized(reksaiBlood, blood);

                return PrefabUtility.SaveAsPrefabAsset(root, FusionPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
