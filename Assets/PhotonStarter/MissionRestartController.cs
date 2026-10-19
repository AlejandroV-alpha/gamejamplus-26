using System;
using Fusion;
using UnityEngine;

namespace GameJam.Multiplayer
{
    // Lives on the persistent runner so unloading the mission cannot stop its reload.
    public sealed class MissionRestartController : MonoBehaviour
    {
        private NetworkRunner runner;
        private NetworkId preparedFor;
        private bool prepared, loading;
        private string failure;
        private int sceneIndex;
        public bool IsRestarting => prepared || loading;

        public static MissionRestartController Ensure(NetworkRunner runner)
        {
            var controller = runner.GetComponent<MissionRestartController>();
            if (controller == null) controller = runner.gameObject.AddComponent<MissionRestartController>();
            controller.runner = runner;
            return controller;
        }

        public static bool IsMissionScene(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/00_Scenes/", StringComparison.Ordinal)
                && path.IndexOf("Loby", StringComparison.OrdinalIgnoreCase) < 0
                && path.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public void Prepare(NetworkId coordinator)
        {
            if (prepared && preparedFor == coordinator) return;
            preparedFor = coordinator;
            prepared = true;
            failure = null;
            // Each peer removes only objects it owns. Scene-authored objects reset on reload.
            // Despawning the whole avatar resets inventory, health, ammo and all pending actions.
            foreach (var obj in runner.GetAllNetworkObjects())
                if (obj != null && obj.IsValid && obj.HasStateAuthority && !obj.NetworkTypeId.IsSceneObject
                    && !obj.NetworkTypeId.IsPrefabNestedObject) runner.Despawn(obj);
            Debug.Log("[Mission] Estado local limpiado: jugadores, inventarios y objetos generados.", this);
        }

        public async void Reload(int buildIndex)
        {
            if (loading || runner == null || !runner.IsRunning || !runner.IsSceneAuthority) return;
            loading = true;
            sceneIndex = buildIndex;
            try
            {
                var scene = SceneRef.FromIndex(buildIndex);
                await runner.UnloadScene(scene);
                if (runner == null || !runner.IsRunning || !runner.IsSceneAuthority) return;
                await runner.LoadScene(scene, UnityEngine.SceneManagement.LoadSceneMode.Single);
                Debug.Log("[Mission] Nivel recargado desde cero sin cerrar la sesión.", this);
            }
            catch (Exception exception)
            {
                failure = "No se pudo recargar el nivel: " + exception.Message;
                Debug.LogError("[Mission] " + failure, this);
            }
            finally { loading = false; }
        }

        private void Update()
        {
            if (!prepared || loading || runner == null || !runner.IsRunning || runner.IsSceneManagerBusy) return;
            var player = runner.GetPlayerObject(runner.LocalPlayer);
            if (player != null && player.IsValid && player.TryGetComponent<GameJam.Combat.PlayerHealth>(out var health)
                && health.MaxHealth > 0f && health.IsAlive) { prepared = false; failure = null; }
        }

        private void OnGUI()
        {
            if (!IsRestarting) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width * .5f - 300f, Screen.height * .4f, 600f, 90f),
                string.IsNullOrEmpty(failure) ? "REINICIANDO MISIÓN\nRestableciendo nivel e inventarios…" : failure, style);
            if (!string.IsNullOrEmpty(failure) && runner != null && runner.IsSceneAuthority
                && GUI.Button(new Rect(Screen.width * .5f - 100f, Screen.height * .4f + 95f, 200f, 35f), "Reintentar"))
                Reload(sceneIndex);
        }
    }
}
