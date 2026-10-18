using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameJam.Editor
{
    /// <summary>Gives the Blender lobby FBX URP materials with emission and glass on import.</summary>
    public class LobyDeEsperaMaterialPostprocessor : AssetPostprocessor
    {
        // Blender material name -> emission intensity (HDR multiplier of the base color).
        static readonly Dictionary<string, float> Emissive = new()
        {
            { "Nave_Luz_Calida", 6f }, { "Nave_Tubo_Luz", 2f }, { "Nave_Verde_Brillo", 4f },
            { "Nave_Liquido_Brillante", 4f }, { "Nave_Burbuja", 6f }, { "Nave_Pantalla", 2.5f },
            { "Nave_Pantalla_Verde", 2.5f }, { "Nave_Rojo", 1.5f }, { "Nave_Rojo_Boton", 3f },
            { "Nave_Holo", 3f }, { "Nave_Luz_Amarilla", 6f }, { "Nave_Naranja", 0.4f },
            { "Nave_Agua", 0.5f }, { "Nave_Ventanilla", 0.3f },
        };

        void OnPostprocessMaterial(Material material)
        {
            if (!assetPath.Contains("LobyDeEspera")) return;
            var name = material.name.Split('.')[0];
            var baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            if (name == "Nave_Liquido_Brillante") baseColor = new Color(0.3f, 1f, 0.4f);

            if (Emissive.TryGetValue(name, out var intensity))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", baseColor.linear * intensity);
            }

            if (name == "Nave_Vidrio")
            {
                baseColor.a = 0.2f;
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", name == "Nave_Vidrio" ? 0.95f : 0.2f);
        }
    }

    public static class LobyDeEsperaSetup
    {
        public const string FbxPath = "Assets/06j_Models/LobyDeEspera/LobyDeEspera.fbx";
        public const string ScenePath = "Assets/00_Scenes/j_LobyDeEspera.unity";
        public const string ProfilePath = "Assets/06j_Models/LobyDeEspera/LobyDeEspera_Volume.asset";

        [MenuItem("Tools/GameJam/Create Loby De Espera Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new System.InvalidOperationException("Lobby scene already exists; edit the existing scene.");
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) ?? throw new System.InvalidOperationException($"Missing {FbxPath}.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lobby = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            lobby.name = "LobyDeEspera";
            GameObjectUtility.SetStaticEditorFlags(lobby, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

            foreach (var r in lobby.GetComponentsInChildren<MeshRenderer>())
            {
                var n = r.name;
                if (!n.Contains("Burbuja") && !n.Contains("Holograma") && !n.Contains("Haz") && !n.Contains("Vidrio"))
                    r.gameObject.AddComponent<MeshCollider>();
                // Lights come from the glowing props, so they follow the model whatever the axis conversion.
                if (n.StartsWith("Tira_Luz_") && !n.Contains("Clip")) AddLight(r, new Color(1f, 0.85f, 0.6f), 9f, 2.5f);
                else if (n.EndsWith("_Liquido")) AddLight(r, new Color(0.3f, 1f, 0.4f), 5f, 2f);
                else if (n.EndsWith("_Tubo_Luz") && n.StartsWith("Puerta")) AddLight(r, new Color(1f, 0.75f, 0.2f), 5f, 1.5f);
                else if (n == "Holograma_Planeta") AddLight(r, new Color(0.3f, 0.6f, 1f), 3f, 1f);
                else if (n.StartsWith("Consola_Pantalla") || n == "Pantalla_Grande") AddLight(r, new Color(0.3f, 0.8f, 1f), 3f, 1f);
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.11f);

            // Bloom so the emissive tubes, screens and light strips actually glow.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(1.2f);
                bloom.threshold.Override(1f);
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.ACES);
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var bounds = new Bounds(lobby.transform.position, Vector3.zero);
            foreach (var r in lobby.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.AddComponent<Camera>().fieldOfView = 70f;
            cam.AddComponent<AudioListener>();
            cam.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            cam.transform.position = bounds.center + new Vector3(-bounds.extents.x * 0.8f, bounds.extents.y * 0.2f, -bounds.extents.z * 0.8f);
            cam.transform.LookAt(bounds.center - Vector3.up * bounds.extents.y * 0.5f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = lobby;
            Debug.Log($"Lobby scene created at {ScenePath}.");
        }

        [MenuItem("Tools/GameJam/Add Player To Loby De Espera")]
        public static void AddPlayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (Object.FindFirstObjectByType<GameJam.Multiplayer.MarsFusionSession>() != null)
                throw new System.InvalidOperationException("The lobby already has a player session.");
            var lobby = GameObject.Find("LobyDeEspera") ?? throw new System.InvalidOperationException("LobyDeEspera not found in the scene.");
            Transform Part(string n) => lobby.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.Split('.')[0] == n) // Blender may add .001 suffixes
                                        ?? throw new System.InvalidOperationException($"{n} not found in the lobby model.");
            var table = Part("Mesa_Tablero").GetComponent<Renderer>().bounds.center;
            var door = Part("Puerta_A_Hoja").GetComponent<Renderer>().bounds.center;
            var floorY = Part("Suelo").GetComponent<Renderer>().bounds.max.y;

            // Between door A and the meeting table: clear of the benches, stairs and lockers.
            var spawn = new GameObject("j_FusionSpawn").transform;
            var p = Vector3.Lerp(door, table, 0.5f);
            spawn.position = new Vector3(p.x, floorY + 0.1f, p.z);
            var look = table - spawn.position; look.y = 0;
            spawn.rotation = Quaternion.LookRotation(look);

            var manager = new GameObject("j_PhotonFusionSession").AddComponent<GameJam.Multiplayer.MarsFusionSession>();
            manager.playerPrefab = AssetDatabase.LoadAssetAtPath<Fusion.NetworkObject>(MarsFusionMigration.PlayerPath);
            manager.runnerPrefab = AssetDatabase.LoadAssetAtPath<Fusion.NetworkRunner>("Assets/PhotonStarter/j_FusionRunner.prefab");
            manager.spawnPoint = spawn;
            manager.roomName = "Loby-Coop";
            manager.groundProbeHeight = 1.5f; // stay under the ceiling
            var spawner = new GameObject("j_FusionPlayerSpawner");
            spawner.AddComponent<Fusion.NetworkObject>();
            spawner.AddComponent<GameJam.Multiplayer.MarsFusionSpawner>();
            new Fusion.Editor.NetworkObjectBakerEditTime().Bake(spawner);

            // Fusion loads the session scene by build index.
            if (EditorBuildSettings.scenes.All(s => s.path != ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = manager.gameObject;
            Debug.Log("Player session added to the lobby. Press Play and choose 'Prueba local'.");
        }

        [MenuItem("Tools/GameJam/Add Player Count To Loby Screen")]
        public static void AddPlayerCountScreen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (Object.FindFirstObjectByType<GameJam.Multiplayer.LobbyPlayerCountScreen>() != null)
                throw new System.InvalidOperationException("The lobby screen already shows the player count.");
            var lobby = GameObject.Find("LobyDeEspera") ?? throw new System.InvalidOperationException("LobyDeEspera not found in the scene.");
            var screen = lobby.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.Split('.')[0] == "Mapa_Pantalla")
                         ?? throw new System.InvalidOperationException("Mapa_Pantalla not found in the lobby model.");
            var b = screen.bounds;

            // The screen is a thin box: its thinnest horizontal axis is the wall normal; point it into the room.
            var normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            var roomCenter = lobby.GetComponentsInChildren<Renderer>().First(r => r.name.Split('.')[0] == "Suelo").bounds.center;
            if (Vector3.Dot(roomCenter - b.center, normal) < 0) normal = -normal;
            float width = Mathf.Max(b.size.x, b.size.z);

            var go = new GameObject("j_PlayerCountScreen");
            go.transform.position = b.center + normal * (Mathf.Min(b.size.x, b.size.z) / 2 + 0.02f);
            go.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up); // TMP is read looking along +forward
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(width * 0.9f, b.size.y * 0.9f);
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 4f;
            tmp.color = new Color(0.85f, 1f, 1f);
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.text = "JUGADORES\n0 / 4";
            go.AddComponent<GameJam.Multiplayer.LobbyPlayerCountScreen>().label = tmp;

            // Darken the glowing panel so the text is readable on it.
            var dark = new Material(screen.sharedMaterial) { name = "Nave_Pantalla_Contador" };
            dark.SetColor("_BaseColor", new Color(0.02f, 0.08f, 0.12f));
            dark.SetColor("_EmissionColor", new Color(0.05f, 0.25f, 0.4f));
            AssetDatabase.CreateAsset(dark, "Assets/06j_Models/LobyDeEspera/Nave_Pantalla_Contador.mat");
            screen.sharedMaterial = dark;

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = go;
            Debug.Log("Player count screen added to the lobby.");
        }

        [MenuItem("Tools/GameJam/Add Console Buttons And Destination Screen")]
        public static void AddConsoleButtonsAndPlanet()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (Object.FindFirstObjectByType<GameJam.Multiplayer.LobbyConsoleButton>() != null)
                throw new System.InvalidOperationException("The lobby console already has its buttons.");
            var lobby = GameObject.Find("LobyDeEspera") ?? throw new System.InvalidOperationException("LobyDeEspera not found in the scene.");
            var roomCenter = Part(lobby, "Suelo").bounds.center;

            // Buttons on top of the console, on the room side, left and right of its centre.
            var console = Part(lobby, "Consola_A").bounds;
            var (cNormal, cAlong) = FacingAxes(console, roomCenter);
            var top = console.center + Vector3.up * console.extents.y + cNormal * (Dot(console.extents, cNormal) * 0.45f);
            float half = Dot(console.extents, cAlong);
            CreateButton("j_Boton_Comenzar", top + cAlong * half * 0.4f, cNormal, GameJam.Multiplayer.LobbyConsoleButton.ButtonAction.StartGame,
                "COMENZAR PARTIDA", new Color(0.2f, 1f, 0.35f));
            CreateButton("j_Boton_Salir", top - cAlong * half * 0.4f, cNormal, GameJam.Multiplayer.LobbyConsoleButton.ButtonAction.LeaveLobby,
                "DEJAR LOBBY", new Color(1f, 0.15f, 0.08f));

            // Destination planet on the big screen above the console.
            var screen = Part(lobby, "Pantalla_Grande");
            var sb = screen.bounds;
            var (sNormal, sAlong) = FacingAxes(sb, roomCenter);
            float sw = Dot(sb.extents, sAlong) * 2f;
            var dark = new Material(screen.sharedMaterial) { name = "Nave_Pantalla_Destino" };
            dark.SetColor("_BaseColor", new Color(0.01f, 0.02f, 0.05f));
            dark.SetColor("_EmissionColor", new Color(0.02f, 0.06f, 0.15f));
            AssetDatabase.CreateAsset(dark, "Assets/06j_Models/LobyDeEspera/Nave_Pantalla_Destino.mat");
            screen.sharedMaterial = dark;

            var root = new GameObject("j_DestinationScreen").transform;
            root.position = sb.center + sNormal * (Dot(sb.extents, sNormal) + 0.02f);
            root.rotation = Quaternion.LookRotation(-sNormal, Vector3.up);

            float radius = sb.size.y * 0.36f;
            var planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(planet.GetComponent<Collider>());
            planet.name = "Planeta_Marte";
            planet.transform.SetParent(root, false);
            planet.transform.localPosition = new Vector3(-sw * 0.25f, 0f, -radius * 0.3f);
            planet.transform.localScale = Vector3.one * radius * 2f;
            planet.transform.localRotation = Quaternion.Euler(-20f, 0f, 15f);
            var mars = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Planeta_Marte" };
            mars.SetColor("_BaseColor", new Color(0.78f, 0.32f, 0.13f));
            mars.EnableKeyword("_EMISSION");
            mars.SetColor("_EmissionColor", new Color(0.35f, 0.1f, 0.03f));
            AssetDatabase.CreateAsset(mars, "Assets/06j_Models/LobyDeEspera/Planeta_Marte.mat");
            planet.GetComponent<Renderer>().sharedMaterial = mars;
            planet.AddComponent<GameJam.Multiplayer.LobbySpin>();
            // A few darker "craters" so the rotation reads.
            var crater = new Material(mars) { name = "Planeta_Marte_Crater" };
            crater.SetColor("_BaseColor", new Color(0.45f, 0.15f, 0.07f));
            crater.SetColor("_EmissionColor", new Color(0.15f, 0.04f, 0.01f));
            AssetDatabase.CreateAsset(crater, "Assets/06j_Models/LobyDeEspera/Planeta_Marte_Crater.mat");
            var rnd = new System.Random(4);
            for (int i = 0; i < 9; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(c.GetComponent<Collider>());
                c.name = $"Crater_{i}";
                c.transform.SetParent(planet.transform, false);
                c.transform.localPosition = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized * 0.46f;
                c.transform.localScale = Vector3.one * (0.12f + (float)rnd.NextDouble() * 0.12f);
                c.GetComponent<Renderer>().sharedMaterial = crater;
            }
            var ringMat = new Material(mars) { name = "Planeta_Orbita" };
            ringMat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.15f));
            ringMat.SetColor("_EmissionColor", new Color(2f, 0.8f, 0.2f));
            AssetDatabase.CreateAsset(ringMat, "Assets/06j_Models/LobyDeEspera/Planeta_Orbita.mat");
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.name = "Orbita";
            ring.transform.SetParent(root, false);
            ring.transform.localPosition = planet.transform.localPosition;
            ring.transform.localScale = new Vector3(radius * 3f, 0.002f, radius * 3f);
            ring.transform.localRotation = Quaternion.Euler(-70f, 0f, 15f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;

            var info = new GameObject("Destino_Texto").AddComponent<TMPro.TextMeshPro>();
            info.transform.SetParent(root, false);
            info.transform.localPosition = new Vector3(sw * 0.2f, 0f, -0.01f);
            info.rectTransform.sizeDelta = new Vector2(sw * 0.5f, sb.size.y * 0.85f);
            info.enableAutoSizing = true;
            info.fontSizeMin = 0.3f;
            info.fontSizeMax = 3f;
            info.alignment = TMPro.TextAlignmentOptions.Left;
            info.color = new Color(0.85f, 1f, 1f);
            info.text = "<size=55%><color=#7FD8FF>DESTINO</color></size>\n<b>MARTE</b>\n<size=45%><color=#FFB347>4º PLANETA · 225 M km</color></size>";

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("Console buttons and destination screen added to the lobby.");
        }

        [MenuItem("Tools/GameJam/Animate Loby Hologram")]
        public static void AnimateHologram()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var lobby = GameObject.Find("LobyDeEspera") ?? throw new System.InvalidOperationException("LobyDeEspera not found in the scene.");
            void Spin(string n, Vector3 speed, float bob)
            {
                var go = Part(lobby, n).gameObject;
                GameObjectUtility.SetStaticEditorFlags(go, 0); // static batching would freeze it in place
                var spin = go.GetComponent<GameJam.Multiplayer.LobbySpin>() ?? go.AddComponent<GameJam.Multiplayer.LobbySpin>();
                spin.degreesPerSecond = speed;
                spin.space = Space.World;
                spin.bobHeight = bob;
            }
            Spin("Holograma_Planeta", new Vector3(0f, 40f, 0f), 0.04f);
            Spin("Holograma_Anillo", new Vector3(0f, -70f, 0f), 0.04f);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Lobby hologram animated.");
        }

        [MenuItem("Tools/GameJam/Add Loby Launch Alert")]
        public static void AddLaunchAlert()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (Object.FindFirstObjectByType<GameJam.Multiplayer.LobbyAlert>() != null)
                throw new System.InvalidOperationException("The lobby already has a launch alert.");
            var lobby = GameObject.Find("LobyDeEspera") ?? throw new System.InvalidOperationException("LobyDeEspera not found in the scene.");
            var go = new GameObject("j_LobbyAlert");
            go.AddComponent<Fusion.NetworkObject>();
            go.AddComponent<GameJam.Multiplayer.LobbyAlert>().lobbyRoot = lobby;
            new Fusion.Editor.NetworkObjectBakerEditTime().Bake(go);
            // Strip lights and monitors change material at runtime.
            foreach (var r in lobby.GetComponentsInChildren<Renderer>())
            {
                var n = r.name.Split('.')[0];
                if ((n.StartsWith("Tira_Luz_") && !n.Contains("Clip")) || n.StartsWith("Consola_Pantalla_") || n == "Pantalla_Grande")
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Launch alert added to the lobby.");
        }

        public const string TransitionVideoPath = "Assets/06j_Models/TransicionNave/TransicionNaveChocando.mp4";

        [MenuItem("Tools/GameJam/Assign Loby Transition Video")]
        public static void AssignTransitionVideo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play Mode first.");
            var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(TransitionVideoPath)
                       ?? throw new System.InvalidOperationException($"Missing {TransitionVideoPath}.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var alert = Object.FindFirstObjectByType<GameJam.Multiplayer.LobbyAlert>()
                        ?? throw new System.InvalidOperationException("Run 'Add Loby Launch Alert' first.");
            alert.transitionVideo = clip;
            EditorUtility.SetDirty(alert);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Transition video assigned to the lobby alert.");
        }

        static Renderer Part(GameObject lobby, string n) =>
            lobby.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.Split('.')[0] == n)
            ?? throw new System.InvalidOperationException($"{n} not found in the lobby model.");

        static float Dot(Vector3 a, Vector3 b) => Mathf.Abs(Vector3.Dot(a, b));

        /// <summary>For a wall-aligned box: the horizontal axis pointing into the room, and the axis along the wall.</summary>
        static (Vector3 normal, Vector3 along) FacingAxes(Bounds b, Vector3 roomCenter)
        {
            var normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            if (Vector3.Dot(roomCenter - b.center, normal) < 0) normal = -normal;
            return (normal, Vector3.Cross(Vector3.up, normal));
        }

        static void CreateButton(string name, Vector3 pos, Vector3 normal, GameJam.Multiplayer.LobbyConsoleButton.ButtonAction action, string title, Color color)
        {
            var root = new GameObject(name).transform;
            root.SetPositionAndRotation(pos, Quaternion.LookRotation(-normal, Vector3.up));

            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseGo.name = "Base";
            baseGo.transform.SetParent(root, false);
            baseGo.transform.localPosition = new Vector3(0, 0.02f, 0);
            baseGo.transform.localScale = new Vector3(0.32f, 0.02f, 0.32f);
            var baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name + "_Base" };
            baseMat.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.08f));
            AssetDatabase.CreateAsset(baseMat, $"Assets/06j_Models/LobyDeEspera/{name}_Base.mat");
            baseGo.GetComponent<Renderer>().sharedMaterial = baseMat;

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.name = "Boton";
            cap.transform.SetParent(root, false);
            cap.transform.localPosition = new Vector3(0, 0.06f, 0);
            cap.transform.localScale = new Vector3(0.22f, 0.03f, 0.22f);
            var capMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name + "_Boton" };
            capMat.SetColor("_BaseColor", color);
            capMat.EnableKeyword("_EMISSION");
            capMat.SetColor("_EmissionColor", color * 2.5f);
            AssetDatabase.CreateAsset(capMat, $"Assets/06j_Models/LobyDeEspera/{name}_Boton.mat");
            cap.GetComponent<Renderer>().sharedMaterial = capMat;

            var glow = new GameObject("Luz").AddComponent<Light>();
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(0, 0.25f, 0);
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = 1.2f;
            glow.intensity = 1.5f;

            var label = new GameObject("Etiqueta").AddComponent<TMPro.TextMeshPro>();
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(0, 0.32f, 0);
            label.rectTransform.sizeDelta = new Vector2(1.2f, 0.25f);
            label.fontSize = 1.2f;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.color = color;
            label.text = title;

            var button = root.gameObject.AddComponent<GameJam.Multiplayer.LobbyConsoleButton>();
            button.action = action;
            button.title = title;
            button.cap = cap.transform;
            button.label = label;
        }

        static void AddLight(Renderer r, Color color, float range, float intensity)
        {
            var go = new GameObject($"Luz_{r.name}");
            go.transform.SetParent(r.transform.root, true);
            go.transform.position = r.bounds.center - Vector3.up * 0.3f;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }
    }
}
