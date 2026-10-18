using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Fusion;
using GameJam.Combat;
using GameJam.Multiplayer;
using GameJam.Mars;

public static class UnifiedPlayerGameplaySetup
{
    const string Folder="Assets/02_Prefabs/Mars/Immersive/MergedContacts";
    static T Asset<T>(string path) where T:UnityEngine.Object {return AssetDatabase.LoadAssetAtPath<T>(path);}
    static void Set(UnityEngine.Object o,string field,UnityEngine.Object value){var so=new SerializedObject(o);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    [MenuItem("Tools/GameJam/Unificar jugador, combate y pisadas")]
    public static void Apply()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Salir de Play antes de configurar.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/02_Prefabs/Mars/Immersive","MergedContacts");
        var source=Asset<GameObject>("Assets/02_Prefabs/Fusion/j_FusionAstronaut.prefab").GetComponent<MarsFusionFootsteps>();
        var bloodSource=Asset<GameObject>("Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Marte.prefab").GetComponent<EnemyBloodFx>();
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/02_Prefabs/Fusion"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            if(root.GetComponent<MarsFusionAvatar>()==null){PrefabUtility.UnloadPrefabContents(root);continue;}
            var animator=root.GetComponentInChildren<Animator>(true);
            var health=root.GetComponent<PlayerHealth>()??root.AddComponent<PlayerHealth>();Set(root.GetComponent<MarsFusionAvatar>(),"healthSource",health);
            var death=root.GetComponent<PlayerDeathAnimation>()??root.AddComponent<PlayerDeathAnimation>();Set(death,"body",animator.transform);
            var hurt=root.GetComponent<PlayerHurtReaction>()??root.AddComponent<PlayerHurtReaction>();Set(hurt,"body",animator.transform);
            Set(hurt,"hurtEffect",Asset<GameObject>("Assets/02_Prefabs/Gun/VFX/VFX_DanoJugador.prefab"));
            var blood=root.GetComponent<EnemyBloodFx>()??root.AddComponent<EnemyBloodFx>();if(bloodSource!=null)EditorUtility.CopySerialized(bloodSource,blood);
            var feet=root.GetComponent<MarsFusionFootsteps>()??root.AddComponent<MarsFusionFootsteps>();feet.decalPrefab=source.decalPrefab;
            feet.leftFoot=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="LeftFoot");feet.rightFoot=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="RightFoot");
            if(animator.GetComponent<MarsFootContactEvents>()==null)animator.gameObject.AddComponent<MarsFootContactEvents>();
            var controller=animator.runtimeAnimatorController;
            string controllerPath=Folder+"/"+root.name+".overrideController";
            if(AssetDatabase.GetAssetPath(controller)==controllerPath)controller=((AnimatorOverrideController)controller).runtimeAnimatorController;
            var overrides=Asset<AnimatorOverrideController>(controllerPath);
            if(overrides==null){overrides=new AnimatorOverrideController(controller);AssetDatabase.CreateAsset(overrides,controllerPath);}else overrides.runtimeAnimatorController=controller;
            var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();overrides.GetOverrides(pairs);
            for(int i=0;i<pairs.Count;i++)
            {
                var clip=pairs[i].Value??pairs[i].Key;string name=clip.name.ToLowerInvariant();
                if(!clip.isLooping || (!name.Contains("walk")&&!name.Contains("run")))continue;
                if(AnimationUtility.GetAnimationEvents(clip).Any(e=>e.functionName=="OnMarsFootContact"))continue;
                string clipPath=Folder+"/"+clip.name+"_UnifiedContacts.anim";var copy=Asset<AnimationClip>(clipPath);
                if(copy==null){copy=UnityEngine.Object.Instantiate(clip);AssetDatabase.CreateAsset(copy,clipPath);}
                var events=AnimationUtility.GetAnimationEvents(copy).Where(e=>e.functionName!="OnMarsFootContact").ToList();
                events.Add(new AnimationEvent{functionName="OnMarsFootContact",time=clip.length*.38f,intParameter=0});
                events.Add(new AnimationEvent{functionName="OnMarsFootContact",time=clip.length*.88f,intParameter=1});
                AnimationUtility.SetAnimationEvents(copy,events.OrderBy(e=>e.time).ToArray());EditorUtility.SetDirty(copy);
                pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(pairs[i].Key,copy);
            }
            overrides.ApplyOverrides(pairs);EditorUtility.SetDirty(overrides);animator.runtimeAnimatorController=overrides;
            var gun=root.GetComponentInChildren<Gun>(true);
            if(gun!=null)
            {
                Set(gun,"muzzleFlashEffect",Asset<GameObject>("Assets/02_Prefabs/Gun/VFX/VFX_DisparoEnergia.prefab"));
                Set(gun,"boltEffect",Asset<GameObject>("Assets/02_Prefabs/Gun/VFX/VFX_BalaEnergia.prefab").GetComponent<EnergyBolt>());
                Set(gun,"impactEffect",Asset<GameObject>("Assets/02_Prefabs/Gun/VFX/VFX_ImpactoEnergia.prefab"));
                var so=new SerializedObject(gun);so.FindProperty("damage").intValue=20;so.FindProperty("muzzleFlashDuration").floatValue=1.2f;so.FindProperty("impactEffectDuration").floatValue=2.5f;so.ApplyModifiedPropertiesWithoutUndo();
            }
            root.GetComponent<NetworkObject>().NetworkedBehaviours=root.GetComponentsInChildren<NetworkBehaviour>(true);
            PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
        }
        var enemy=PrefabUtility.LoadPrefabContents("Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Fusion.prefab");var enemyBlood=enemy.GetComponent<EnemyBloodFx>()??enemy.AddComponent<EnemyBloodFx>();EditorUtility.CopySerialized(bloodSource,enemyBlood);PrefabUtility.SaveAsPrefabAsset(enemy,"Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Fusion.prefab");PrefabUtility.UnloadPrefabContents(enemy);
        foreach(string path in new[]{"Assets/00_Scenes/j_MarsFusion.unity","Assets/00_Scenes/b_NeptuneExploration.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            bool mars=path.Contains("Mars");
            foreach(var terrain in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Terrain>(true)))
            {
                var surface=terrain.GetComponent<MarsFootstepSurface>()??terrain.gameObject.AddComponent<MarsFootstepSurface>();
                surface.profile=Asset<MarsFootstepProfile>("Assets/02_Prefabs/Mars/Immersive/"+(mars?"j_SandFootsteps.asset":"j_SnowFootsteps.asset"));
                surface.paintedLayer=mars?1:-1;EditorUtility.SetDirty(surface);
            }
            // Use the same equipped gameplay avatar when starting Neptune directly or from the lobby.
            foreach(var session in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MarsFusionSession>(true)))
            {session.playerPrefab=Asset<GameObject>("Assets/02_Prefabs/Fusion/A_FusionAstronaut.prefab").GetComponent<NetworkObject>();EditorUtility.SetDirty(session);}
            EditorSceneManager.SaveScene(scene);if(opened)EditorSceneManager.CloseScene(scene,true);
        }
        AssetDatabase.SaveAssets();Debug.Log("[Unified player] Combat, interactions, health and contact footsteps configured.");
    }
}
