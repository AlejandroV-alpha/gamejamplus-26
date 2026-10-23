using System.IO;
using GameJam.Combat;
using UnityEditor;
using UnityEngine;

namespace GameJam.EditorTools
{
    /// <summary>
    /// Usa la salpicadura de sangre de Cartoon FX Remaster ("CFXR2 Blood Shape Splash") cuando se daña a un enemigo.
    /// Solo cambia los prefabs de enemigos; los astronautas conservan su sangre.
    /// </summary>
    [InitializeOnLoad]
    public static class EnemyCartoonBloodSetup
    {
        const string BloodPath = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Liquids/CFXR2 Blood Shape Splash.prefab";
        const string Key = "gamejamplus26_EnemyCartoonBlood_v1";

        static readonly string[] EnemyPrefabs =
        {
            "Assets/Enemies/ElCuernudo/Prefabs/ElCuernudo_Fusion.prefab",
            "Assets/Enemies/GusanoMarte/Prefabs/GusanoMarte_Fusion.prefab",
            "Assets/Enemies/VioletVenomwing/Prefabs/Venomwing_Fusion.prefab",
            "Assets/Enemies/JungleNightmare/Prefabs/JungleNightmare_Fusion.prefab",
            "Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Fusion.prefab",
            "Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Marte.prefab",
            "Assets/Enemies/GusanoExcavador/Gusano_Excavador_Fusion.prefab",
        };

        static EnemyCartoonBloodSetup()
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

        [MenuItem("Tools/GameJam/Sangre Cartoon FX en enemigos")]
        public static void Apply()
        {
            var blood = AssetDatabase.LoadAssetAtPath<GameObject>(BloodPath);
            if (blood == null)
            {
                Debug.LogWarning("[SangreCartoon] No encontré " + BloodPath);
                return;
            }
            int count = 0;
            foreach (var path in EnemyPrefabs)
            {
                if (!File.Exists(path)) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                foreach (var fx in contents.GetComponentsInChildren<EnemyBloodFx>(true))
                {
                    var so = new SerializedObject(fx);
                    so.FindProperty("bloodBurst").objectReferenceValue = blood;
                    so.FindProperty("burstDuration").floatValue = 2.5f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                    count++;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[SangreCartoon] Sangre de Cartoon FX asignada a " + count + " enemigo(s).");
        }
    }
}
