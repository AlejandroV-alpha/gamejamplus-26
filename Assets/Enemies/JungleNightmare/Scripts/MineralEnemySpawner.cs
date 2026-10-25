using System.Collections.Generic;
using System.Linq;
using Fusion;
using GameJam.Collectibles;
using GameJam.Multiplayer;
using UnityEngine;

/// <summary>
/// Agrupa los minerales del mapa en zonas y pone en cada una un grupo de guardianes
/// (2 medianos y 1 grande). Cuando uno muere, la zona lo repone al cabo de un tiempo.
/// </summary>
public sealed class MineralEnemySpawner : NetworkBehaviour
{
    [SerializeField] NetworkObject mediumPrefab;
    [SerializeField] NetworkObject largePrefab;
    [SerializeField, Min(0)] int mediumPerZone = 2;
    [SerializeField, Min(0)] int largePerZone = 1;
    [SerializeField, Min(1)] int zoneLimit = 30;
    [SerializeField, Min(3f)] float zoneRadius = 15f;
    [SerializeField, Min(2f), Tooltip("Segundos hasta reponer un guardian muerto.")] float respawnDelaySeconds = 40f;
    [SerializeField, Min(0f)] float minDistanceFromPlayers = 15f;
    [SerializeField, Tooltip("Distancia minima y maxima al mineral (m).")] Vector2 spawnRingAroundMineral = new Vector2(1.5f, 3.5f);

    sealed class Guard { public bool large; public NetworkObject enemy; public float respawnAt; }
    sealed class Zone { public List<Vector3> minerals = new List<Vector3>(); public Vector3 center; public List<Guard> guards = new List<Guard>(); }

    readonly List<Zone> zones = new List<Zone>();
    const int GuardCapacity = 128;
    [Networked] bool PopulationInitialized { get; set; }
    [Networked] int GuardCount { get; set; }
    [Networked, Capacity(GuardCapacity)] NetworkArray<NetworkObject> GuardObjects => default;
    [Networked, Capacity(GuardCapacity)] NetworkArray<Vector3> GuardAnchors => default;
    [Networked, Capacity(GuardCapacity)] NetworkArray<NetworkBool> GuardIsLarge => default;
    [Networked, Capacity(GuardCapacity)] NetworkArray<float> GuardRespawnAt => default;

    public override void Spawned()
    {
        if (HasStateAuthority && !PopulationInitialized) InitializePopulation();
    }

    void InitializePopulation()
    {
        zones.Clear();
        // Los minerales se recogen y desaparecen: las zonas se calculan una vez al inicio.
        var points = FindObjectsByType<MaterialPickup>(FindObjectsSortMode.None)
            .Where(p => p.gameObject.scene == gameObject.scene && !p.name.StartsWith("Damaged"))
            .Select(p => p.transform.position).ToList();
        // Tambien los cristales decorativos del mapa (azules, violetas) cuentan como minerales.
        points.AddRange(gameObject.scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>())
            .Where(t => t.name.IndexOf("crystal", System.StringComparison.OrdinalIgnoreCase) >= 0
                && t.GetComponentInParent<MaterialPickup>() == null && t.GetComponent<Renderer>() != null)
            .Select(t => t.position));
        BuildZones(points);
        // Al entrar a la partida los guardianes ya estan en su sitio.
        float now = Runner.SimulationTime;
        foreach (var zone in zones)
        {
            // Zonas chicas (1-2 minerales) llevan solo medianos; las de 3+ llevan el grupo completo.
            int large = zone.minerals.Count >= 3 ? largePerZone : 0;
            int medium = Mathf.Min(mediumPerZone, zone.minerals.Count);
            for (int i = 0; i < large; i++) zone.guards.Add(new Guard { large = true, respawnAt = now });
            for (int i = 0; i < medium; i++) zone.guards.Add(new Guard { large = false, respawnAt = now });
        }
        GuardCount = 0;
        foreach (var zone in zones)
            foreach (var guard in zone.guards)
            {
                if (GuardCount >= GuardCapacity) break;
                GuardAnchors.Set(GuardCount, zone.minerals[Random.Range(0, zone.minerals.Count)]);
                GuardIsLarge.Set(GuardCount, guard.large);
                GuardRespawnAt.Set(GuardCount, now);
                GuardCount++;
            }
        PopulationInitialized = true;
        initialWave = true;
        Debug.Log($"[MineralEnemySpawner] {points.Count} minerales en {zones.Count} zonas.", this);
    }

    /// <summary>
    /// Une minerales cercanos en zonas y se queda con las mas separadas entre si.
    /// </summary>
    void BuildZones(List<Vector3> points)
    {
        var all = new List<Zone>();
        foreach (var p in points)
        {
            var zone = all.FirstOrDefault(z => (z.center - p).sqrMagnitude < zoneRadius * zoneRadius);
            if (zone == null) { zone = new Zone(); all.Add(zone); }
            zone.minerals.Add(p);
            zone.center = zone.minerals.Aggregate(Vector3.zero, (a, b) => a + b) / zone.minerals.Count;
        }

        // Primero la zona con mas minerales; luego siempre la mas lejana de las ya elegidas.
        var remaining = all.OrderByDescending(z => z.minerals.Count).ToList();
        while (remaining.Count > 0 && zones.Count < zoneLimit)
        {
            var next = zones.Count == 0 ? remaining[0]
                : remaining.OrderByDescending(z => zones.Min(c => (c.center - z.center).sqrMagnitude)).First();
            zones.Add(next);
            remaining.Remove(next);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        float now = Runner.SimulationTime;
        if (!PopulationInitialized) InitializePopulation();
        for (int i = 0; i < GuardCount; i++)
        {
            var enemy = GuardObjects.Get(i);
            if (enemy != null && enemy.IsValid) continue;
            // Keep the respawn deadline in replicated state so a new master continues the same population.
            if (GuardRespawnAt.Get(i) < 0)
            {
                GuardObjects.Set(i, null);
                GuardRespawnAt.Set(i, now + respawnDelaySeconds);
                continue;
            }
            if (now < GuardRespawnAt.Get(i)) continue;
            var prefab = GuardIsLarge.Get(i) ? largePrefab : mediumPrefab;
            if (prefab == null || !TryPickSpot(GuardAnchors.Get(i), out var position)) { GuardRespawnAt.Set(i, now + 5f); continue; }
            GuardObjects.Set(i, Runner.Spawn(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
            GuardRespawnAt.Set(i, -1f);
        }
        initialWave = false;
    }

    bool initialWave;

    bool TryPickSpot(Vector3 mineral, out Vector3 position)
    {
        var players = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .Where(a => a.Object != null && a.Object.IsValid && a.Runner == Runner)
            .Select(a => a.transform.position).ToArray();

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(spawnRingAroundMineral.x, spawnRingAroundMineral.y);
            Vector3 candidate = Ground(mineral + new Vector3(ring.x, 0f, ring.y));
            if (!initialWave && players.Any(p => (p - candidate).sqrMagnitude < minDistanceFromPlayers * minDistanceFromPlayers)) continue;
            position = candidate;
            return true;
        }
        position = default;
        return false;
    }

    static Vector3 Ground(Vector3 point)
    {
        float best = float.MaxValue;
        Vector3 result = point;
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 8f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<MaterialPickup>() != null || hit.distance >= best) continue;
            best = hit.distance;
            result = hit.point;
        }
        return result;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.4f, .85f, 1f, .8f);
        foreach (var pickup in FindObjectsByType<MaterialPickup>(FindObjectsSortMode.None))
            Gizmos.DrawWireSphere(pickup.transform.position, spawnRingAroundMineral.y);
    }
}
