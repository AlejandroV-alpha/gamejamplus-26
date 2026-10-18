using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.EditorTools
{
    /// <summary>
    /// Ordena b_NeptuneExploration: nave y objetos de mision al centro, y monstruos
    /// que aparecen con el tiempo junto a los minerales en lugar de una manada fija.
    /// </summary>
    public static class NeptuneLayoutSetup
    {
        private const string MediumPath = "Assets/Enemies/JungleNightmare/Prefabs/JungleNightmare_Mediano.prefab";
        private const string LargePath = "Assets/Enemies/JungleNightmare/Prefabs/JungleNightmare_Grande.prefab";

        [MenuItem("Tools/GameJam/Neptuno: nave al centro y monstruos en minerales")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

            Vector3 center = MapCenter(all);
            int moved = CenterMissionObjects(all, center);
            bool spawner = ReplaceEnemySpawners(scene, center);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Neptuno] Centro {center}. Objetos movidos: {moved}. Spawner de minerales: {spawner}");
        }

        private static Vector3 MapCenter(Transform[] all)
        {
            Transform terrain = all.FirstOrDefault(t => t.name == "Neptune - Ice Terrain");
            if (terrain != null)
            {
                Terrain t = terrain.GetComponent<Terrain>();
                if (t != null) return terrain.position + t.terrainData.size * .5f;
                Renderer r = terrain.GetComponentInChildren<Renderer>();
                if (r != null) return r.bounds.center;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Nave en el centro; caja de crafteo, estacion de reparacion y aparicion del jugador a su alrededor.
        /// </summary>
        private static int CenterMissionObjects(Transform[] all, Vector3 center)
        {
            int moved = 0;
            Transform fuselage = all.FirstOrDefault(t => t.name == "Fuselaje_central");
            Transform ship = fuselage != null ? ShipRoot(fuselage) : null;
            Vector3 forward = Vector3.forward, right = Vector3.right;
            if (ship != null)
            {
                Place(ship, center, null);
                forward = Vector3.ProjectOnPlane(ship.forward, Vector3.up).normalized;
                right = Vector3.Cross(Vector3.up, forward);
                moved++;
            }

            moved += PlaceNamed(all, "CraftingTable", center + right * 8f + forward * 2f, center);
            moved += PlaceNamed(all, "ShipRepairStation", center - right * 8f + forward * 2f, center);
            moved += PlaceNamed(all, "Neptune - Player Spawn", center - forward * 14f, center);
            return moved;
        }

        /// <summary>
        /// Sube desde una pieza de la nave mientras el conjunto siga midiendo como una nave,
        /// para no arrastrar un contenedor que agrupe medio nivel.
        /// </summary>
        private static Transform ShipRoot(Transform part)
        {
            Transform ship = part;
            while (ship.parent != null)
            {
                Renderer[] renderers = ship.parent.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) break;
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                if (Mathf.Max(bounds.size.x, bounds.size.z) > 40f) break;
                ship = ship.parent;
            }
            return ship;
        }

        private static int PlaceNamed(Transform[] all, string name, Vector3 position, Vector3 lookAt)
        {
            Transform target = all.FirstOrDefault(t => t.name == name && (t.parent == null || t.parent.name != name));
            if (target == null) return 0;
            Place(target, position, lookAt);
            return 1;
        }

        private static void Place(Transform target, Vector3 position, Vector3? lookAt)
        {
            Undo.RecordObject(target, "Ordenar Neptuno");
            Vector3 grounded = Ground(position, target);
            float pivotToBottom = target.position.y - LowestPoint(target);
            target.position = grounded + Vector3.up * pivotToBottom;
            if (lookAt.HasValue)
            {
                Vector3 look = lookAt.Value - target.position; look.y = 0f;
                if (look.sqrMagnitude > .01f) target.rotation = Quaternion.LookRotation(look);
            }
            // Las instancias de prefab pierden el cambio al guardar si no se registra.
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static float LowestPoint(Transform target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            return renderers.Length == 0 ? target.position.y : renderers.Min(r => r.bounds.min.y);
        }

        private static Vector3 Ground(Vector3 point, Transform ignore)
        {
            float best = float.MaxValue;
            Vector3 result = point;
            // Solo cuenta el suelo de hielo: limites invisibles, nieblas o rocas altas dejaban la nave flotando.
            GameObject terrain = GameObject.Find("Neptune - Ice Terrain");
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(point.x, 500f, point.z), Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore))
            {
                if ((ignore != null && hit.collider.transform.IsChildOf(ignore)) || hit.distance >= best) continue;
                bool isGround = hit.collider is TerrainCollider || (terrain != null && hit.collider.transform.IsChildOf(terrain.transform));
                if (terrain != null && !isGround) continue;
                best = hit.distance;
                result = hit.point;
            }
            return result;
        }

        /// <summary>
        /// Quita la manada fija y deja un unico spawner que usa los minerales del mapa.
        /// </summary>
        private static bool ReplaceEnemySpawners(Scene scene, Vector3 center)
        {
            foreach (ReksaiFusionAreaSpawner old in Object.FindObjectsByType<ReksaiFusionAreaSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(s => s.gameObject.scene == scene && s.enemyPrefab != null && s.enemyPrefab.name.StartsWith("JungleNightmare")).ToArray())
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            GameObject pack = GameObject.Find("JungleNightmare_Manada");
            if (pack != null) Undo.DestroyObjectImmediate(pack);
            GameObject previous = GameObject.Find("JungleNightmare_SpawnerMinerales");
            if (previous != null) Undo.DestroyObjectImmediate(previous);

            NetworkObject medium = AssetDatabase.LoadAssetAtPath<GameObject>(MediumPath)?.GetComponent<NetworkObject>();
            NetworkObject large = AssetDatabase.LoadAssetAtPath<GameObject>(LargePath)?.GetComponent<NetworkObject>();
            if (medium == null && large == null)
            {
                Debug.LogError("[Neptuno] Faltan los prefabs mediano/grande: ejecuta antes 'Jungle Nightmare: manada mediana y grande'.");
                return false;
            }

            GameObject go = new GameObject("JungleNightmare_SpawnerMinerales");
            Undo.RegisterCreatedObjectUndo(go, "Spawner de minerales");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = center;
            go.AddComponent<NetworkObject>();
            MineralEnemySpawner spawner = go.AddComponent<MineralEnemySpawner>();
            SerializedObject so = new SerializedObject(spawner);
            so.FindProperty("mediumPrefab").objectReferenceValue = medium;
            so.FindProperty("largePrefab").objectReferenceValue = large;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
