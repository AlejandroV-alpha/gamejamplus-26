using Fusion;
using UnityEngine;

public sealed class ReksaiFusionAreaSpawner : NetworkBehaviour
{
    public NetworkObject enemyPrefab;
    [Min(0f), Tooltip("0 spawns immediately; POI guards wait until a player approaches.")]
    public float activationDistance;
    private float nextActivationCheck;
    [Networked] private NetworkBool DidSpawn { get; set; }

    public override void FixedUpdateNetwork()
    {
        if (!GameJam.Multiplayer.MarsFusionSession.CanSpawnNow(Runner)
            || Object == null || !Object.IsValid || !StateBufferIsValid) return;
        if (!HasStateAuthority || DidSpawn || enemyPrefab == null) return;
        if (activationDistance > 0)
        {
            if (Runner.SimulationTime < nextActivationCheck) return;
            nextActivationCheck = Runner.SimulationTime + .5f;
            bool nearby = false;
            foreach (var player in FindObjectsByType<GameJam.Multiplayer.MarsFusionAvatar>(FindObjectsSortMode.None))
                if (player.Object != null && player.Object.IsValid && player.Runner == Runner && player.IsAlive
                    && (player.transform.position - transform.position).sqrMagnitude <= activationDistance * activationDistance)
                { nearby = true; break; }
            if (!nearby) return;
        }
        var enemy = Runner.Spawn(enemyPrefab, transform.position, transform.rotation);
        if (enemy != null) DidSpawn = true;
    }
}
