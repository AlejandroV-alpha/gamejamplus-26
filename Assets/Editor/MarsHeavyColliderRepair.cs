using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MarsHeavyColliderRepair
{
    const string Antenna = "Assets/06Bc_Models/broken antenna/Antenna2.fbx";
    const string AntennaGuid = "4c49c75a109b84d479a86063b1046ae5";
    static MarsHeavyColliderRepair() { EditorApplication.update += WhenReady; }
    static void WhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WhenReady;
        // Virtual Multiplayer Play Mode editors have a read-only AssetDatabase.
        string root = Application.dataPath.Replace('\\', '/');
        if (root.Contains("/Library/VP/")) return;
        Repair();
    }
    [MenuItem("Tools/GameJam/Optimizar collider de antena de Marte")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var meshes = AssetDatabase.LoadAllAssetsAtPath(Antenna).OfType<Mesh>().ToArray();
        if (meshes.Length == 0) return;
        int changed = 0;
        // Edit only collider serialization; do not open/save scenes or overwrite live object changes.
        foreach (string path in Directory.GetFiles("Assets/00_Scenes", "*.unity", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles("Assets/02_Prefabs", "*.prefab", SearchOption.AllDirectories)))
        {
            string original = File.ReadAllText(path);
            if (!original.Contains(AntennaGuid)) continue;
            string updated = Regex.Replace(original, @"--- !u!64 &(-?\d+)\r?\nMeshCollider:\r?\n(?<body>.*?)(?=--- !u!|\z)", match =>
            {
                string body = match.Groups["body"].Value;
                var reference = Regex.Match(body, @"m_Mesh: \{fileID: (-?\d+), guid: " + AntennaGuid);
                if (!reference.Success) return match.Value;
                long id = long.Parse(reference.Groups[1].Value);
                var mesh = meshes.FirstOrDefault(m => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string guid, out long localId) && localId == id);
                if (mesh == null) return match.Value;
                Bounds bounds = mesh.bounds;
                string filtered = Regex.Replace(body, @"^  m_(Convex|CookingOptions|Mesh):.*\r?\n", "", RegexOptions.Multiline);
                filtered = Regex.Replace(filtered, @"^  serializedVersion:.*\r?\n", "", RegexOptions.Multiline);
                string center = Vector(bounds.center);
                string size = Vector(bounds.size);
                changed++;
                return "--- !u!65 &" + match.Groups[1].Value + "\nBoxCollider:\n" + filtered.TrimEnd()
                    + "\n  serializedVersion: 3\n  m_Size: " + size + "\n  m_Center: " + center + "\n";
            }, RegexOptions.Singleline);
            if (updated == original) continue;
            File.WriteAllText(path, updated);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        if (changed > 0) Debug.Log("[Mars collision] Reemplazados " + changed + " colliders de antena por cajas; modelo visual conservado.");
    }
    static string Vector(Vector3 v) => "{x: " + v.x.ToString("R", CultureInfo.InvariantCulture)
        + ", y: " + v.y.ToString("R", CultureInfo.InvariantCulture) + ", z: " + v.z.ToString("R", CultureInfo.InvariantCulture) + "}";
}
