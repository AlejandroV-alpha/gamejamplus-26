using GameJam.Combat;
using GameJam.Inventory;
using UnityEditor;
using UnityEngine;

namespace GameJam.EditorTools
{
    /// <summary>
    /// Agrega el "game feel" (hitmarker, hit flash, hit-stop, explosión y botín magnético) a los enemigos.
    /// Por ahora: Violet Venomwing. Menú: Tools > GameJam > Game feel en enemigos.
    /// </summary>
    [InitializeOnLoad]
    public static class EnemyFeelSetup
    {
        const string Key = "gamejamplus26_EnemyFeel_venomwing_v1";
        const string VenomwingPrefab = "Assets/Enemies/VioletVenomwing/Prefabs/Venomwing_Fusion.prefab";
        const string DeathFx = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Eerie/CFXR2 WW Enemy Explosion.prefab";
        const string OrbMat = "Assets/Enemies/VioletVenomwing/Materials/M_AcidoBola.mat";

        static EnemyFeelSetup()
        {
            if (Application.isBatchMode || EditorPrefs.GetBool(Key, false)) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(Key, false)) return;
                EditorPrefs.SetBool(Key, true);
                Apply();
            };
        }

        public static void BatchRun()
        {
            EditorPrefs.SetBool(Key, true);
            Apply();
        }

        [MenuItem("Tools/GameJam/Game feel en enemigos")]
        public static void Apply()
        {
            Setup(VenomwingPrefab, "VENOMWING", 1.6f, new Color(.55f, 1f, .3f),
                ("Assets/_SO/Materials/EnergyCrystal.asset", 1, 2),
                ("Assets/_SO/Materials/RedMineral.asset", 1, 3));
            AssetDatabase.SaveAssets();
        }

        static void Setup(string prefabPath, string displayName, float deathScale, Color orbColor,
            params (string material, int min, int max)[] loot)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var fb = contents.GetComponent<EnemyHitFeedback>();
            if (fb == null) fb = contents.AddComponent<EnemyHitFeedback>();
            var so = new SerializedObject(fb);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("deathEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(DeathFx);
            so.FindProperty("deathEffectScale").floatValue = deathScale;
            so.FindProperty("orbMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(OrbMat);
            so.FindProperty("orbColor").colorValue = orbColor;
            var lootProp = so.FindProperty("loot");
            lootProp.arraySize = loot.Length;
            for (int i = 0; i < loot.Length; i++)
            {
                var e = lootProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MaterialData>(loot[i].material);
                e.FindPropertyRelative("amount").vector2IntValue = new Vector2Int(loot[i].min, loot[i].max);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[GameFeel] Configurado en " + displayName);
        }
    }
}
