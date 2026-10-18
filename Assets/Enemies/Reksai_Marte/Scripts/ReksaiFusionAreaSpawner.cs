using Fusion;
using UnityEngine;

public sealed class ReksaiFusionAreaSpawner : NetworkBehaviour
{
    public NetworkObject enemyPrefab;
    [Networked] private NetworkBool DidSpawn { get; set; }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || DidSpawn || enemyPrefab == null) return;
        Runner.Spawn(enemyPrefab, transform.position, transform.rotation);
        DidSpawn = true;
    }
}
