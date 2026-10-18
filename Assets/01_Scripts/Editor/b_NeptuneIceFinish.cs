using UnityEditor;
using UnityEngine;

// Surface treatment only: keeps the hand-editable terrain and its open spaces.
public static class b_NeptuneIceFinish
{
    const string Folder = "Assets/05_Materials/Neptune";

    [MenuItem("Tools/Neptune/Apply Ice Surface Detail")]
    public static void Apply()
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(Folder + "/b_NeptuneTerrain.asset");
        if (data == null) throw new System.InvalidOperationException("Create Neptune Terrain first.");
        Undo.RecordObject(data, "Neptune ice surface detail");
        Surface("b_Frost", false);
        Surface("b_BlueIce", true);
        var maps = new float[data.alphamapResolution, data.alphamapResolution, 2];
        float minWhite = 1f, maxWhite = 0f;
        for (int z = 0; z < data.alphamapResolution; z++)
        for (int x = 0; x < data.alphamapResolution; x++)
        {
            float u = x / (float)(data.alphamapResolution - 1);
            float v = z / (float)(data.alphamapResolution - 1);
            float broad = Mathf.PerlinNoise(u * 9f + 71f, v * 9f + 23f);
            float edge = Mathf.PerlinNoise(u * 31f + 14f, v * 31f + 5f);
            float white = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.33f, .67f,
                broad + (edge - .5f) * .23f));
            // Raised shelves collect frost; the lowest plains expose blue ice.
            white = Mathf.Clamp01(white * .85f +
                Mathf.InverseLerp(9f, 14f, data.GetInterpolatedHeight(u, v)) * .35f);
            maps[z, x, 0] = white;
            maps[z, x, 1] = 1f - white;
            minWhite = Mathf.Min(minWhite, white);
            maxWhite = Mathf.Max(maxWhite, white);
        }
        data.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"NEPTUNE_ICE_FINISH_OK frostRange={minWhite:F2}-{maxWhite:F2}, heightmap preserved");
    }

    static void Surface(string name, bool ice)
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var relief = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];
        var bumps = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Periodic coordinate mapping avoids seams between terrain texture tiles.
            float u = x / (float)size;
            float v = y / (float)size;
            float n = PeriodicNoise(u, v, 3f);
            float grain = PeriodicNoise(u, v, 35f);
            float vein = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 4f +
                Mathf.Sin(v * Mathf.PI * 4f) * .65f + (n - .5f) * 2f));
            float crack = ice ? 1f - Mathf.SmoothStep(.012f, .05f, vein) : 0f;
            var color = ice
                ? Color.Lerp(new Color(.065f, .22f, .36f), new Color(.27f, .58f, .73f), n)
                : Color.Lerp(new Color(.64f, .79f, .85f), new Color(.9f, .95f, .97f), n);
            color *= .94f + grain * .1f;
            color = Color.Lerp(color, new Color(.035f, .13f, .22f), crack * .65f);
            color.a = 1f;
            colors[y * size + x] = color;
            float h = ice ? .5f + (n - .5f) * .08f - crack * .18f : .35f + grain * .28f;
            bumps[y * size + x] = new Color(h, h, h, 1f);
        }
        texture.SetPixels(colors); texture.Apply();
        relief.SetPixels(bumps); relief.Apply();
        string diffusePath = Folder + "/" + name + ".png";
        string normalPath = Folder + "/" + name + "_Normal.png";
        System.IO.File.WriteAllBytes(diffusePath, texture.EncodeToPNG());
        System.IO.File.WriteAllBytes(normalPath, relief.EncodeToPNG());
        Object.DestroyImmediate(texture); Object.DestroyImmediate(relief);
        AssetDatabase.ImportAsset(diffusePath);
        AssetDatabase.ImportAsset(normalPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        importer.textureType = TextureImporterType.NormalMap;
        importer.convertToNormalmap = true;
        importer.heightmapScale = ice ? .045f : .08f;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Folder + "/" + name + ".terrainlayer");
        Undo.RecordObject(layer, "Neptune ice material");
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        layer.tileSize = new Vector2(ice ? 22f : 10f, ice ? 22f : 10f);
        layer.normalScale = ice ? .35f : .5f;
        layer.smoothness = ice ? .82f : .28f;
        EditorUtility.SetDirty(layer);
    }

    static float PeriodicNoise(float u, float v, float frequency)
    {
        float a = Mathf.PerlinNoise(u * frequency + 31, v * frequency + 19);
        float b = Mathf.PerlinNoise((u - 1) * frequency + 31, v * frequency + 19);
        float c = Mathf.PerlinNoise(u * frequency + 31, (v - 1) * frequency + 19);
        float d = Mathf.PerlinNoise((u - 1) * frequency + 31, (v - 1) * frequency + 19);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }
}
