using Fusion;
using UnityEngine;

namespace GameJam.Multiplayer
{
    // Match Fusion Starter: spawn once the scene NetworkObject is attached to the runner.
    public sealed class MarsFusionSpawner : NetworkBehaviour
    {
        public override void Spawned()
        {
            var session = FindFirstObjectByType<MarsFusionSession>();
            if (session != null) session.StartCoroutine(SpawnAfterSceneReady(session, Runner));
        }

        private static System.Collections.IEnumerator SpawnAfterSceneReady(MarsFusionSession session, NetworkRunner runner)
        {
            yield return null;
            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            while (runner != null && runner.IsRunning && sceneManager != null && sceneManager.IsBusy) yield return null;
            yield return null;
            if (session != null && runner != null && runner.IsRunning) session.SpawnLocalPlayer(runner);
        }
    }
}
