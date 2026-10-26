using System;
using System.IO;
using System.Linq;
using Fusion;
using GameJam.Combat;
using GameJam.Collectibles;
using GameJam.Inventory;
using GameJam.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace GameJam.EditorTools
{
    [InitializeOnLoad]
    public static class EquipmentLoadoutSetup
    {
        const string Folder = "Assets/02_Prefabs/Interactables/Equipment";
        const string Data = "Assets/_SO/Equipment";
        const string VisualPath = "Assets/Resources/Equipment/WalkieTalkieVisual.prefab";
        const string Request = ".local/Equipment/setup.request";
        static EquipmentLoadoutSetup() => EditorApplication.delayCall += Requested;
        static void Requested()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += Requested; return; }
            File.Delete(Request);
            try { Configure(); File.WriteAllText(".local/Equipment/setup.txt","PASS"); }
            catch(Exception ex) { File.WriteAllText(".local/Equipment/setup.txt",ex.ToString()); Debug.LogException(ex); }
        }
        static void FolderAt(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent=Path.GetDirectoryName(path).Replace('\\','/'); FolderAt(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        static T Asset<T>(string path) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
        static void Set(UnityEngine.Object component,string property,UnityEngine.Object value)
        {
            var so=new SerializedObject(component);so.FindProperty(property).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();
        }
        [MenuItem("Tools/GameJam/Equipment/Configure weapon and radio loadout")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before editing prefabs.");
            FolderAt(Folder); FolderAt(Data); FolderAt("Assets/Resources/Equipment");
            var radio = BuildRadio();
            var weapon = Asset<GameObject>("Assets/02_Prefabs/K-39.prefab");
            var gunItem = Item("PrimaryWeapon","primary_weapon","Arma principal",CarriedItemKind.PrimaryWeapon);
            var radioItem = Item("WalkieTalkie","walkie_talkie","Walkie-talkie",CarriedItemKind.Radio);
            gunItem.description="Equipa para apuntar y disparar. Ocupa un espacio.";
            radioItem.description="Lleva para recibir radio; equipa y abre el canal para transmitir.";
            gunItem.icon=Icon(BuildWeaponIcon(),"PrimaryWeapon"); radioItem.icon=Icon(radio,"WalkieTalkie");
            if(gunItem.icon==null || radioItem.icon==null) throw new InvalidOperationException("Equipment sprites were not imported.");
            gunItem.prefab=Pickup(gunItem,weapon,"PrimaryWeaponPickup");
            radioItem.prefab=Pickup(radioItem,radio,"WalkieTalkiePickup");
            EditorUtility.SetDirty(gunItem);EditorUtility.SetDirty(radioItem);
            int count=0;
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/02_Prefabs/Fusion"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var inventory=root.GetComponent<GameJam.Inventory.Inventory>();
                    if(inventory==null || root.GetComponent<MarsFusionAvatar>()==null)continue;
                    var so=new SerializedObject(inventory);var registry=so.FindProperty("materials");
                    registry.arraySize=10;registry.GetArrayElementAtIndex(8).objectReferenceValue=gunItem;registry.GetArrayElementAtIndex(9).objectReferenceValue=radioItem;
                    var initial=so.FindProperty("startingItems");initial.arraySize=2;initial.GetArrayElementAtIndex(0).objectReferenceValue=gunItem;initial.GetArrayElementAtIndex(1).objectReferenceValue=radioItem;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    var equipment=root.GetComponent<PlayerEquipment>()??root.AddComponent<PlayerEquipment>();
                    Set(equipment,"radioEquippedSound",Asset<AudioClip>("Assets/Resources/Audio/Radio/Equip.wav"));
                    Set(equipment,"channelOpenSound",Asset<AudioClip>("Assets/Resources/Audio/Radio/ChannelOpen.wav"));
                    Set(equipment,"channelCloseSound",Asset<AudioClip>("Assets/Resources/Audio/Radio/ChannelClose.wav"));
                    var presentation=root.GetComponent<EquipmentPresentation>();
                    bool newPresentation=presentation==null;
                    if(newPresentation)presentation=root.AddComponent<EquipmentPresentation>();
                    Set(presentation,"radioVisualPrefab",radio);
                    // Existing grips may have been calibrated in the Inspector; keep them on rebuild.
                    if(newPresentation)
                    {
                        var view=new SerializedObject(presentation);
                        view.FindProperty("radioGripOffset").vector3Value=new Vector3(-.092f,.413f,-.036f);
                        view.FindProperty("radioRotation").vector3Value=new Vector3(25.626f,-67.976f,53.75f);
                        view.FindProperty("radioScale").vector3Value=Vector3.one*1.666664f;
                        view.ApplyModifiedPropertiesWithoutUndo();
                    }
                    root.GetComponent<NetworkObject>().NetworkedBehaviours=root.GetComponentsInChildren<NetworkBehaviour>(true);
                    PrefabUtility.SaveAsPrefabAsset(root,path);count++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();Debug.Log("[Equipment] Weapon/radio configured on "+count+" player prefabs.");
        }
        static MaterialData Item(string file,string id,string label,CarriedItemKind kind)
        {
            string path=Data+"/"+file+".asset";var item=Asset<MaterialData>(path);
            if(item==null){item=ScriptableObject.CreateInstance<MaterialData>();AssetDatabase.CreateAsset(item,path);}
            item.id=id;item.materialName=label;item.kind=kind;return item;
        }
        static Material Material(string name,Color color)
        {
            string path=Folder+"/"+name+".mat";var mat=Asset<Material>(path);
            if(mat==null){var source=Asset<Material>("Assets/06j_Models/Caja_Craft/M_Caja_Craft.mat");mat=new Material(source.shader);AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);EditorUtility.SetDirty(mat);return mat;
        }
        static GameObject BuildRadio()
        {
            var root=new GameObject("Walkie-talkie");
            try
            {
                var body=Material("M_Radio_Body",new Color(.16f,.23f,.19f));
                var rubber=Material("M_Radio_Rubber",new Color(.025f,.035f,.04f));
                var screen=Material("M_Radio_Screen",new Color(.28f,.85f,.66f));
                var amber=Material("M_Radio_Button",new Color(.94f,.53f,.18f));
                Part(root,"Body",new Vector3(0,0,0),new Vector3(.085f,.16f,.045f),body);
                Part(root,"Rubber base",new Vector3(0,-.085f,0),new Vector3(.09f,.02f,.05f),rubber);
                Part(root,"Antenna",new Vector3(-.027f,.14f,0),new Vector3(.01f,.14f,.01f),rubber);
                Part(root,"Volume knob",new Vector3(.022f,.092f,0),new Vector3(.022f,.025f,.025f),rubber);
                Part(root,"Display bezel",new Vector3(0,.035f,-.025f),new Vector3(.066f,.043f,.008f),rubber);
                Part(root,"Display",new Vector3(0,.035f,-.03f),new Vector3(.054f,.029f,.004f),screen);
                for(int i=0;i<5;i++)Part(root,"Speaker grille "+i,new Vector3(0,-.005f-i*.009f,-.026f),new Vector3(.052f,.003f,.004f),rubber);
                Part(root,"Push to talk",new Vector3(.046f,.012f,0),new Vector3(.009f,.05f,.026f),amber);
                Part(root,"Channel LED",new Vector3(-.025f,.066f,-.025f),new Vector3(.01f,.008f,.005f),screen);
                return PrefabUtility.SaveAsPrefabAsset(root,VisualPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static GameObject BuildWeaponIcon()
        {
            var root=new GameObject("Weapon icon silhouette");
            try
            {
                var alloy=Material("M_WeaponIcon_Alloy",new Color(.7f,.77f,.8f));
                var dark=Material("M_Radio_Rubber",new Color(.025f,.035f,.04f));
                Part(root,"Slide",new Vector3(0,.08f,0),new Vector3(.30f,.075f,.055f),alloy);
                Part(root,"Grip",new Vector3(-.09f,-.03f,0),new Vector3(.075f,.16f,.055f),dark);
                Part(root,"Barrel",new Vector3(.175f,.08f,0),new Vector3(.06f,.045f,.045f),dark);
                Part(root,"Sight",new Vector3(.12f,.125f,0),new Vector3(.02f,.025f,.025f),dark);
                Part(root,"Trigger guard",new Vector3(-.006f,-.025f,0),new Vector3(.10f,.015f,.025f),alloy);
                Part(root,"Guard front",new Vector3(.04f,.005f,0),new Vector3(.015f,.06f,.025f),alloy);
                return PrefabUtility.SaveAsPrefabAsset(root,Folder+"/PrimaryWeaponIconModel.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void Part(GameObject root,string name,Vector3 position,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(root.transform,false);
            part.transform.localPosition=position;part.transform.localScale=size;part.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }
        static Bounds Bounds(GameObject go)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);return bounds;
        }
        static GameObject Pickup(MaterialData item,GameObject visual,string name)
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/02_Prefabs/Interactables/Materials/MetalPickup.prefab");
            try
            {
                root.name=name;while(root.transform.childCount>0)UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                var holder=new GameObject("Visual");holder.transform.SetParent(root.transform,false);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(visual,holder.transform);model.transform.localPosition=Vector3.zero;
                var bounds=Bounds(model);float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
                model.transform.localScale*= (item.kind==CarriedItemKind.Radio?.34f:.55f)/Mathf.Max(.001f,size);
                bounds=Bounds(model);model.transform.position+=Vector3.up*(.02f-bounds.min.y);bounds=Bounds(model);
                var collider=root.GetComponent<BoxCollider>();collider.center=root.transform.InverseTransformPoint(bounds.center);collider.size=bounds.size;
                var pickup=root.GetComponent<MaterialPickup>();Set(pickup,"material",item);Set(pickup,"visual",holder);
                var so=new SerializedObject(pickup);so.FindProperty("amount").intValue=1;so.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<NetworkObject>().NetworkedBehaviours=root.GetComponentsInChildren<NetworkBehaviour>(true);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");AssetDatabase.SetLabels(prefab,new[]{"FusionPrefab"});return prefab;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static Sprite Icon(GameObject prefab,string name)
        {
            string path=Data+"/"+name+".png";
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                var bounds=Bounds(model);
                var camGo=new GameObject("Icon camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo,scene);
                var camera=camGo.AddComponent<Camera>();camera.scene=scene;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.orthographic=true;
                camera.orthographicSize=Mathf.Max(bounds.extents.y,Mathf.Max(bounds.extents.x,bounds.extents.z))*1.5f;
                camera.transform.position=bounds.center+new Vector3(.8f,.35f,-1)*Mathf.Max(.5f,bounds.size.magnitude)*2;camera.transform.LookAt(bounds.center);
                var lightGo=new GameObject("Icon light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo,scene);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(35,-30,0);light.cullingMask=1<<31;
                var rt=new RenderTexture(256,256,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
                var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.SaveAndReimport();return Asset<Sprite>(path);
        }
    }
}
