using System.Collections.Generic;
using Fusion;
using GameJam.Combat;
using GameJam.Multiplayer;
using UnityEngine;

namespace GameJam.Waves
{
    /// <summary>
    /// Oleadas de monstruos por tiempo. Calma (explorar y recolectar) -> aviso -> oleada alrededor
    /// de los jugadores -> premio al limpiarla. Cada oleada trae mas bichos y la calma se acorta.
    /// La autoridad decide y spawnea; el HUD (WaveHud) solo lee el estado replicado.
    /// </summary>
    public sealed class WaveDirector : NetworkBehaviour
    {
        public enum Phase : byte { Calm, Warning, Active, Cleared }

        [Header("Enemigos que pueden venir")]
        [SerializeField] private NetworkObject[] enemyPrefabs;
        [SerializeField, Tooltip("Desde que oleada aparece cada prefab (mismo orden).")] private int[] unlockWave;
        [Header("Ritmo")]
        [SerializeField, Min(5f)] private float firstCalm = 45f;
        [SerializeField, Min(5f)] private float calmSeconds = 60f;
        [SerializeField, Min(5f)] private float minCalmSeconds = 30f;
        [SerializeField, Min(2f)] private float warningSeconds = 6f;
        [SerializeField, Min(20f)] private float maxWaveSeconds = 150f;
        [Header("Tamaño")]
        [SerializeField, Min(1)] private int baseCount = 5;
        [SerializeField, Min(1)] private int maxAlive = 14;
        [SerializeField] private Vector2 spawnRing = new Vector2(24f, 40f);
        [Header("Premio al limpiar")]
        [SerializeField] private NetworkObject[] rewardPrefabs;
        [SerializeField] private Transform rewardPoint;

        [Networked] public Phase Current { get; private set; }
        [Networked] public float PhaseStart { get; private set; }
        [Networked] public float PhaseEnd { get; private set; }
        [Networked] public int Wave { get; private set; }
        [Networked] public int Total { get; private set; }
        [Networked] public int Remaining { get; private set; }
        [Networked] private Vector3 Focus { get; set; }

        public static WaveDirector Instance { get; private set; }
        /// <summary>Punto aproximado por donde llega la oleada (para la flecha del aviso).</summary>
        public Vector3 IncomingFrom => Focus;
        public float Now => Runner != null ? Runner.SimulationTime : 0f;

        private readonly List<NetworkObject> spawned = new List<NetworkObject>();
        private int toSpawn, spawnIndex;
        private float nextSpawnAt;
        private readonly List<Vector3> fronts = new List<Vector3>();

        public override void Spawned()
        {
            Instance = this;
            EnemyAggro.Reset();
            if (HasStateAuthority) Enter(Phase.Calm, firstCalm);
        }

        public override void Despawned(NetworkRunner runner, bool hasState) { if (Instance == this) Instance = null; }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            float now = Runner.SimulationTime;
            switch (Current)
            {
                case Phase.Calm:
                    if (now >= PhaseEnd && AnyPlayer(out var p))
                    {
                        Wave++;
                        AnnouncedAt = p.transform.position;
                        Focus = PickApproach(p.transform.position);
                        Enter(Phase.Warning, warningSeconds);
                    }
                    break;

                case Phase.Warning:
                    if (now >= PhaseEnd)
                    {
                        int players = Mathf.Max(1, CountPlayers());
                        // Mas bichos por oleada y por jugador: 5, 8, 11... (+3 por cada jugador extra).
                        toSpawn = baseCount + (Wave - 1) * 3 + (players - 1) * 3;
                        spawnIndex = 0;
                        // Desde la oleada 2 llegan por varios frentes a la vez.
                        fronts.Clear();
                        fronts.Add(Focus);
                        int extra = Mathf.Min(2, (Wave - 1) / 2 + (players > 2 ? 1 : 0));
                        for (int i = 0; i < extra; i++) fronts.Add(Quaternion.Euler(0, 120f * (i + 1), 0) * (Focus - p0()) + p0());
                        Total = toSpawn;
                        Remaining = toSpawn;
                        spawned.Clear();
                        nextSpawnAt = now;
                        Enter(Phase.Active, maxWaveSeconds);
                    }
                    break;

                case Phase.Active:
                    // Llegan en grupos de 2-3 cada ~1.4 s: una avalancha mezclada.
                    int cap = maxAlive + (CountPlayers() - 1) * 3;
                    if (toSpawn > 0 && now >= nextSpawnAt && Alive() < cap)
                    {
                        int group = Mathf.Min(toSpawn, Random.Range(2, 4));
                        for (int i = 0; i < group; i++) if (SpawnOne()) toSpawn--;
                        nextSpawnAt = now + 1.4f;
                    }
                    Remaining = toSpawn + Alive();
                    if (Remaining <= 0)
                    {
                        GiveReward();
                        Enter(Phase.Cleared, 5f);
                    }
                    else if (now >= PhaseEnd) Enter(Phase.Calm, NextCalm());   // se acabo el tiempo: los que queden siguen ahi
                    break;

                case Phase.Cleared:
                    if (now >= PhaseEnd) Enter(Phase.Calm, NextCalm());
                    break;
            }
        }

