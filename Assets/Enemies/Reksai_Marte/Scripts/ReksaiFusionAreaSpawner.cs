using Fusion;
using UnityEngine;

public sealed class ReksaiFusionAreaSpawner : NetworkBehaviour
{
    public NetworkObject enemyPrefab;
    [Networked] private NetworkBool DidSpawn { get; set; }

    public override void FixedUpdateNetwork()
    {
        if (!GameJam.Multiplayer.MarsFusionSession.CanSpawnNow(Runner)
            || Object == null || !Object.IsValid || !StateBufferIsValid) return;
        if (!HasStateAuthority || DidSpawn || enemyPrefab == null) return;
        var enemy = Runner.Spawn(enemyPrefab, transform.position, transform.rotation);
        if (enemy != null) DidSpawn = true;
    }
}
