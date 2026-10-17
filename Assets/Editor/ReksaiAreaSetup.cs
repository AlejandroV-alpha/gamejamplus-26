using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class ReksaiAreaSetup
{
 const string Root="Assets/Enemies/Reksai_Marte";
 const string Report="C:/Users/samir/Documents/Codex/2026-10-16/https-youtu-be-t3zw3owrhek-si-sbs6zfyrzfibfcbi/work/reksai_setup_report.txt";
 
 [MenuItem("Tools/GameJam/Configurar Reksai en Worm Basin")]
 public static void Run()
 {
  if(SessionState.GetBool("ReksaiAreaSetupDone2",false))return;
  if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  try
  {
   var scene=SceneManager.GetActiveScene();
   var basin=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x=>x.name=="j_WormBasin");
   if(basin==null)throw new Exception("No se encontro j_WormBasin en la escena activa.");
   if(GameObject.Find("Reksai_Spawn_WormBasin")!=null){SessionState.SetBool("ReksaiAreaSetupDone2",true);return;}
   var path=Root+"/Models/Reksai_Marte_Animado.fbx";
   var importer=(ModelImporter)AssetImporter.GetAtPath(path);
   importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
   var clips=importer.defaultClipAnimations;
   foreach(var clip in clips){clip.loopTime=clip.name.Contains("idle1base")||clip.name.Contains("runhaste");}
   importer.clipAnimations=clips;importer.SaveAndReimport();
   var mat=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Reksai_Marte.mat") ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name="Reksai_Marte";
   var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Reksai_Marte_BaseColor.png");
   mat.SetTexture("_BaseMap",tex);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.15f);
   if(!AssetDatabase.Contains(mat))AssetDatabase.CreateAsset(mat,Root+"/Materials/Reksai_Marte.mat");
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Animations/Reksai_Marte.controller") ?? AnimatorController.CreateAnimatorControllerAtPath(Root+"/Animations/Reksai_Marte.controller");
   var anims=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(a=>!a.name.StartsWith("__preview__")).ToArray();
   if(anims.Length!=8)throw new Exception("Se esperaban 8 clips, hay "+anims.Length);
   var sm=controller.layers[0].stateMachine;
   foreach(var clip in anims){if(sm.states.Any(s=>s.state.motion==clip))continue;var state=sm.AddState(clip.name.Split('|').Last());state.motion=clip;if(clip.name.Contains("idle1base"))sm.defaultState=state;}
   var leftover=GameObject.Find("Reksai_Marte");if(leftover!=null && leftover.transform.parent==null)Undo.DestroyObjectImmediate(leftover);
   var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
   var enemy=new GameObject("Reksai_Marte");model.transform.SetParent(enemy.transform,false);
   foreach(var r in model.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();
   var animator=model.GetComponentInChildren<Animator>() ?? model.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
   float scale=2.8f/Mathf.Max(.01f,bounds.size.y);model.transform.localScale*=scale;
   bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
   model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
   var prefab=PrefabUtility.SaveAsPrefabAsset(enemy,Root+"/Prefabs/Reksai_Marte.prefab");UnityEngine.Object.DestroyImmediate(enemy);
   var markers=basin.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Survey")).ToArray();
   var point=markers.Length>0?markers.Aggregate(Vector3.zero,(sum,t)=>sum+t.position)/markers.Length:basin.position;
   var hits=Physics.RaycastAll(point+Vector3.up*1000f,Vector3.down,2000f,~0,QueryTriggerInteraction.Ignore);
   var ground=hits.Where(h=>h.collider is TerrainCollider).OrderBy(h=>h.distance).FirstOrDefault();
   if(ground.collider!=null)point.y=ground.point.y;
   else {var terrain=Terrain.activeTerrains.FirstOrDefault(t=>point.x>=t.transform.position.x&&point.z>=t.transform.position.z&&point.x<=t.transform.position.x+t.terrainData.size.x&&point.z<=t.transform.position.z+t.terrainData.size.z);if(terrain!=null)point.y=terrain.SampleHeight(point)+terrain.transform.position.y;}
   var spawn=new GameObject("Reksai_Spawn_WormBasin");Undo.RegisterCreatedObjectUndo(spawn,"Agregar spawn de Reksai");spawn.transform.position=point;
   var component=spawn.AddComponent<ReksaiAreaSpawner>();var preview=(GameObject)PrefabUtility.InstantiatePrefab(prefab);preview.name="Reksai_VistaPrevia";preview.tag="EditorOnly";preview.transform.SetParent(spawn.transform,false);
   var so=new SerializedObject(component);so.FindProperty("enemyPrefab").objectReferenceValue=prefab;so.FindProperty("editorPreview").objectReferenceValue=preview;so.ApplyModifiedPropertiesWithoutUndo();
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=spawn;
   if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(point+Vector3.up*1.4f,Vector3.one*10f),false);
   File.WriteAllText(Report,"OK\nScene: "+scene.path+"\nSpawn: "+point+"\nMarkers: "+markers.Length+"\nClips: "+anims.Length+"\nHeight: 2.8m\nMaterial: URP Lit\n");
   SessionState.SetBool("ReksaiAreaSetupDone2",true);
  }
  catch(Exception e){File.WriteAllText(Report,"ERROR\n"+e);Debug.LogException(e);SessionState.SetBool("ReksaiAreaSetupDone2",true);}
 }
}