        // Centro de referencia de los frentes: donde estaba el jugador al anunciar la oleada.
        [Networked] private Vector3 AnnouncedAt { get; set; }
        private Vector3 p0() => AnnouncedAt;
        /// <summary>Todos los frentes por donde llega la oleada actual (para el HUD).</summary>
        public Vector3 FrontPoint(int i) => Quaternion.Euler(0, 120f * i, 0) * (Focus - AnnouncedAt) + AnnouncedAt;
        public int FrontCount => Mathf.Clamp(1 + Mathf.Min(2, (Wave - 1) / 2), 1, 3);

        private float NextCalm() => Mathf.Max(minCalmSeconds, calmSeconds - (Wave - 1) * 6f);

        private void Enter(Phase phase, float seconds)
        {
            Current = phase;
            PhaseStart = Runner.SimulationTime;
            PhaseEnd = Runner.SimulationTime + seconds;
        }

        private int Alive()
        {
            int n = 0;
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                var o = spawned[i];
                if (o == null || !o.IsValid) { spawned.RemoveAt(i); continue; }
                var ui = o.GetComponent<EnemyCombatUI>();
                if (ui != null && ui.IsDead) { spawned.RemoveAt(i); continue; }
                n++;
            }
            return n;
        }

        private bool SpawnOne()
        {
            var prefab = PickEnemy();
            if (prefab == null || !AnyPlayer(out var target)) return false;
            // Desde uno de los frentes anunciados, abierto en abanico.
            Vector3 center = target.transform.position;
            Vector3 front = fronts.Count > 0 ? fronts[spawnIndex % fronts.Count] : Focus;
            Vector3 dir = front - p0(); dir.y = 0;
            if (dir.sqrMagnitude < .01f) dir = Vector3.forward;
            float angle = Random.Range(-55f, 55f);
            Vector3 flat = Quaternion.Euler(0, angle, 0) * dir.normalized * Random.Range(spawnRing.x, spawnRing.y);
            if (!Ground(center + flat, out var pos)) return false;
            var face = Quaternion.LookRotation(Flat(center - pos));
            var obj = Runner.Spawn(prefab, pos, face);
            if (obj != null) { spawned.Add(obj); EnemyAggro.MarkWave(obj); }
            return obj != null;
        }

        private NetworkObject PickEnemy()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0) return null;
            // Rotacion por tipos: cada oleada mezcla todos los desbloqueados (terrestres, voladores, gusanos...).
            var pool = new List<NetworkObject>();
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                int unlock = unlockWave != null && i < unlockWave.Length ? unlockWave[i] : 1;
                if (enemyPrefabs[i] != null && Wave >= unlock) pool.Add(enemyPrefabs[i]);
            }
            if (pool.Count == 0) return enemyPrefabs[0];
            int pick = (spawnIndex++ + Wave) % pool.Count;
            return Random.value < .25f ? pool[Random.Range(0, pool.Count)] : pool[pick];
        }

        private Vector3 PickApproach(Vector3 around)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            return around + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * spawnRing.y;
        }

        private void GiveReward()
        {
            if (rewardPrefabs == null || rewardPrefabs.Length == 0) return;
            Vector3 at = rewardPoint != null ? rewardPoint.position : transform.position;
            int count = 2 + Mathf.Min(Wave, 6);
            for (int i = 0; i < count; i++)
            {
                var prefab = rewardPrefabs[Random.Range(0, rewardPrefabs.Length)];
                if (prefab == null) continue;
                Vector2 r = Random.insideUnitCircle * 2.2f;
                Runner.Spawn(prefab, at + new Vector3(r.x, 1.5f, r.y), Quaternion.Euler(0, Random.Range(0, 360f), 0));
            }
        }

        private bool AnyPlayer(out MarsFusionAvatar player)
        {
            player = null;
            var list = new List<MarsFusionAvatar>();
            foreach (var a in FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None))
                if (a.Object != null && a.Object.IsValid && a.Runner == Runner && a.IsAlive) list.Add(a);
            if (list.Count == 0) return false;
            player = list[Random.Range(0, list.Count)];
            return true;
        }

        private int CountPlayers()
        {
            int n = 0;
            foreach (var a in FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None))
                if (a.Object != null && a.Object.IsValid && a.Runner == Runner) n++;
            return n;
        }

        private static bool Ground(Vector3 p, out Vector3 ground)
        {
            ground = p;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 120f, Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider is TerrainCollider) { ground = h.point + Vector3.up * .3f; return true; }
            }
            if (Physics.Raycast(p + Vector3.up * 120f, Vector3.down, out var any, 300f, ~0, QueryTriggerInteraction.Ignore)
                && any.normal.y > .6f && any.collider.attachedRigidbody == null)
            { ground = any.point + Vector3.up * .3f; return true; }
            return false;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > .001f ? v : Vector3.forward; }
    }
}
