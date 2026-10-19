using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameJam.Multiplayer
{
    public sealed class MarsFusionSession : MonoBehaviour
    {
        public NetworkObject playerPrefab;
        public NetworkRunner runnerPrefab;
        public Transform spawnPoint;
        public int maxPlayers = 4;
        public string roomName = "Mars-Coop";
        [Tooltip("Height above the spawn point where the ground raycast starts. Keep it below the ceiling in indoor scenes.")]
        public float groundProbeHeight = 100f;
        public string planetLabel = "MARTE";
        public NetworkRunner Runner { get; private set; }
        public string Status { get; private set; } = "Photon Fusion — listo";
        public bool MenuOpen { get; private set; } = true;
        public bool Busy { get; private set; }
        private CursorLockMode previousLock;
        private bool previousVisible;
        private bool previousBackground;
        private static string shutdownMessage;
        private int connectionAttempt;
        private PlayerRef sessionHost;
        private bool hostDepartureHandled;

        private void LogStage(string stage, string details = "") =>
            Debug.Log("[Mars Fusion pid=" + System.Diagnostics.Process.GetCurrentProcess().Id + " attempt=" + connectionAttempt + "] stage=" + stage + " " + details, this);

        private void Awake()
        {
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            if (!string.IsNullOrEmpty(shutdownMessage)) { Status = shutdownMessage; shutdownMessage = null; }
            if (FindFirstObjectByType<GameJam.UI.SciFiGameMenuManager>() == null)
            {
                var menuGo = new GameObject("SciFiGameMenuManager");
                menuGo.AddComponent<GameJam.UI.SciFiGameMenuManager>();
            }
            // Arriving from another scene of the same session (e.g. lobby -> mission): keep the running runner.
            foreach (var runner in NetworkRunner.Instances)
            {
                if (runner == null || !runner.IsRunning) continue;
                Runner = runner;
                FusionLoadDiagnostics.Attach(runner);
                AttachSessionEvents(runner);
                Status = "Conectado — " + runner.SessionInfo.Name;
                LogStage("runner-adopted", "scene=" + SceneManager.GetActiveScene().name);
                SetMenu(false);
                return;
            }
            SetMenu(true);
        }

        public async Task<bool> StartSession(bool offline)
        {
            if (Busy || Runner != null || playerPrefab == null || runnerPrefab == null)
            {
                LogStage("start-rejected", "busy=" + Busy + " runnerExists=" + (Runner != null) + " playerPrefab=" + (playerPrefab != null) + " runnerPrefab=" + (runnerPrefab != null));
                return false;
            }
            connectionAttempt++;
            LogStage("start-requested", "mode=" + (offline ? "Single" : "Shared") + " sceneIndex=" + SceneManager.GetActiveScene().buildIndex);
            if (SceneManager.GetActiveScene().buildIndex < 0)
            {
                Status = "La escena actual no está incluida en la lista de escenas del Build Profile activo. Agrégala antes de iniciar Fusion.";
                LogStage("scene-config-failed", SceneManager.GetActiveScene().path);
                Debug.LogError("[Mars Fusion] " + Status, this);
                return false;
            }
            FusionAppSettings settings = PhotonLocalSettings.Read();
            if (!offline && (settings == null || string.IsNullOrWhiteSpace(settings.AppIdFusion)))
            {
                Status = PhotonLocalSettings.LastDiagnostic;
                LogStage("config-failed", Status);
                return false;
            }
            LogStage("config-ready", "appIdPresent=" + (settings != null && !string.IsNullOrWhiteSpace(settings.AppIdFusion)) + " region=" + (string.IsNullOrEmpty(settings?.FixedRegion) ? "auto" : settings.FixedRegion));
            Busy = true;
            var runner = Instantiate(runnerPrefab);
            Runner = runner;
            FusionLoadDiagnostics.Attach(runner);
            LogStage("runner-created");
            try
            {
                AttachSessionEvents(runner);
                var scenes = runner.GetComponent<NetworkSceneManagerDefault>();
                if (scenes == null) scenes = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
                Status = offline ? "Iniciando prueba local..." : "Conectando con Photon...";
                var info = new NetworkSceneInfo();
                info.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));
                LogStage("connecting");
                var result = await runner.StartGame(new StartGameArgs
                {
                    GameMode = offline ? GameMode.Single : GameMode.Shared,
                    SessionName = roomName.Trim(), PlayerCount = Mathf.Clamp(maxPlayers, 1, MarsPlayerIdentityView.Palette.Length),
                    Scene = info, SceneManager = scenes, CustomPhotonAppSettings = settings,
                    SessionProperties = new Dictionary<string, SessionProperty> { ["GameMode"] = "MarsCoop-v1" }
                });
                LogStage("start-result", "ok=" + result.Ok + " reason=" + result.ShutdownReason);
                if (!result.Ok) throw new InvalidOperationException(result.ShutdownReason.ToString());
                RememberSessionHost(runner);
                // StartGame can finish while the default scene manager is still taking over the scene.
                // Wait before spawning, otherwise scene cleanup can remove the new player.
                while (scenes != null && scenes.IsBusy && runner != null && runner.IsRunning)
                    await Task.Delay(10);
                if (this == null || Runner != runner) return false;
                await Task.Delay(50);
                if (this == null || Runner != runner || !runner.IsRunning) return false;
                SpawnLocalPlayer(runner);
                LogStage("session-ready", "mode=" + runner.GameMode + " localPlayer=" + runner.LocalPlayer);
                Status = offline ? "Prueba local — Fusion Single" : "Conectado — " + roomName;
                SetMenu(false);
                return true;
            }
            catch (Exception exception)
            {
                string message = exception.Message;
                if (settings != null && !string.IsNullOrEmpty(settings.AppIdFusion)) message = message.Replace(settings.AppIdFusion, "[redacted]");
                Status = "No se pudo iniciar: " + message;
                LogStage("start-failed", "type=" + exception.GetType().Name + " detail=" + message);
                if (runner != null) await runner.Shutdown();
                if (Runner == runner) Runner = null;
                SetMenu(true);
                return false;
            }
            finally { Busy = false; }
        }

        private Coroutine pendingPlayerSpawn;
        private NetworkRunner pendingPlayerRunner;

        public static bool CanSpawnNow(NetworkRunner runner)
        {
            if (runner == null || !runner.IsRunning || !runner.CanSpawn || runner.IsSceneManagerBusy) return false;
            // In Shared, GetNextId reads Client.ServerConnection.Counter. CanSpawn
            // only checks topology and remains true even without that connection.
            if (runner.GameMode == GameMode.Shared)
                return runner.IsConnectedToServer && runner.IsInSession
                    && runner.LocalPlayer.IsRealPlayer && runner.IsPlayerValid(runner.LocalPlayer);
            return true;
        }

        public void SpawnLocalPlayer(NetworkRunner runner)
        {
            if (runner == null || runner != Runner || playerPrefab == null) return;
            if (pendingPlayerSpawn != null && pendingPlayerRunner == runner) return;
            if (pendingPlayerSpawn != null) StopCoroutine(pendingPlayerSpawn);
            pendingPlayerRunner = runner;
            pendingPlayerSpawn = StartCoroutine(WaitForPlayerSpawn(runner));
        }

        private System.Collections.IEnumerator WaitForPlayerSpawn(NetworkRunner runner)
        {
            // Always yield before completion, so the returned Coroutine handle is valid.
            yield return null;
            float deadline = Time.realtimeSinceStartup + 90f;
            int readyFrames = 0;
            try
            {
                while (runner != null && runner == Runner && runner.IsRunning)
                {
                    readyFrames = CanSpawnNow(runner) ? readyFrames + 1 : 0;
                    if (readyFrames >= 2)
                    {
                        SpawnLocalPlayerNow(runner);
                        yield break;
                    }
                    if (Time.realtimeSinceStartup >= deadline)
                    {
                        LogStage("spawn-timeout", "serverConnected=" + runner.IsConnectedToServer
                            + " inSession=" + runner.IsInSession + " localPlayer=" + runner.LocalPlayer);
                        yield break;
                    }
                    yield return null;
                }
                LogStage("spawn-cancelled", "runner-stopped-or-replaced");
            }
            finally { pendingPlayerSpawn = null; pendingPlayerRunner = null; }
        }

        private void SpawnLocalPlayerNow(NetworkRunner runner)
        {
            if (runner != Runner || !CanSpawnNow(runner) || playerPrefab == null)
            {
                LogStage("spawn-deferred", "runner-not-ready");
                return;
            }
            var existing = runner.GetPlayerObject(runner.LocalPlayer);
            var position = spawnPoint != null ? spawnPoint.position : transform.position;
            var offset = UnityEngine.Random.insideUnitCircle * 1.5f;
            position += new Vector3(offset.x, 0, offset.y);
            if (Physics.Raycast(position + Vector3.up * groundProbeHeight, Vector3.down, out var hit, groundProbeHeight + 100f, 1, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y + .1f;
            if (existing != null)
            {
                // The player survived a scene change: move it to this scene's spawn instead of spawning twice.
                if (spawnPoint != null && existing.HasStateAuthority && existing.TryGetComponent<Fusion.Addons.SimpleKCC.SimpleKCC>(out var kcc))
                {
                    kcc.SetPosition(position);
                    kcc.SetLookRotation(spawnPoint.rotation);
                }
                LogStage("player-already-spawned");
                return;
            }
            var player = runner.Spawn(playerPrefab, position, spawnPoint != null ? spawnPoint.rotation : Quaternion.identity, runner.LocalPlayer);
            if (player == null) { LogStage("spawn-deferred", "spawn-returned-null"); return; }
            runner.SetPlayerObject(runner.LocalPlayer, player);
            LogStage("player-spawned", "valid=" + (player != null && player.IsValid));
        }

        public async Task Disconnect()
        {
            if (Busy) return;
            Busy = true;
            var runner = Runner;
            DetachSessionEvents(runner);
            try { if (runner != null) await runner.Shutdown(); }
            finally
            {
                Runner = null; Busy = false; SetMenu(true);
                // Fusion removes scene NetworkObjects on shutdown. Restore them before another session.
                if (Application.isPlaying)
                {
                    int lobbyIndex = SceneUtility.GetBuildIndexByScenePath("Assets/00_Scenes/j_LobyDeEspera.unity");
                    if (lobbyIndex >= 0)
                        SceneManager.LoadScene(lobbyIndex);
                    else
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }
        }

        private void AttachSessionEvents(NetworkRunner runner)
        {
            var events = runner.GetComponent<NetworkEvents>();
            if (events == null) return;
            events.OnShutdown.RemoveListener(OnShutdown);
            events.PlayerJoined.RemoveListener(OnPlayerJoined);
            events.PlayerLeft.RemoveListener(OnPlayerLeft);
            events.OnShutdown.AddListener(OnShutdown);
            events.PlayerJoined.AddListener(OnPlayerJoined);
            events.PlayerLeft.AddListener(OnPlayerLeft);
            RememberSessionHost(runner);
        }

        private void DetachSessionEvents(NetworkRunner runner)
        {
            if (runner == null) return;
            var events = runner.GetComponent<NetworkEvents>();
            if (events == null) return;
            events.OnShutdown.RemoveListener(OnShutdown);
            events.PlayerJoined.RemoveListener(OnPlayerJoined);
            events.PlayerLeft.RemoveListener(OnPlayerLeft);
        }

        private void RememberSessionHost(NetworkRunner runner)
        {
            if (runner == null || !runner.IsRunning || runner.GameMode != GameMode.Shared) return;
            PlayerRef master = runner.GetMasterClient();
            if (master.IsRealPlayer) sessionHost = master;
        }

        private void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner == null || runner != Runner) return;
            RememberSessionHost(runner);
        }

        /// <summary>
        /// En modo compartido el anfitrion es el Master Client. Si se va, la sala no se cierra sola:
        /// el resto vuelve al lobby y la partida termina.
        /// </summary>
        private void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (hostDepartureHandled || runner == null || runner != Runner || runner.GameMode != GameMode.Shared) return;
            if (!sessionHost.IsRealPlayer || player != sessionHost) return;
            hostDepartureHandled = true;
            shutdownMessage = "El anfitrión salió de la partida. Volviendo al lobby...";
            Status = shutdownMessage;
            LogStage("host-left", "player=" + player);
            StartCoroutine(ReturnLobbyAfterHostLeft());
        }

        private System.Collections.IEnumerator ReturnLobbyAfterHostLeft()
        {
            yield return null;
            if (this != null && Runner != null) _ = Disconnect();
        }

        private void OnShutdown(NetworkRunner runner, ShutdownReason reason)
        {
            LogStage("shutdown", "reason=" + reason);
            if (Runner != runner) return;
            Runner = null;
            Status = "Sesión cerrada: " + reason;
            shutdownMessage = Status;
            SetMenu(true);
            if (Application.isPlaying)
            {
                string curScene = SceneManager.GetActiveScene().name;
                if (!curScene.Contains("Loby") && !curScene.Contains("Lobby"))
                {
                    int lobbyIndex = SceneUtility.GetBuildIndexByScenePath("Assets/00_Scenes/j_LobyDeEspera.unity");
                    if (lobbyIndex >= 0) StartCoroutine(ReturnToLobbyAfterShutdown(lobbyIndex));
                }
            }
        }

        private System.Collections.IEnumerator ReturnToLobbyAfterShutdown(int lobbyIndex)
        {
            // Leave Fusion's shutdown/update callback before unloading its scene objects.
            yield return null;
            if (Application.isPlaying && Runner == null) SceneManager.LoadSceneAsync(lobbyIndex);
        }

        public void SetMenu(bool visible)
        {
            MenuOpen = visible;
            if (GameJam.UI.SciFiGameMenuManager.Instance != null)
            {
                GameJam.UI.SciFiGameMenuManager.Instance.ApplySessionMenuState(visible, Runner != null && Runner.IsRunning);
            }
            else
            {
                Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = visible;
            }
        }

        private void Update()
        {
            if (SceneTransitionVideo.IsBlockingInput || PortalWhiteTransition.IsBlockingInput) return;
            if (GameJam.UI.SciFiGameMenuManager.Instance == null && Runner != null && Runner.IsRunning && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (MarsFusionAvatar.Local != null && MarsFusionAvatar.Local.LocalHud != null && MarsFusionAvatar.Local.LocalHud.ConsumeEscape()) return;
                SetMenu(!MenuOpen);
            }
        }

        private void OnGUI()
        {
            bool sciFiMenu = GameJam.UI.SciFiGameMenuManager.Instance != null || FindFirstObjectByType<GameJam.UI.SciFiGameMenuManager>() != null;
            // El menu SciFi solo ofrece iniciar sesion en el Lobby; en escenas de mision sin sesion
            // se muestran estos botones para poder probar la escena directamente con Play.
            bool needsStartButtons = Runner == null && !GameJam.UI.SciFiGameMenuManager.IsLobbyScene;
            if (sciFiMenu && !needsStartButtons) return;
            if (!MenuOpen) return;
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(460, Screen.width - 40), 320), GUI.skin.box);
            GUILayout.Label(planetLabel + " · PHOTON FUSION");
            GUILayout.Label(Status);
            GUI.enabled = !Busy;
            if (Runner == null)
            {
                GUILayout.Label("Nombre de sala (igual en ambos clientes)");
                roomName = GUILayout.TextField(roomName, 48);
                if (GUILayout.Button("Prueba local · sin App ID", GUILayout.Height(36))) _ = StartSession(true);
                if (GUILayout.Button("Crear / entrar a sala online", GUILayout.Height(36))) _ = StartSession(false);
            }
            else
            {
                if (GUILayout.Button("Continuar", GUILayout.Height(36))) SetMenu(false);
                if (GUILayout.Button("Desconectar", GUILayout.Height(36))) _ = Disconnect();
            }
            GUI.enabled = true;
            GUILayout.Label("WASD · Ratón · Espacio · Shift\nTab: tablet  /  Mantener V: hablar  /  H: gesto de dolor\nEsc: menú de conexión");
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            DetachSessionEvents(Runner);
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
            Application.runInBackground = previousBackground;
        }
    }
}
