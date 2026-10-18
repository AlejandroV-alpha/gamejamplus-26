using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NeptuneIceDistribution
{
    const string Models = "Assets/06Bc_Models/EstructuresIce";
    const string MaterialPath = Models + "/b_iceTransparent_mat.mat";
    const string TerrainPath = "Assets/05_Materials/Neptune/b_NeptuneTerrain.asset";
    const string Output = "Assets/02_Prefabs/Neptune";

    [MenuItem("Tools/Neptune/Create Additional Ice Clusters")]
    public static void Create()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        if (material == null || terrain == null) throw new InvalidOperationException("Missing Neptune terrain or ice material.");
        if (!AssetDatabase.IsValidFolder(Output))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(Output).Replace('\\', '/'), "Neptune");
        string prefabPath = Output + "/b_NeptuneIceClusters.prefab";
        if (File.Exists(prefabPath)) throw new InvalidOperationException("Ice clusters already exist; edit their scene instance.");

        string[] paths = Directory.GetFiles(Models, "*.fbx").OrderBy(p => p, StringComparer.Ordinal).ToArray();
        int verifiedMeshes = 0;
        foreach (string path in paths)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var names = model.GetComponentsInChildren<MeshRenderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToArray();
            importer.addCollider = true;
            foreach (string name in names)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
            importer.SaveAndReimport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var collider = filter.GetComponent<MeshCollider>();
                if (collider == null || collider.sharedMesh != filter.sharedMesh || collider.convex)
                    throw new InvalidOperationException("Invalid mesh collider: " + path);
                verifiedMeshes++;
            }
        }

        // Exclude the two largest meshes from additional copies; existing placements stay intact.
        var choices = paths.Where(p => !p.EndsWith("EstructeIce.fbx") && !p.EndsWith("estrucuture_4Ice.fbx")).ToArray();
        if (choices.Length == 0) choices = paths;
        var root = new GameObject("b_NeptuneIceClusters");
        try
        {
            Vector2[] centers = { new Vector2(.22f,.20f), new Vector2(.74f,.32f),
                new Vector2(.24f,.65f), new Vector2(.72f,.78f) };
            int placed = 0;
            for (int cluster = 0; cluster < centers.Length; cluster++)
            {
                var group = new GameObject("Ice Cluster " + (cluster+1)).transform;
                group.SetParent(root.transform, false);
                for (int j = 0; j < 3; j++)
                {
                    string path = choices[placed % choices.Length];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    instance.transform.SetParent(group, false);
                    instance.name = "Ice " + (placed+1).ToString("00") + " - " + Path.GetFileNameWithoutExtension(path);
                    var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length == 0) throw new InvalidOperationException("No mesh renderers: " + path);
                    foreach (var renderer in renderers)
                        renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    float width = j == 0 ? 22f : j == 1 ? 12f : 7f;
                    float scale = width / Mathf.Max(bounds.size.x, bounds.size.z, .01f);
                    instance.transform.localScale *= scale;
                    instance.transform.rotation = Quaternion.Euler(0, cluster*73+j*41, 0) * instance.transform.rotation;
                    bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    float x = terrain.size.x * centers[cluster].x + (j-1) * 20f;
                    float z = terrain.size.z * centers[cluster].y + (j == 1 ? 17f : -8f);
                    float ground = terrain.GetInterpolatedHeight(x/terrain.size.x, z/terrain.size.z) - 8f;
                    // Terrain origin matches the Neptune scene. Sink the base slightly into the ice.
                    instance.transform.position += new Vector3(x-180f-bounds.center.x,
                        ground-bounds.min.y-.25f, z-18f-bounds.center.z);
                    placed++;
                }
            }
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(saved.transform, out string guid, out long transformId);
            AssetDatabase.SaveAssets();
            Debug.Log("NEPTUNE_ICE_LAYOUT_OK models="+paths.Length+" colliders="+verifiedMeshes+
                " added="+placed+" terrainSize="+terrain.size+" prefabGuid="+guid+" transformId="+transformId);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
