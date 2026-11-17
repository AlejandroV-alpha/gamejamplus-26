using System.Collections.Generic;
using Fusion;
using GameJam.Multiplayer;
using UnityEngine;

namespace GameJam.Waves
{
    /// <summary>
    /// Casco de la nave: lo que defienden los jugadores. Los bichos de las oleadas van a por ella
    /// y le bajan la integridad; si llega a 0, se pierde la partida (reinicio del nivel).
    /// Entre oleadas se recupera un poco sola para que una mala oleada no sea definitiva.
    /// </summary>
    public sealed class ShipObjective : NetworkBehaviour
    {
        [SerializeField] private Transform shipRoot;
        [SerializeField, Min(10)] private int maxIntegrity = 600;
        [SerializeField, Min(0f)] private float regenPerSecondOutOfWave = 2f;

        [Networked] public float Integrity { get; private set; }
        [Networked] public int HitCount { get; private set; }
        [Networked] public Vector3 LastHitPoint { get; private set; }

        public static ShipObjective Instance { get; private set; }
        public int Max => maxIntegrity;
        public float Ratio => Object != null && Object.IsValid ? Mathf.Clamp01(Integrity / maxIntegrity) : 1f;
        public bool Destroyed => Object != null && Object.IsValid && Integrity <= 0f;
        public Vector3 Center { get; private set; }
        public float Radius { get; private set; } = 10f;

        private readonly List<Collider> hull = new List<Collider>();

        public override void Spawned()
        {
            Instance = this;
            if (shipRoot == null) shipRoot = transform;
            var b = new Bounds(shipRoot.position, Vector3.zero);
            foreach (var r in shipRoot.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            Center = new Vector3(b.center.x, shipRoot.position.y, b.center.z);
            Radius = Mathf.Max(3f, Mathf.Max(b.extents.x, b.extents.z) * .85f);
            hull.AddRange(shipRoot.GetComponentsInChildren<Collider>());
            if (HasStateAuthority) Integrity = maxIntegrity;
        }

        public override void Despawned(NetworkRunner runner, bool hasState) { if (Instance == this) Instance = null; }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || Destroyed) return;
            var wave = WaveDirector.Instance;
            bool fighting = wave != null && wave.Current == WaveDirector.Phase.Active;
            if (!fighting && Integrity < maxIntegrity)
                Integrity = Mathf.Min(maxIntegrity, Integrity + regenPerSecondOutOfWave * Runner.DeltaTime);
        }

