using GameJam.Combat;
using GameJam.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace GameJam.EditorTools
{
    /// <summary>
    /// Conecta la vida del jugador y la animacion de muerte en el astronauta de j_MarsFusion.
    /// </summary>
    public static class PlayerHealthSetup
    {
        private const string AvatarPath = "Assets/02_Prefabs/Fusion/A_FusionAstronaut.prefab";

        [MenuItem("Tools/GameJam/Conectar Vida y Muerte del Jugador")]
        public static void Setup()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(AvatarPath);
            MarsFusionAvatar avatar = contents.GetComponent<MarsFusionAvatar>();
            if (avatar == null)
            {
                PrefabUtility.UnloadPrefabContents(contents);
                Debug.LogError("[PlayerHealthSetup] A_FusionAstronaut no tiene MarsFusionAvatar en la raiz.");
                return;
            }

            PlayerHealth health = contents.GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = contents.AddComponent<PlayerHealth>();
            }
            if (contents.GetComponent<PlayerDeathAnimation>() == null)
            {
                contents.AddComponent<PlayerDeathAnimation>();
            }

            SerializedObject so = new SerializedObject(avatar);
            so.FindProperty("healthSource").objectReferenceValue = health;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, AvatarPath);
            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerHealthSetup] Vida (100) y animacion de muerte conectadas en " + AvatarPath);
        }
    }
}
