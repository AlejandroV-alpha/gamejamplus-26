using Fusion;
using UnityEngine;
using System.Collections.Generic;
using GameJam.Combat;
using GameJam.Ship;

namespace GameJam.Multiplayer
{
    public sealed class MarsFusionSpawner : NetworkBehaviour
    {
        [Networked] public int RestartPhase { get; private set; }
        [Networked] private TickTimer RestartDeadline { get; set; }
        private readonly HashSet<PlayerRef> preparedPlayers = new HashSet<PlayerRef>();
        private float nextPrepareRequest;
        private bool reloadRequested;

        public override void Spawned()
        {
            MissionRestartController.Ensure(Runner);
            var session = FindFirstObjectByType<MarsFusionSession>();
            // The session owns one cancellable request for both StartGame and scene arrival.
            if (session != null) session.SpawnLocalPlayer(Runner);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !Runner.IsSceneAuthority || Runner.IsSceneManagerBusy
                || !MissionRestartController.IsMissionScene(gameObject.scene.path)) return;
            if (RestartPhase == 2)
            {
                bool ready = true;
                int count = 0;
                foreach (var player in Runner.ActivePlayers) { count++; ready &= preparedPlayers.Contains(player); }
                if (ready && count > 0 && !reloadRequested)
                {
                    reloadRequested = true;
                    MissionRestartController.Ensure(Runner).Reload(gameObject.scene.buildIndex);
                }
                else if (!reloadRequested && Time.unscaledTime >= nextPrepareRequest)
                {
                    nextPrepareRequest = Time.unscaledTime + 1f;
                    RPC_PrepareRestart();
                }
                return;
            }
            // A player without a spawned, initialized health source is still loading, not dead.
            bool allDead = true;
            int participants = 0;
            foreach (var player in Runner.ActivePlayers)
            {
                participants++;
                if (!Runner.TryGetPlayerObject(player, out var actor) || actor == null || !actor.IsValid)
                { allDead = false; break; }
                var health = actor.GetComponent<PlayerHealth>();
                if (health == null || health.MaxHealth <= 0f || health.IsAlive) { allDead = false; break; }
            }
            if (allDead && participants > 0)
                foreach (var station in FindObjectsByType<ShipRepairStation>(FindObjectsSortMode.None))
                    if (station.Runner == Runner && station.IsRepaired()) allDead = false;
            if (!allDead || participants == 0)
            {
                RestartPhase = 0;
                RestartDeadline = TickTimer.None;
                return;
            }
            if (RestartPhase == 0)
            {
                RestartPhase = 1;
                RestartDeadline = TickTimer.CreateFromSeconds(Runner, 3f);
                Debug.Log("[Mission] Todo el equipo ha caído. Reiniciando el nivel en 3 segundos.", this);
            }
            else if (RestartDeadline.Expired(Runner))
            {
                RestartPhase = 2;
                preparedPlayers.Clear();
                nextPrepareRequest = Time.unscaledTime + 1f;
                RPC_PrepareRestart();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PrepareRestart()
        {
            MissionRestartController.Ensure(Runner).Prepare(Object.Id);
            RPC_RestartPrepared();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RestartPrepared(RpcInfo info = default)
        {
            if (RestartPhase != 2) return;
            var source = info.IsInvokeLocal ? Runner.LocalPlayer : info.Source;
            if (Runner.IsPlayerValid(source)) preparedPlayers.Add(source);
        }

        private void OnGUI()
        {
            if (Object == null || !Object.IsValid || RestartPhase != 1) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width * .5f - 290f, Screen.height * .4f, 580f, 90f),
                "TODO EL EQUIPO HA CAÍDO\nReiniciando este nivel en "
                + Mathf.CeilToInt(RestartDeadline.RemainingTime(Runner).GetValueOrDefault()) + " s…", style);
        }
    }
}
