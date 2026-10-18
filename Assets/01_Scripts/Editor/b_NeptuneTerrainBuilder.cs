using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Generates persistent assets so the result remains sculptable with Unity Terrain tools.
public static class b_NeptuneTerrainBuilder
{
    const string Folder = "Assets/05_Materials/Neptune";
    const string Scene = "Assets/00_Scenes/b_NeptuneExploration.unity";
    const int Resolution = 513;
    const float Width = 360f;

    [InitializeOnLoadMethod]
    static void RegisterSceneSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += ApplyReferenceToInitialTerrain;
        EditorApplication.delayCall += () => OnSceneOpened(SceneManager.GetActiveScene(), OpenSceneMode.Single);
    }

    static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            mode != OpenSceneMode.Single || scene.path != Scene || scene.isDirty ||
            AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/b_NeptuneTerrain.asset") != null)
            return;
        EditorApplication.delayCall += () =>
        {
            if (SceneManager.GetActiveScene().path == Scene &&
                !SceneManager.GetActiveScene().isDirty &&
                AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/b_NeptuneTerrain.asset") == null)
                Build();
        };
    }

    [MenuItem("Tools/Neptune/Create Ice Terrain")]
    public static void Build()
    {
        bool alreadyOpen = SceneManager.GetActiveScene().path == Scene;
        if (!alreadyOpen && Application.isBatchMode == false &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = alreadyOpen ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(Scene);
        // Never replace an artist's edited terrain when the menu is invoked again.
        if (GameObject.Find("Neptune - Ice Terrain") != null)
        {
            Debug.Log("Neptune terrain already exists; use Terrain tools to edit it.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/05_Materials", "Neptune");

        var data = new TerrainData { name = "b_NeptuneTerrain_GentleV3", heightmapResolution = Resolution,
            size = new Vector3(Width, 90f, Width), alphamapResolution = 256,
            baseMapResolution = 512 };
        var heights = CreateHeights();
        data.SetHeights(0, 0, heights);

        var snow = Layer("b_Frost", new Color(.48f, .72f, .85f), .65f);
        var ice = Layer("b_BlueIce", new Color(.16f, .4f, .58f), .7f);
        data.terrainLayers = new[] { snow, ice };
        var splat = new float[256, 256, 2];
        for (int z = 0; z < 256; z++)
        for (int x = 0; x < 256; x++)
        {
            float exposed = .75f + .15f * Mathf.PerlinNoise(x * .025f, z * .025f);
            splat[z, x, 0] = 1f - exposed;
            splat[z, x, 1] = exposed;
        }
        data.SetAlphamaps(0, 0, splat);
        AssetDatabase.CreateAsset(data, Folder + "/b_NeptuneTerrain.asset");
        var ground = Terrain.CreateTerrainGameObject(data);
        ground.name = "Neptune - Ice Terrain";
        ground.transform.position = new Vector3(-180, -8, -18);
        var terrain = ground.GetComponent<Terrain>();
        terrain.heightmapPixelError = 3f;
        terrain.basemapDistance = 1000f;
        var material = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
        AssetDatabase.CreateAsset(material, Folder + "/b_NeptuneTerrain.mat");
        terrain.materialTemplate = material;
        var old = GameObject.Find("Neptune - Starter Platform");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        var camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0, 3f, 0);
            camera.transform.rotation = Quaternion.Euler(4, 12, 0);
            camera.farClipPlane = 650;
        }
        var sun = RenderSettings.sun;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(18, -35, 0);
            sun.intensity = .65f;
            sun.color = new Color(.68f, .81f, 1f);
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.18f, .27f, .43f);
        RenderSettings.ambientEquatorColor = new Color(.1f, .16f, .28f);
        RenderSettings.ambientGroundColor = new Color(.045f, .07f, .13f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = .0045f;
        RenderSettings.fogColor = new Color(.055f, .105f, .2f);
        var spawn = new GameObject("Neptune - Player Spawn");
        spawn.transform.position = new Vector3(0, 2, 0);
        Selection.activeGameObject = ground;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("NEPTUNE_TERRAIN_CREATED: 360m terrain, four low ice shelves and four flat clearings.");
    }

    // One-time migration of the initial generated landscape to the requested reference.
    static void ApplyReferenceToInitialTerrain()
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/b_NeptuneTerrain.asset");
        if (data == null || (data.name != "b_NeptuneTerrain" && data.name != "b_NeptuneTerrain_ReferenceV2")) return;
        Undo.RecordObject(data, "Neptune ice reference relief");
        data.SetHeights(0, 0, CreateHeights());
        var maps = new float[256, 256, 2];
        for (int z = 0; z < 256; z++)
        for (int x = 0; x < 256; x++)
        {
            float blue = .75f + .15f * Mathf.PerlinNoise(x * .025f, z * .025f);
            maps[z, x, 0] = 1f - blue;
            maps[z, x, 1] = blue;
        }
        data.SetAlphamaps(0, 0, maps);
        data.name = "b_NeptuneTerrain_GentleV3";
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("NEPTUNE_REFERENCE_APPLIED: low ice shelves and broad flat clearings.");
    }
    static float[,] CreateHeights()
    {
        var heights = new float[Resolution, Resolution];
        // Broad, low shelves leave ample room for hand-placed ice models.
        var shelves = new[] {
            new Vector4(75f, 100f, 5f, 65f),
            new Vector4(280f, 150f, 7f, 75f),
            new Vector4(100f, 280f, 6f, 70f),
            new Vector4(285f, 305f, 4f, 60f)
        };
        for (int z = 0; z < Resolution; z++)
        for (int x = 0; x < Resolution; x++)
        {
            float wx = x * Width / (Resolution - 1);
            float wz = z * Width / (Resolution - 1);
            float height = 8f + Mathf.PerlinNoise(wx * .012f + 17f, wz * .012f + 41f) * .5f;
            foreach (var shelf in shelves)
            {
                float distance = Vector2.Distance(new Vector2(wx, wz), new Vector2(shelf.x, shelf.y));
                float blend = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(shelf.w * .3f, shelf.w, distance));
                height += shelf.z * blend;
            }
            // Flat plazas with wide, gentle transitions into the surrounding ice.
            for (int clearing = 0; clearing < 4; clearing++)
            {
                float cz = 65f + clearing * 75f;
                float cx = 180f + Mathf.Sin(cz * .025f) * 35f;
                float distance = Vector2.Distance(new Vector2(wx, wz), new Vector2(cx, cz));
                float flat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 42f, distance));
                height = Mathf.Lerp(height, 8f, flat);
            }
            float landing = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15f, 35f,
                Vector2.Distance(new Vector2(wx, wz), new Vector2(180f, 18f))));
            heights[z, x] = Mathf.Lerp(height, 8f, landing) / 90f;
        }
        return heights;
    }

    static TerrainLayer Layer(string name, Color color, float smoothness)
    {
        var texture = new Texture2D(64, 64) { name = name, wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
            pixels[y * 64 + x] = color * (.87f + .13f * Mathf.PerlinNoise(x * .2f, y * .2f));
        texture.SetPixels(pixels);
        texture.Apply();
        string path = Folder + "/" + name + ".png";
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var layer = new TerrainLayer { name = name,
            diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
            tileSize = new Vector2(12, 12), smoothness = smoothness, metallic = 0f };
        AssetDatabase.CreateAsset(layer, Folder + "/" + name + ".terrainlayer");
        return layer;
    }
}