        /// <summary>Punto del casco mas cercano a quien llega, un poco por fuera, a ras de suelo.</summary>
        public Vector3 HullPoint(Vector3 from)
        {
            Vector3 dir = from - Center; dir.y = 0;
            if (dir.sqrMagnitude < .01f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 outside = Center + dir * (Radius + 6f) + Vector3.up * 1.5f;
            float best = float.MaxValue; Vector3 point = Center + dir * Radius;
            foreach (var h in Physics.RaycastAll(outside, -dir, Radius + 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsHull(h.collider) || h.distance >= best) continue;
                best = h.distance; point = h.point;
            }
            point += dir * 1.2f;
            point.y = from.y;
            return point;
        }

        public bool IsHull(Collider c) => c != null && shipRoot != null && c.transform.IsChildOf(shipRoot);

        /// <summary>Distancia horizontal al punto del casco mas cercano.</summary>
        public float DistanceToHull(Vector3 from)
        {
            Vector3 d = HullPoint(from) - from; d.y = 0;
            return d.magnitude;
        }

        public void Damage(float amount, Vector3 at)
        {
            if (Object == null || !Object.IsValid || amount <= 0 || Destroyed) return;
            RPC_Damage(amount, at);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_Damage(float amount, Vector3 at)
        {
            if (Destroyed) return;
            Integrity = Mathf.Max(0f, Integrity - amount);
            HitCount++;
            LastHitPoint = at;
        }

        // Chispas y un golpe metalico donde pegan, en todos los clientes.
        private int seenHits = -1;
        public override void Render()
        {
            if (seenHits < 0) { seenHits = HitCount; return; }
            if (HitCount == seenHits) return;
            seenHits = HitCount;
            GameJam.Combat.ImpactBurstFx.Spawn(LastHitPoint + Vector3.up * 1.2f, new Color(1f, .55f, .2f), .7f);
        }
    }

    /// <summary>
    /// Reglas comunes de objetivo para todos los enemigos:
    ///  - Quien dispara a un enemigo pasa a ser su objetivo (lo persigue aunque este lejos).
    ///  - Los bichos de oleada, si no tienen a un jugador encima, van a atacar la nave.
    /// </summary>
    public static class EnemyAggro
    {
        public const float AggroSeconds = 25f;
        public const float ShipDistraction = 14f;   // un jugador a esta distancia distrae a un bicho de oleada

        private static readonly HashSet<NetworkId> waveEnemies = new HashSet<NetworkId>();
        private static readonly Dictionary<NetworkId, Transform> anchors = new Dictionary<NetworkId, Transform>();

        /// <summary>Al empezar una partida nueva (recarga del nivel) se olvidan los bichos anteriores.</summary>
        public static void Reset()
        {
            waveEnemies.Clear();
            foreach (var t in anchors.Values) if (t != null) Object.Destroy(t.gameObject);
            anchors.Clear();
        }

        public static void MarkWave(NetworkObject o) { if (o != null) waveEnemies.Add(o.Id); }
        public static bool IsWave(NetworkObject o) => o != null && o.IsValid && waveEnemies.Contains(o.Id);

        public static ShipObjective Ship
        {
            get
            {
                var s = ShipObjective.Instance;
                return s != null && s.Object != null && s.Object.IsValid && !s.Destroyed ? s : null;
            }
        }

        /// <summary>¿Este enemigo deberia ir a por la nave?</summary>
        public static bool WantsShip(NetworkObject self) => Ship != null && IsWave(self);

        /// <summary>Un transform que sigue el punto del casco mas cercano (para IAs que persiguen transforms).</summary>
        public static Transform ShipAnchor(NetworkObject self, Vector3 from)
        {
            var ship = Ship;
            if (ship == null || self == null) return null;
            if (!anchors.TryGetValue(self.Id, out var t) || t == null)
            {
                t = new GameObject("ShipAttackPoint").transform;
                anchors[self.Id] = t;
            }
            t.position = ship.HullPoint(from);
            return t;
        }

        public static void Forget(NetworkObject self)
        {
            if (self == null) return;
            waveEnemies.Remove(self.Id);
            if (anchors.TryGetValue(self.Id, out var t) && t != null) Object.Destroy(t.gameObject);
            anchors.Remove(self.Id);
        }

        /// <summary>El astronauta que disparo (por quien envio el RPC); si no se sabe, el mas cercano.</summary>
        public static MarsFusionAvatar FindShooter(NetworkRunner runner, PlayerRef source, Vector3 near)
        {
            MarsFusionAvatar best = null; float bestD = float.MaxValue;
            bool shared = runner != null && runner.GameMode == GameMode.Shared;
            foreach (var a in Object.FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None))
            {
                if (a.Object == null || !a.Object.IsValid || a.Runner != runner || !a.IsAlive) continue;
                if (source != PlayerRef.None && ((shared ? a.Object.StateAuthority : a.Object.InputAuthority) == source
                    || a.Object.StateAuthority == source || a.Object.InputAuthority == source)) return a;
                float d = (a.transform.position - near).sqrMagnitude;
                if (d < bestD) { bestD = d; best = a; }
            }
            return best;
        }

        /// <summary>Jugador vivo mas cercano dentro de un radio (sin correa de "casa").</summary>
        public static MarsFusionAvatar NearestPlayer(NetworkRunner runner, Vector3 from, float range)
        {
            MarsFusionAvatar best = null; float bestD = range * range;
            foreach (var a in Object.FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None))
            {
                if (a.Object == null || !a.Object.IsValid || a.Runner != runner || !a.IsAlive) continue;
                float d = (a.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = a; }
            }
            return best;
        }
    }
}
