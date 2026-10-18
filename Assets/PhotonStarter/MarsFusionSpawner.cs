using Fusion;
using UnityEngine;

namespace GameJam.Multiplayer
{
    public sealed class MarsFusionSpawner : NetworkBehaviour
    {
        public override void Spawned()
        {
            var session = FindFirstObjectByType<MarsFusionSession>();
            // The session owns one cancellable request for both StartGame and scene arrival.
            if (session != null) session.SpawnLocalPlayer(Runner);
        }
    }
}
