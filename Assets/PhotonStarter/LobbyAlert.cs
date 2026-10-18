using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

namespace GameJam.Multiplayer
{
    /// <summary>
    /// Red-alert launch sequence for the lobby, shown on every client: red pulsing ceiling lights,
    /// cinematic, impact and exit portal. Authority travels once every active player enters.
    /// </summary>
    public sealed class LobbyAlert : NetworkBehaviour
    {
        public GameObject lobbyRoot;
        public float countdown = 5f;
        public Color alertColor = new(1f, 0.08f, 0.04f);
        [Tooltip("Cinematic played inside the lobby before opening the exit portal.")]
        public UnityEngine.Video.VideoClip transitionVideo;
        public Material portalMaterial;
        [Header("Collision sound and landing")]
        public AudioClip meteorImpactSound;
        public AudioClip landingImpactSound;
        public float[] meteorImpactTimes = { 1.8f, 3.6f, 5.7f, 6.9f };
        [Range(0,1)] public float meteorVolume = .55f;
        [Range(0,1)] public float landingVolume = .75f;
        [Min(.1f)] public float landingImpactSeconds = 1.8f;
        [Range(0,1)] public float landingShakeStrength = .65f;

        private readonly List<(Light light, float intensity)> ceilingLights = new();
        private readonly List<Material> stripMaterials = new();
        private readonly List<Material> monitorMaterials = new();
        private readonly List<Transform> warnings = new();
        private Material destinationMaterial;
        private TextMeshPro destinationText;
        [Networked] public int DeparturePhase { get; private set; }
        [Networked] private TickTimer PhaseTimer { get; set; }
        [Networked] private int MissionSceneIndex { get; set; }
        public int DestinationSceneIndex => MissionSceneIndex;
        [Networked] public int PortalEnteredCount { get; private set; }
        private readonly HashSet<PlayerRef> cinematicFinished = new();
        private readonly HashSet<PlayerRef> portalEntered = new();
        private bool localVideoStarted, localVideoFinished;
        private int shownPhase;
        private LobbyDepartureEffects effects;
        public bool DepartureActive => Object != null && Object.IsValid && DeparturePhase > 0;
        public float PhaseRemaining => Object != null && Object.IsValid ? PhaseTimer.RemainingTime(Runner) ?? 0 : 0;

        private static string BaseName(Object o) => o.name.Split('.')[0];

        private void Awake()
        {
            effects = gameObject.AddComponent<LobbyDepartureEffects>();
            effects.alert = this;
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
            if (DepartureActive || Runner == null || !HasStateAuthority || !LobbyConsoleButton.AllPlayersReady(Runner) || missionSceneIndex < 0) return;
            MissionSceneIndex = missionSceneIndex;
            DeparturePhase = 1;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, countdown);
            Debug.Log("[Lobby departure] stage=alarm");
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (DeparturePhase == 0 && LobbyConsoleButton.AllPlayersReady(Runner))
                Begin(UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Assets/00_Scenes/j_MarsFusion.unity"));
            else if (DeparturePhase == 1 && PhaseTimer.Expired(Runner))
            {
                DeparturePhase = 2;
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, 40);
                Debug.Log("[Lobby departure] stage=cinematic");
            }
            else if (DeparturePhase == 2 && (Runner.ActivePlayers.All(p => cinematicFinished.Contains(p)) || PhaseTimer.Expired(Runner)))
            {
                DeparturePhase = 3;
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, landingImpactSeconds);
                Debug.Log("[Lobby departure] stage=impact");
            }
            else if (DeparturePhase == 3 && PhaseTimer.Expired(Runner))
            {
                DeparturePhase = 4;
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, 1.4f);
                Debug.Log("[Lobby departure] stage=portal-open");
            }
            else if (DeparturePhase == 4)
            {
                PortalEnteredCount = Runner.ActivePlayers.Count(p => portalEntered.Contains(p));
                if (Runner.ActivePlayers.Any() && Runner.ActivePlayers.All(p => portalEntered.Contains(p)))
                {
                    DeparturePhase = 5;
                    Debug.Log("[Lobby departure] stage=travel");
                    Runner.LoadScene(SceneRef.FromIndex(MissionSceneIndex));
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_CinematicFinished(RpcInfo info = default)
        {
            if (DeparturePhase != 2) return;
            var source = Runner.GameMode == GameMode.Single ? Runner.LocalPlayer : info.Source;
            if (Runner.ActivePlayers.Contains(source)) cinematicFinished.Add(source);
        }

        public void EnterPortal() => RPC_EnterPortal();

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_EnterPortal(RpcInfo info = default)
        {
            if (DeparturePhase != 4 || effects == null) return;
            var source = Runner.GameMode == GameMode.Single ? Runner.LocalPlayer : info.Source;
            var player = Runner.GetPlayerObject(source);
            if (player != null && effects.ContainsPortal(player.transform.position)) portalEntered.Add(source);
        }

        private void Update()
        {
            if (!DepartureActive) return;
            if (shownPhase != DeparturePhase)
            {
                shownPhase = DeparturePhase;
                foreach (var w in warnings) w.gameObject.SetActive(DeparturePhase < 4);
                foreach (var (light, _) in ceilingLights) light.color = alertColor;
                if (DeparturePhase >= 3) SceneTransitionVideo.CancelCurrent();
            }
            if (DeparturePhase == 2)
            {
                if (!localVideoStarted)
                {
                    localVideoStarted = true;
                    SceneTransitionVideo.Play(transitionVideo);
                }
                else if (!localVideoFinished && !SceneTransitionVideo.IsPresent)
                {
                    localVideoFinished = true;
                    RPC_CinematicFinished();
                }
            }
            float t = Time.time;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 7f);

            foreach (var (light, intensity) in ceilingLights) light.intensity = intensity * (0.3f + 1.2f * pulse);
            foreach (var m in stripMaterials) m.SetColor("_EmissionColor", alertColor * (1f + 5f * pulse));
            foreach (var m in monitorMaterials) m.SetColor("_EmissionColor", alertColor * (0.2f + 0.6f * pulse));
            foreach (var w in warnings) w.localScale = Vector3.one * (0.85f + 0.25f * pulse);
            if (destinationMaterial != null) destinationMaterial.SetColor("_EmissionColor", alertColor * (0.15f + 0.5f * pulse));
            if (destinationText != null)
            {
                int left = Mathf.CeilToInt(PhaseRemaining);
                destinationText.text = DeparturePhase >= 4
                    ? $"<size=50%>COMPUERTA ABIERTA</size>\n<b>HACIA MARTE</b>\n<size=40%>CRUZAD EL PORTAL · {PortalEnteredCount} / {Runner.ActivePlayers.Count()}</size>"
                    : $"<size=55%><color=#FFD0C0>ALERTA</color></size>\n<b><color=#FF3020>TURBULENCIA</color></b>\n<size=45%><color=#FFB347>{(DeparturePhase == 1 ? "IMPACTO EN " + left : "ASEGURA TU POSICIÓN")}</color></size>";
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
