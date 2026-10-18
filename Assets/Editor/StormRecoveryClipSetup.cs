using System.Linq;
using GameJam.Environment.Mars;
using UnityEditor;
using UnityEngine;

// Bake a standalone clip with a real Unity-generated reference after import.
[InitializeOnLoad]
public static class StormRecoveryClipSetup
{
    const string Model = "Assets/07Edu_Animations/Astronaut/FixedAnimations/RIGGED_ASTRONAUT.v2.0@Stand Up.fbx";
    const string Rig = "Assets/06Edu_Models/RIGGED_ASTRONAUT.v2.0.fbx";
    const string Destination = "Assets/Resources/Animations/StormStandUp.anim";
    static StormRecoveryClipSetup()
    {
        EditorApplication.update += WhenReady;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            { EditorApplication.update -= WhenReady; EditorApplication.update += WhenReady; }
        };
    }
    static void WhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WhenReady;
        Apply();
    }
    [MenuItem("Tools/GameJam/Reparar animacion de recuperacion de tormenta")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Rig).OfType<Avatar>().FirstOrDefault();
        if (Application.dataPath.Replace('\\', '/').Contains("/Library/VP/")) return;
        if (importer == null || avatar == null)
        { Debug.LogError("[Storm recovery setup] No se pudo cargar FBX o avatar humanoide de referencia."); return; }
        if (importer.animationType != ModelImporterAnimationType.Human
            || importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther || importer.sourceAvatar != avatar
            || AssetDatabase.LoadAssetAtPath<AnimationClip>(Destination) == null)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            // Use Unity's actual imported take, instead of a hand-written FBX clip ID.
            var clips = importer.defaultClipAnimations;
            if (clips.Length > 0)
            {
                clips[0].name = "Storm Stand Up";
                clips[0].loopTime = false;
                clips[0].lockRootHeightY = true;
                clips[0].lockRootPositionXZ = true;
                importer.clipAnimations = new[] { clips[0] };
            }
            importer.SaveAndReimport();
        }
        var source = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview") && c.isHumanMotion);
        if (source == null)
        {
            Debug.LogError("[Storm recovery setup] Stand Up no contiene un clip Humanoid valido; revisar importacion del FBX.");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Animations")) AssetDatabase.CreateFolder("Assets/Resources", "Animations");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Destination);
        if (clip == null)
        {
            clip = Object.Instantiate(source);
            clip.name = "StormStandUp";
            AssetDatabase.CreateAsset(clip, Destination);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/02_Prefabs/Fusion" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var storm in prefab.GetComponentsInChildren<PlayerSandstorm>(true))
            {
                var serialized = new SerializedObject(storm);
                var property = serialized.FindProperty("standUpClip");
                if (property.objectReferenceValue == clip) continue;
                property.objectReferenceValue = clip;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Storm recovery setup] Clip Humanoid horneado y asignado a los prefabs Fusion.");
    }
}
