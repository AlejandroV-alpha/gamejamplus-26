using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

namespace GameJam.Multiplayer
{
    /// <summary>
    /// Red-alert launch sequence for the lobby, shown on every client: red pulsing ceiling lights,
    /// animated warning triangles on the console monitors and a countdown on the destination screen.
    /// The scene authority loads the mission when the countdown ends.
    /// </summary>
    public sealed class LobbyAlert : NetworkBehaviour
    {
        public GameObject lobbyRoot;
        public float countdown = 5f;
        public Color alertColor = new(1f, 0.08f, 0.04f);
        [Tooltip("Cinematic played fullscreen while the mission scene loads.")]
        public UnityEngine.Video.VideoClip transitionVideo;
        private bool videoStarted;

        private readonly List<(Light light, float intensity)> ceilingLights = new();
        private readonly List<Material> stripMaterials = new();
        private readonly List<Material> monitorMaterials = new();
        private readonly List<Transform> warnings = new();
        private Material destinationMaterial;
        private TextMeshPro destinationText;
        private bool active;
        private bool loading;
        private float startTime;
        private int sceneIndex = -1;

        private static string BaseName(Object o) => o.name.Split('.')[0];

        private void Awake()
        {
            if (lobbyRoot == null) return;
            foreach (var l in lobbyRoot.GetComponentsInChildren<Light>(true))
                if (l.name.StartsWith("Luz_Tira_Luz_")) ceilingLights.Add((l, l.intensity));
            var renderers = lobbyRoot.GetComponentsInChildren<Renderer>(true);
            var floor = renderers.FirstOrDefault(r => BaseName(r) == "Suelo");
            var roomCenter = floor != null ? floor.bounds.center : lobbyRoot.transform.position;
            foreach (var r in renderers)
            {
                string n = BaseName(r);
                if (n.StartsWith("Tira_Luz_") && !n.Contains("Clip")) stripMaterials.Add(r.material);
                else if (n.StartsWith("Consola_Pantalla_"))
                {
                    monitorMaterials.Add(r.material);
                    warnings.Add(CreateWarning(r, roomCenter));
                }
                else if (n == "Pantalla_Grande") destinationMaterial = r.material;
            }
            destinationText = lobbyRoot.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<TextMeshPro>(true))
                .FirstOrDefault(t => t.name == "Destino_Texto");
        }

        /// <summary>Called by the start button on the scene authority.</summary>
        public void Begin(int missionSceneIndex)
        {
            if (active) return;
            sceneIndex = missionSceneIndex;
            RPC_StartAlert();
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_StartAlert()
        {
            if (active) return;
            active = true;
            startTime = Time.time;
            foreach (var w in warnings) w.gameObject.SetActive(true);
            foreach (var (light, _) in ceilingLights) light.color = alertColor;
            foreach (var m in monitorMaterials) { m.SetColor("_BaseColor", new Color(0.05f, 0f, 0f)); }
        }

        private void Update()
        {
            if (!active) return;
            float t = Time.time - startTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 7f);

            foreach (var (light, intensity) in ceilingLights) light.intensity = intensity * (0.3f + 1.2f * pulse);
            foreach (var m in stripMaterials) m.SetColor("_EmissionColor", alertColor * (1f + 5f * pulse));
            foreach (var m in monitorMaterials) m.SetColor("_EmissionColor", alertColor * (0.2f + 0.6f * pulse));
            foreach (var w in warnings) w.localScale = Vector3.one * (0.85f + 0.25f * pulse);
            if (destinationMaterial != null) destinationMaterial.SetColor("_EmissionColor", alertColor * (0.15f + 0.5f * pulse));
            if (destinationText != null)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(countdown - t));
                destinationText.text = $"<size=55%><color=#FFD0C0>ALERTA</color></size>\n<b><color=#FF3020>DESPEGUE</color></b>\n<size=45%><color=#FFB347>INICIANDO EN {left}</color></size>";
            }

            // Every client covers the scene change with the crash cinematic; it outlives the lobby scene.
            if (!videoStarted && t >= countdown)
            {
                videoStarted = true;
                SceneTransitionVideo.Play(transitionVideo);
            }

            if (!loading && sceneIndex >= 0 && t >= countdown && Runner != null && Runner.IsSceneAuthority)
            {
                loading = true;
                Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
            }
        }

        /// <summary>A red triangle with an exclamation mark floating just in front of a monitor.</summary>
        private Transform CreateWarning(Renderer monitor, Vector3 roomCenter)
        {
            var b = monitor.bounds;
            var normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            if (Vector3.Dot(roomCenter - b.center, normal) < 0) normal = -normal;
            float h = b.size.y * 0.38f;

            var root = new GameObject("Alerta_" + monitor.name).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(b.center + normal * (Mathf.Min(b.size.x, b.size.z) * 0.5f + 0.02f),
                Quaternion.LookRotation(-normal, Vector3.up));

            var tri = new GameObject("Triangulo");
            tri.transform.SetParent(root, false);
            var mesh = new Mesh { name = "AlertTriangle" };
            // Clockwise as seen by the viewer (looking along +forward), so the front face is visible.
            mesh.vertices = new[] { new Vector3(0, h, 0), new Vector3(h * 1.1f, -h * 0.8f, 0), new Vector3(-h * 1.1f, -h * 0.8f, 0) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            tri.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = new Material(monitor.sharedMaterial);
            mat.SetColor("_BaseColor", alertColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", alertColor * 4f);
            tri.AddComponent<MeshRenderer>().sharedMaterial = mat;

            var mark = new GameObject("Exclamacion").AddComponent<TextMeshPro>();
            mark.transform.SetParent(root, false);
            mark.transform.localPosition = new Vector3(0, -h * 0.15f, -0.005f);
            mark.rectTransform.sizeDelta = new Vector2(h, h * 1.4f);
            mark.alignment = TextAlignmentOptions.Center;
            mark.fontStyle = FontStyles.Bold;
            mark.enableAutoSizing = true;
            mark.fontSizeMin = 0.1f;
            mark.fontSizeMax = 10f;
            mark.color = new Color(0.08f, 0f, 0f);
            mark.text = "!";

            root.gameObject.SetActive(false);
            return root;
        }
    }
}
