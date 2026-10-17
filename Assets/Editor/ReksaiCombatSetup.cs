using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.SceneManagement;

public static class ReksaiCombatSetup
{
 const string Root="Assets/Enemies/Reksai_Marte";
 const string Report="C:/Users/samir/Documents/Codex/2026-10-16/https-youtu-be-t3zw3owrhek-si-sbs6zfyrzfibfcbi/work/reksai_combat_report.txt";
 
 [MenuItem("Tools/GameJam/Configurar combate Reksai")]
 public static void Setup()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool("ReksaiCombatSetupV5",false))return;
  try
  {
   var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
   var camp=all.FirstOrDefault(t=>t.name=="b_brokemantenna2");if(camp==null)throw new Exception("No se encontro b_brokemantenna2");
   var path=Root+"/Prefabs/Reksai_Marte.prefab";var obj=PrefabUtility.LoadPrefabContents(path);
   if(obj.GetComponent<NetworkObject>()==null)obj.AddComponent<NetworkObject>();
   if(obj.GetComponent<NetworkTransform>()==null)obj.AddComponent<NetworkTransform>();
   if(obj.GetComponent<ReksaiEnemy>()==null)obj.AddComponent<ReksaiEnemy>();
   var collider=obj.GetComponent<CapsuleCollider>();if(collider==null)collider=obj.AddComponent<CapsuleCollider>();collider.height=2.5f;collider.radius=.8f;collider.center=new Vector3(0,1.25f,0);
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Animations/Reksai_Marte.controller");
   var machine=controller.layers[0].stateMachine;
   foreach(var s in machine.states.Where(s=>s.state.name.Contains("dance")).ToArray())machine.RemoveState(s.state);
   var animator=obj.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=controller;
   PrefabUtility.SaveAsPrefabAsset(obj,path);PrefabUtility.UnloadPrefabContents(obj);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
   // Existing scene preview was created before networking was added. Replace only the generated preview.
   foreach(var spawner in all.Where(t=>t!=null).Select(t=>t.GetComponent<ReksaiAreaSpawner>()).Where(c=>c!=null).ToArray())
   {
    var so=new SerializedObject(spawner);var previous=so.FindProperty("editorPreview").objectReferenceValue as GameObject;
    if(previous!=null)UnityEngine.Object.DestroyImmediate(previous);
    var preview=(GameObject)PrefabUtility.InstantiatePrefab(prefab);preview.name="Reksai_VistaPrevia";preview.tag="EditorOnly";preview.transform.SetParent(spawner.transform,false);
    so.FindProperty("enemyPrefab").objectReferenceValue=prefab;so.FindProperty("editorPreview").objectReferenceValue=preview;so.ApplyModifiedPropertiesWithoutUndo();
   }
   var rs=camp.GetComponentsInChildren<Renderer>(true);var bounds=rs.Length>0?rs[0].bounds:new Bounds(camp.position,Vector3.one*20);
   foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);
   var center=bounds.center;float radius=Mathf.Clamp(Mathf.Max(bounds.extents.x,bounds.extents.z)+5,14,32);
   var group=GameObject.Find("Reksai_Spawns_AbandonedCamp");if(group==null)group=GameObject.Find("Reksai_Spawns_CentralShip");if(group==null)group=GameObject.Find("Reksai_Spawns_Ruinas");if(group==null)group=new GameObject("Reksai_Spawns_Ruinas");group.name="Reksai_Spawns_Ruinas";Undo.RegisterCreatedObjectUndo(group,"Agregar Reksai al campamento");
   for(int i=0;i<3;i++)
   {
    
    float a=(30+120*i)*Mathf.Deg2Rad;var pos=center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
    var terrain=Terrain.activeTerrains.FirstOrDefault(t=>pos.x>=t.transform.position.x&&pos.z>=t.transform.position.z&&pos.x<=t.transform.position.x+t.terrainData.size.x&&pos.z<=t.transform.position.z+t.terrainData.size.z);
    if(terrain!=null)pos.y=terrain.SampleHeight(pos)+terrain.transform.position.y;
    var existing=group.transform.Find("Reksai_Spawn_"+(i+1));if(existing!=null){existing.position=pos;existing.rotation=Quaternion.LookRotation(new Vector3(center.x-pos.x,0,center.z-pos.z));continue;}
    var go=new GameObject("Reksai_Spawn_"+(i+1));go.transform.SetParent(group.transform);go.transform.position=pos;go.transform.rotation=Quaternion.LookRotation(new Vector3(center.x-pos.x,0,center.z-pos.z));
    var spawn=go.AddComponent<ReksaiAreaSpawner>();var preview=(GameObject)PrefabUtility.InstantiatePrefab(prefab);preview.name="Reksai_VistaPrevia";preview.tag="EditorOnly";preview.transform.SetParent(go.transform,false);
    var so=new SerializedObject(spawn);so.FindProperty("enemyPrefab").objectReferenceValue=prefab;so.FindProperty("editorPreview").objectReferenceValue=preview;so.ApplyModifiedPropertiesWithoutUndo();
   }
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=group;
   SceneView.lastActiveSceneView?.Frame(new Bounds(center,Vector3.one*(radius*2.5f)),false);
   File.WriteAllText(Report,"OK\nScene: "+scene.path+"\nCamp: "+camp.name+"\nCenter: "+center+"\nRadius: "+radius+"\nSpawns: "+group.transform.childCount+"\nPrefab networked: "+(prefab.GetComponent<NetworkObject>()!=null)+"\nController states: "+machine.states.Length);
   SessionState.SetBool("ReksaiCombatSetupV5",true);
  }
  catch(Exception e){File.WriteAllText(Report,"ERROR\n"+e);Debug.LogException(e);SessionState.SetBool("ReksaiCombatSetupV5",true);}
 }
}





