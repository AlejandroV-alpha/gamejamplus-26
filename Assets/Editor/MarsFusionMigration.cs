using System.Linq;
using Fusion;
using Fusion.Addons.SimpleKCC;
using GameJam.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Player = Starter.ThirdPersonCharacter.Player;
using PlayerInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.Editor
{
    public static class MarsFusionMigration
    {
        public const string PlayerPath = "Assets/02_Prefabs/Fusion/j_FusionAstronaut.prefab";
        public const string ScenePath = "Assets/00_Scenes/j_MarsFusion.unity";

        [MenuItem("Tools/GameJam/Create Mars Fusion Integration")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new System.InvalidOperationException("Save your scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new System.InvalidOperationException("Fusion scene already exists; edit the existing scene.");
            if (!AssetDatabase.IsValidFolder("Assets/02_Prefabs/Fusion")) AssetDatabase.CreateFolder("Assets/02_Prefabs", "Fusion");
            var legacy = PrefabUtility.LoadPrefabContents("Assets/02_Prefabs/Tablet/j_TabletPlayer.prefab");
            var root = new GameObject("j_FusionAstronaut");
            try
            {
                root.layer = 2; // IgnoreRaycast: the camera's environment casts exclude the player itself.
                root.AddComponent<NetworkObject>();
                var kcc = root.AddComponent<SimpleKCC>();
                var serializedKcc = new SerializedObject(kcc);
                serializedKcc.FindProperty("_settings.Radius").floatValue = .28f;
                serializedKcc.FindProperty("_settings.Height").floatValue = 2.05f;
                serializedKcc.FindProperty("_settings.ColliderLayer").intValue = 2;
                serializedKcc.FindProperty("_settings.CollisionLayerMask").intValue = 1;
                serializedKcc.ApplyModifiedPropertiesWithoutUndo();
                var originalAnimator = legacy.GetComponentInChildren<Animator>();
                var visual = UnityEngine.Object.Instantiate(originalAnimator.gameObject, root.transform);
                visual.name = "AstronautVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = originalAnimator.transform.localRotation;
                visual.transform.localScale = originalAnimator.transform.lossyScale;
                foreach (var tr in visual.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 2;
                var animator = visual.GetComponent<Animator>();
                animator.applyRootMotion = false;
                var playerInput = root.AddComponent<PlayerInput>();
                var player = root.AddComponent<Player>();
                player.KCC = kcc; player.PlayerInput = playerInput; player.Animator = animator;
                player.WalkSpeed = 3; player.SprintSpeed = 6; player.JumpImpulse = 8;
                player.UpGravity = -25; player.DownGravity = -40;
                var pivot = new GameObject("CameraPivot").transform;
                pivot.SetParent(root.transform, false); pivot.localPosition = new Vector3(0, 1.65f, 0);
                var handle = new GameObject("CameraHandle").transform;
                handle.SetParent(pivot, false); handle.localPosition = new Vector3(0, .25f, -4.5f);
                player.CameraPivot = pivot; player.CameraHandle = handle;
                player.FootstepAudioClips = new AudioClip[0];
                var avatar = root.AddComponent<MarsFusionAvatar>();
                avatar.tablet = AssetDatabase.LoadAssetAtPath<GameJam.Tablet.TabletItemDefinition>("Assets/02_Prefabs/Tablet/j_MissionTablet.asset");
                avatar.handSocket = animator.GetBoneTransform(HumanBodyBones.RightHand);
                avatar.eyes = visual.GetComponentsInChildren<SpriteRenderer>().First(x => x.name == "Eyes");
                avatar.mouth = visual.GetComponentsInChildren<SpriteRenderer>().First(x => x.name == "Mouth");
                var originalFace = new SerializedObject(legacy.GetComponent<GameJam.Characters.PlayerVisorFace>());
                var face = new SerializedObject(avatar);
                foreach (var name in new[] { "neutralEyes", "blinkEyes", "hurtEyes", "deadEyes", "smileMouth", "smallMouth", "openMouth", "hurtMouth", "deadMouth" })
                    face.FindProperty(name).objectReferenceValue = originalFace.FindProperty(name).objectReferenceValue;
                face.ApplyModifiedPropertiesWithoutUndo();
                new Fusion.Editor.NetworkObjectBakerEditTime().Bake(root);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(legacy); UnityEngine.Object.DestroyImmediate(root); }

            AssetDatabase.CopyAsset("Assets/00_Scenes/j_TabletPrototype.unity", ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "NetworkManager" || go.name == "GameManager" || go.name == "TestPanel" || go.name == "Camera")
                    UnityEngine.Object.DestroyImmediate(go);
            var camera = Camera.main;
            if (camera != null)
            {
                var brain = camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
                if (brain != null) UnityEngine.Object.DestroyImmediate(brain);
            }
            var manager = new GameObject("j_PhotonFusionSession").AddComponent<MarsFusionSession>();
            manager.playerPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(PlayerPath);
            manager.runnerPrefab = AssetDatabase.LoadAssetAtPath<NetworkRunner>("Assets/PhotonStarter/j_FusionRunner.prefab");
            var spawn = new GameObject("j_FusionSpawn").transform;
            spawn.position = new Vector3(-14.29f, 12.2f, -71.43f);
            manager.spawnPoint = spawn;
            var spawner = new GameObject("j_FusionPlayerSpawner");
            spawner.AddComponent<NetworkObject>();
            spawner.AddComponent<MarsFusionSpawner>();
            new Fusion.Editor.NetworkObjectBakerEditTime().Bake(spawner);
            var list = EditorBuildSettings.scenes.ToList();
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = manager.gameObject;
        }
    }
}
