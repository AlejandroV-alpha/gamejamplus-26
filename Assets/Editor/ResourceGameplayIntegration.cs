using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Fusion;
using GameJam.Inventory;
using GameJam.Collectibles;
using GameJam.Crafting;
using GameJam.Ship;
using GameJam.Multiplayer;

public static class ResourceGameplayIntegration
{
    const string Pickups="Assets/02_Prefabs/Interactables/Materials/";
    static readonly string[] MaterialNames={"Metal","Circuit","IceCrystal","EnergyCrystal","RedMineral","DamagedPropulsion","DamagedIntake","DamagedPanel"};
    static MaterialData Data(string name) {return AssetDatabase.LoadAssetAtPath<MaterialData>("Assets/_SO/Materials/"+name+".asset");}
    static void Set(UnityEngine.Object o,string key,UnityEngine.Object value) {var s=new SerializedObject(o);s.FindProperty(key).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void Bake(GameObject o) {o.GetComponent<NetworkObject>().NetworkedBehaviours=o.GetComponentsInChildren<NetworkBehaviour>(true);}
    static Bounds BoundsOf(GameObject o) {var rs=o.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static void Visual(GameObject root,GameObject source,float maxSize)
    {
        var pickup=root.GetComponent<MaterialPickup>();var so=new SerializedObject(pickup);var old=so.FindProperty("visual").objectReferenceValue as GameObject;
        if(old!=null && old!=root) UnityEngine.Object.DestroyImmediate(old);
        var holder=new GameObject("Visual");holder.transform.SetParent(root.transform,false);
        var model=UnityEngine.Object.Instantiate(source,holder.transform);model.name=source.name;
        if(PrefabUtility.IsPartOfPrefabInstance(model))PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(var c in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        model.transform.localPosition=Vector3.zero;
        var b=BoundsOf(holder);float extent=Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z));
        holder.transform.localScale*=maxSize/Mathf.Max(.01f,extent);
        b=BoundsOf(holder);holder.transform.position-=new Vector3(b.center.x-root.transform.position.x,b.min.y-root.transform.position.y,b.center.z-root.transform.position.z);
        b=BoundsOf(holder);var box=root.GetComponent<BoxCollider>();box.center=root.transform.InverseTransformPoint(b.center);box.size=b.size;box.isTrigger=false;
        Set(pickup,"visual",holder);
    }
    static void SaveVisual(string material,string model,float size)
    {
        string path=Pickups+material+"Pickup.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        Visual(root,AssetDatabase.LoadAssetAtPath<GameObject>(model),size);Bake(root);PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
    }
    public static string PrepareAssets()
    {
        SaveVisual("Metal","Assets/02_Prefabs/Mars/Resources_mars/Metal_Model.prefab",1.3f);
        SaveVisual("Circuit","Assets/02_Prefabs/Mars/Resources_mars/circuit_Model.prefab",1.1f);
        SaveVisual("RedMineral","Assets/02_Prefabs/Mars/Resources_mars/crimsom_red_Model.prefab",1.6f);
        SaveVisual("IceCrystal","Assets/02_Prefabs/Neptune/resources_neptune/azure_crystal_model.prefab",1.6f);
        SaveVisual("EnergyCrystal","Assets/02_Prefabs/Neptune/resources_neptune/crystal_violet.prefab",1.6f);
        var ship=GameObject.Find("j_CentralShip");
        string[][] groups={new[]{"Anillo_turbina_-1_-2.05","Collar_turbina_-1_-2.05_0","Collar_turbina_-1_-2.05_1","Collar_turbina_-1_-2.05_2","Luz_turbina_-1_-2.05"},new[]{"Rejilla_toma_-1"},new[]{"Armadura_techo_-1_0","Armadura_techo_-1_1","Armadura_techo_-1_2","Armadura_techo_-1_3"}};
        // Locate intake pieces by actual imported names rather than inventing new geometry.
        groups[1]=ship.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Rejilla")&&t.name.Contains("-1")).Select(t=>t.name).ToArray();
        if(groups[1].Length==0)groups[1]=new[]{"Interior_toma_-1","Marco_toma_abajo_-1","Marco_toma_arriba_-1","Marco_toma_vertical_-1_-1","Marco_toma_vertical_-1_1"};
        string[] labels={"Turbina danada","Toma de aire danada","Panel de casco danado"};
        for(int i=0;i<3;i++)
        {
            string name=MaterialNames[i+5];var data=Data(name);
            if(data==null){data=ScriptableObject.CreateInstance<MaterialData>();AssetDatabase.CreateAsset(data,"Assets/_SO/Materials/"+name+".asset");}
            data.id="damaged_"+name.Substring(7).ToLowerInvariant();data.materialName=labels[i];data.description="Pieza recuperada de la nave. Necesaria junto con minerales para reparar.";
            var source=new GameObject(name+"_ShipMesh");
            foreach(string part in groups[i]){var original=ship.GetComponentsInChildren<Transform>(true).First(t=>t.name==part);var copy=UnityEngine.Object.Instantiate(original.gameObject,source.transform);copy.transform.rotation=original.rotation;copy.transform.localScale=original.lossyScale;copy.transform.position=original.position;}
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Pickups+"MetalPickup.prefab"));root.name=name+"Pickup";
            if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            Visual(root,source,3.4f);Set(root.GetComponent<MaterialPickup>(),"material",data);
            var smoke=root.GetComponent<DamagedPartSmoke>()??root.AddComponent<DamagedPartSmoke>();smoke.smokeMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/05_Materials/Mars/b_efecto/b_efectoPolvoMarte.mat");Bake(root);
            data.prefab=PrefabUtility.SaveAsPrefabAsset(root,Pickups+name+"Pickup.prefab");EditorUtility.SetDirty(data);
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(source);
        }
        foreach(string planet in new[]{"Mars","Neptune"})
        {
            string[] recipes=planet=="Mars"?new[]{"Propulsor","FiltroDeAire","Radar"}:new[]{"Motor","Navegador","Blindaje"};
            int[] partIndices=planet=="Mars"?new[]{5,6,7}:new[]{5,6,7};
            for(int i=0;i<3;i++){var r=AssetDatabase.LoadAssetAtPath<RecipeData>("Assets/_SO/Recipes/"+planet+"/"+recipes[i]+".asset");r.ingredients.RemoveAll(x=>x.material!=null&&x.material.id.StartsWith("damaged_"));r.ingredients.Add(new RecipeIngredient{material=Data(MaterialNames[partIndices[i]]),amount=1});EditorUtility.SetDirty(r);}
        }
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/02_Prefabs/Fusion"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root=PrefabUtility.LoadPrefabContents(path);var inv=root.GetComponent<Inventory>();
            if(inv!=null){var so=new SerializedObject(inv);var a=so.FindProperty("materials");a.arraySize=8;for(int i=0;i<8;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=Data(MaterialNames[i]);so.ApplyModifiedPropertiesWithoutUndo();
                if(root.GetComponent<GameJam.Gameplay.PlayerCollector>()==null)root.AddComponent<GameJam.Gameplay.PlayerCollector>();
                if(root.GetComponent<PlayerCrafter>()==null)root.AddComponent<PlayerCrafter>();if(root.GetComponent<PlayerShipRepair>()==null)root.AddComponent<PlayerShipRepair>();if(root.GetComponent<InventoryDeathRecovery>()==null)root.AddComponent<InventoryDeathRecovery>();Bake(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
            PrefabUtility.UnloadPrefabContents(root);
        }
        // The guide uses a separate catalog today; keep its text aligned with actual recipe costs.
        var catalog=AssetDatabase.LoadAssetAtPath<GameJam.Tablet.TabletRecipeCatalog>("Assets/02_Prefabs/Tablet/j_PlanetRecipeCatalog.asset");
        foreach(var planet in catalog.planets)if(planet.name=="Marte"||planet.name=="Neptuno")for(int i=0;i<3;i++){var list=planet.recipes[i].ingredients.Where(x=>!x.mineral.EndsWith("danada")&&!x.mineral.EndsWith("danado")).ToList();list.Add(new GameJam.Tablet.TabletRecipeCatalog.Ingredient{mineral=labels[i],amount=1});planet.recipes[i].ingredients=list.ToArray();}EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();return "Pickup visuals, recovered ship parts, recipes and inventories configured";
    }
    static float Ground(Terrain terrain,Vector3 position) {return terrain.SampleHeight(position)+terrain.transform.position.y;}
    static GameObject Spawn(Scene scene,GameObject prefab,Vector3 position,Transform parent=null)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);if(parent!=null)go.transform.SetParent(parent,true);go.transform.position=position;return go;
    }
    public static string ConfigureScenes()
    {
        var mars=SceneManager.GetSceneByPath("Assets/00_Scenes/j_MarsFusion.unity");
        var original=mars.GetRootGameObjects().First(g=>g.name=="j_CentralShip");
        int converted=0;
        foreach(string path in new[]{"Assets/00_Scenes/j_MarsFusion.unity","Assets/00_Scenes/b_NeptuneExploration.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path);bool isMars=path.Contains("Mars");
            var terrain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true)).First();
            var zone=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ExplorationZone>(true)).First();
            var resources=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Gameplay Resources");
            if(resources==null){resources=new GameObject("Gameplay Resources");SceneManager.MoveGameObjectToScene(resources,scene);}
            var mapping=new Dictionary<string,string>{{"Metal_Model","Metal"},{"circuit_Model","Circuit"},{"crimsom_red_Model","RedMineral"},{"azure_crystal_model","IceCrystal"},{"crystal_violet","EnergyCrystal"}};
            var models=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.gameObject.activeInHierarchy && t.GetComponentInParent<MaterialPickup>()==null && PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject)==t.gameObject).ToArray();
            foreach(var model in models)
            {
                string asset=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.gameObject);string key=System.IO.Path.GetFileNameWithoutExtension(asset);
                if(!mapping.ContainsKey(key))continue;
                var position=model.position;position.y=Ground(terrain,position)+.03f;
                model.gameObject.SetActive(false);EditorUtility.SetDirty(model.gameObject);
                if(!zone.Contains(position))continue;
                var item=Spawn(scene,Data(mapping[key]).prefab,position,resources.transform);item.transform.rotation=Quaternion.Euler(0,model.eulerAngles.y,0);converted++;
            }
            // Replace the independent scene grayboxes too, retaining their identity and scripts.
            foreach(var pickup in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MaterialPickup>(true)).Where(p=>p.transform.parent!=resources.transform).ToArray())
            {
                var so=new SerializedObject(pickup);var data=so.FindProperty("material").objectReferenceValue as MaterialData;
                string key=data!=null?data.name:"";string model=mapping.FirstOrDefault(kv=>kv.Value==key).Key;
                if(model==null)continue;
                string folder=(key=="IceCrystal"||key=="EnergyCrystal")?"Assets/02_Prefabs/Neptune/resources_neptune/":"Assets/02_Prefabs/Mars/Resources_mars/";
                pickup.transform.localScale=Vector3.one;Visual(pickup.gameObject,AssetDatabase.LoadAssetAtPath<GameObject>(folder+model+".prefab"),key=="Circuit"?1.1f:1.6f);
            }
            var ship=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="j_CentralShip"||g.name=="Neptune Expedition Ship");
            if(ship==null)
            {
                ship=UnityEngine.Object.Instantiate(original);ship.name="Neptune Expedition Ship";SceneManager.MoveGameObjectToScene(ship,scene);
                var p=new Vector3(40,0,35);ship.transform.position=p;var b=BoundsOf(ship);p.y=Ground(terrain,p)-b.min.y+.1f;ship.transform.position=p;
            }
            var table=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CraftingTable>(true)).FirstOrDefault();
            if(table==null)table=Spawn(scene,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Interactables/Crafting/CraftingTable.prefab"),Vector3.zero).GetComponent<CraftingTable>();
            var ts=new SerializedObject(table);ts.FindProperty("planet").intValue=isMars?2:0;var rec=ts.FindProperty("recipes");rec.arraySize=3;
            string[] names=isMars?new[]{"Propulsor","FiltroDeAire","Radar"}:new[]{"Motor","Navegador","Blindaje"};
            for(int i=0;i<3;i++)rec.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<RecipeData>("Assets/_SO/Recipes/"+(isMars?"Mars":"Neptune")+"/"+names[i]+".asset");ts.ApplyModifiedPropertiesWithoutUndo();
            Vector3 tablePosition=ship.transform.position+new Vector3(-10,0,-22);tablePosition.y=Ground(terrain,tablePosition)+.1f;table.transform.position=tablePosition;
            var station=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ShipRepairStation>(true)).FirstOrDefault();
            if(station==null)station=Spawn(scene,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Prefabs/Interactables/Ship/ShipRepairStation.prefab"),Vector3.zero).GetComponent<ShipRepairStation>();
            Set(station,"craftingTable",table);var stationPosition=ship.transform.position+new Vector3(0,0,-19);stationPosition.y=Ground(terrain,stationPosition)+.1f;station.transform.position=stationPosition;
            var presentation=ship.GetComponent<ShipRepairPresentation>()??ship.AddComponent<ShipRepairPresentation>();presentation.station=station;
            var partNames=new HashSet<string>();for(int i=5;i<8;i++)foreach(var t in Data(MaterialNames[i]).prefab.GetComponentsInChildren<Transform>(true))partNames.Add(t.name.Replace("(Clone)",""));
            presentation.missingParts=ship.GetComponentsInChildren<Transform>(true).Where(t=>partNames.Contains(t.name)).Select(t=>t.gameObject).ToArray();foreach(var part in presentation.missingParts)part.SetActive(false);
            if(!scene.GetRootGameObjects().Any(g=>g.name=="Lost Ship Parts"))
            {
                var group=new GameObject("Lost Ship Parts");SceneManager.MoveGameObjectToScene(group,scene);
                Vector3[] positions=isMars?new[]{new Vector3(-170,0,-165),new Vector3(105,0,-210),new Vector3(-65,0,165)}:new[]{new Vector3(-80,0,180),new Vector3(255,0,330),new Vector3(70,0,570)};
                for(int i=0;i<3;i++){var p=positions[i];p.y=Ground(terrain,p)+.05f;Spawn(scene,Data(MaterialNames[i+5]).prefab,p,group.transform);}
            }
            // Neptune's original distribution had crystals only; salvage supplies the other recipe inputs.
            if(!isMars && !scene.GetRootGameObjects().Any(g=>g.name=="Neptune Salvage Supplies"))
            {
                var group=new GameObject("Neptune Salvage Supplies");SceneManager.MoveGameObjectToScene(group,scene);
                for(int i=0;i<24;i++){Vector3 p=new Vector3(-90+(i%4)*110,0,70+(i/4)*110);p.y=Ground(terrain,p)+.03f;Spawn(scene,Data(i%2==0?"Metal":"Circuit").prefab,p,group.transform);}
            }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();return "Converted distributed minerals: "+converted+". Both maps have ship-part recovery and repair presentation.";
    }

}
