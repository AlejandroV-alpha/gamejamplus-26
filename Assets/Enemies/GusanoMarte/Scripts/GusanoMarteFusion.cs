using System.Linq;
using Fusion;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gusano de Marte (reemplaza al Gusano Excavador). La autoridad de Fusion decide; todos lo ven igual.
/// Enterrado -> viaja bajo tierra hasta un jugador (montículo de polvo) -> EMERGE -> lo persigue reptando
/// -> lo MUERDE y lo atrapa entre los dientes (lo mastica) -> 10 balazos de los compañeros lo hacen SOLTARLO
/// -> se vuelve a enterrar.
/// </summary>
public sealed class GusanoMarteFusion : NetworkBehaviour, ITakeDamage
{
    public enum State { Hidden, Tunnel, Emerge, Chase, Grab, Hold, Release, Burrow, Death }

    [Header("Territorio y detección")]
    [SerializeField, Min(5f)] float territoryRadius = 55f;
    [SerializeField, Min(5f)] float detectionRange = 30f;
    [SerializeField] Vector2 hiddenSeconds = new Vector2(3f, 6f);

    [Header("Movimiento")]
    [SerializeField, Min(1f)] float tunnelSpeed = 12f;
    [SerializeField, Min(1f), Tooltip("Sale a esta distancia del jugador.")] float emergeDistance = 5f;
    [SerializeField, Min(.5f)] float chaseSpeed = 6f;
    [SerializeField, Min(10f)] float turnDegreesPerSecond = 130f;
    [SerializeField] Vector2 chaseSeconds = new Vector2(7f, 11f);

    [Header("Mordida y agarre")]
    [SerializeField, Min(1f)] float grabStartRange = 5.5f;
    [SerializeField, Min(.5f), Tooltip("Radio alrededor de la boca en el que la mordida atrapa.")] float grabRadius = 2.6f;
    [SerializeField, Min(0)] int biteDamage = 15;
    [SerializeField, Min(1), Tooltip("Balazos al gusano necesarios para soltar al jugador.")] int hitsToFree = 10;
    [SerializeField, Min(1f), Tooltip("Segundos máximos que retiene al jugador.")] float holdSeconds = 15f;
    [SerializeField, Min(0)] int holdDamage = 4;
    [SerializeField, Min(.2f)] float holdDamageInterval = 1f;

    [Header("Vida")]
    [SerializeField, Min(1)] int maxHealth = 260;

    [Header("Visual")]
    [SerializeField] Material dustMaterial;

    // Duraciones de los clips (frames / 30).
    const float BurrowTime = 40f / 30f, EmergeTime = 48f / 30f, GrabTime = 30f / 30f, ReleaseTime = 36f / 30f, BiteTime = .5f;

    [Networked] State Current { get; set; }
    [Networked] float StateStart { get; set; }
    [Networked] float StateLength { get; set; }
    [Networked] Vector3 SyncedPosition { get; set; }
    [Networked] Quaternion SyncedRotation { get; set; }
    [Networked] PlayerRef GrabbedPlayer { get; set; }
    [Networked] int FreeHits { get; set; }
    [Networked] public int Health { get; private set; }

    /// <summary>Verdadero en el cliente cuyo astronauta está atrapado: el avatar bloquea sus controles.</summary>
    public static bool LocalPlayerHeld { get; private set; }
    static GusanoMarteFusion holder;

    public State CurrentState => Object != null && Object.IsValid ? Current : State.Hidden;
    float Elapsed => Runner.SimulationTime - StateStart;
    float Size => Mathf.Max(.1f, transform.localScale.x);

    Animator animator;
    Renderer[] renderers;
    Collider[] colliders;
    Transform[] jaws;
    Transform head;
    Vector3 home;
    bool biteDone, dropped;
    float nextHoldDamage, dustAt;
    State shown = (State)(-1);
    AudioSource voice;
    ParticleSystem dust;

    public Vector3 MouthCenter => jaws != null && jaws.Length > 0
        ? jaws.Aggregate(Vector3.zero, (s, j) => s + j.position) / jaws.Length
        : transform.position + transform.forward * 2f * Size + Vector3.up * Size;

    public override void Spawned()
    {
        var voiceClip = Resources.Load<AudioClip>("Audio/Monsters/GusanoExcavador");
        if (voiceClip != null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.clip = voiceClip;
            voice.volume = .7f;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 4f;
            voice.maxDistance = 45f;
            voice.dopplerLevel = 0f;
        }
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        var all = GetComponentsInChildren<Transform>(true);
        jaws = all.Where(t => t.name.StartsWith("Jaw_")).ToArray();
        head = all.FirstOrDefault(t => t.name == "Head");
        home = transform.position;
        dust = BuildDust();
        if (HasStateAuthority)
        {
            Health = maxHealth;
            transform.position = OnGround(transform.position);
            Enter(State.Hidden, Random.Range(hiddenSeconds.x, hiddenSeconds.y));
        }
        GameJam.Combat.EnemyCombatUI.Attach(gameObject, "GUSANO DE MARTE", () => Health, maxHealth,
            () => Object != null && Object.IsValid && StateBufferIsValid
                && Current != State.Hidden && Current != State.Tunnel && Current != State.Burrow && Current != State.Death);
    }

    // ------------------------------------------------------------------ IA (autoridad)

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (Current == State.Death)
        {
            if (Elapsed >= StateLength) Runner.Despawn(Object);
            return;
        }
        MarsFusionAvatar target = NearestPlayer();
        Vector3 to = target != null ? Flat(target.transform.position - transform.position) : Vector3.zero;
        float dist = to.magnitude;

        switch (Current)
        {
            case State.Hidden:
                if (Elapsed >= StateLength && target != null && dist <= detectionRange) Enter(State.Tunnel, 6f);
                break;

            case State.Tunnel:
                // Bajo tierra: avanza hacia el jugador; solo se ve el montículo de polvo.
                if (target == null) { Enter(State.Hidden, hiddenSeconds.x); break; }
                Face(target.transform.position, 400f);
                float step = Mathf.Min(tunnelSpeed * Runner.DeltaTime, Mathf.Max(0f, dist - emergeDistance));
                Vector3 next = OnGround(transform.position + to.normalized * step);
                if (Flat(next - home).magnitude < territoryRadius) transform.position = next;
                if (dist <= emergeDistance + .2f || Elapsed >= StateLength)
                {
                    Face(target.transform.position, 9999f);
                    Enter(State.Emerge, EmergeTime);
                }
                break;

            case State.Emerge:
                if (target != null) Face(target.transform.position, 60f);
                if (Elapsed >= StateLength) Enter(State.Chase, Random.Range(chaseSeconds.x, chaseSeconds.y));
                break;

            case State.Chase:
                if (target == null || Elapsed >= StateLength) { Enter(State.Burrow, BurrowTime); break; }
                Face(target.transform.position, turnDegreesPerSecond);
                if (dist <= grabStartRange) { Enter(State.Grab, GrabTime); break; }
                // Avanza a empujones al ritmo de la onda del cuerpo (clip Slither de 0.8 s).
                float surge = .7f + .45f * Mathf.Max(0f, Mathf.Sin(Elapsed * Mathf.PI * 2f / .8f * 2f));
                Vector3 ahead = OnGround(transform.position + transform.forward
                    * Mathf.Min(chaseSpeed * surge * Runner.DeltaTime, Mathf.Max(0f, dist - grabStartRange * .7f)));
                if (Flat(ahead - home).magnitude < territoryRadius) transform.position = ahead;
                else { Enter(State.Burrow, BurrowTime); }
                break;

            case State.Grab:
                if (Elapsed < BiteTime * .8f && target != null) Face(target.transform.position, 200f);
                if (!biteDone && Elapsed >= BiteTime)
                {
                    biteDone = true;
                    Bite(target);
                }
                if (Elapsed >= StateLength)
                {
                    if (GrabbedPlayer != PlayerRef.None) Enter(State.Hold, holdSeconds);
                    else Enter(State.Chase, Random.Range(chaseSeconds.x, chaseSeconds.y) * .5f);
                }
                break;

            case State.Hold:
                MarsFusionAvatar victim = Grabbed();
                if (victim == null || !victim.IsAlive || FreeHits >= hitsToFree || Elapsed >= StateLength)
                {
                    // Liberado a balazos, porque murió o porque se cansó: lo escupe y se entierra.
                    Enter(State.Release, ReleaseTime);
                    break;
                }
                if (Runner.SimulationTime >= nextHoldDamage)
                {
                    nextHoldDamage = Runner.SimulationTime + holdDamageInterval;
                    victim.TakeMonsterDamage(holdDamage);
                }
                break;

            case State.Release:
                if (GrabbedPlayer != PlayerRef.None && Elapsed >= .2f) GrabbedPlayer = PlayerRef.None;
                if (Elapsed >= StateLength) Enter(State.Burrow, BurrowTime);
                break;

            case State.Burrow:
                if (Elapsed >= StateLength) Enter(State.Hidden, Random.Range(hiddenSeconds.x, hiddenSeconds.y));
                break;
        }

        SyncedPosition = transform.position;
        SyncedRotation = transform.rotation;
    }

    void Enter(State s, float length)
    {
        Current = s;
        StateStart = Runner.SimulationTime;
        StateLength = length;
        biteDone = false;
    }

    void Bite(MarsFusionAvatar target)
    {
        if (target == null || !target.IsAlive) return;
        Vector3 mouth = MouthCenter;
        Vector3 body = target.transform.position + Vector3.up * .9f;
        if (Vector3.Distance(mouth, body) > grabRadius) return;
        if (biteDamage > 0) target.TakeMonsterDamage(biteDamage);
        if (target.IsAlive && target.Object != null)
        {
            GrabbedPlayer = target.Object.StateAuthority;
            FreeHits = 0;
            nextHoldDamage = Runner.SimulationTime + holdDamageInterval;
        }
    }

    MarsFusionAvatar NearestPlayer()
    {
        return FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .Where(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.IsAlive
                && Flat(a.transform.position - home).magnitude < territoryRadius)
            .OrderBy(a => (a.transform.position - transform.position).sqrMagnitude)
            .FirstOrDefault();
    }

    MarsFusionAvatar Grabbed()
    {
        if (GrabbedPlayer == PlayerRef.None) return null;
        return FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .FirstOrDefault(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.Object.StateAuthority == GrabbedPlayer);
    }

    // ------------------------------------------------------------------ daño recibido

    public void TakeDamage(int amount)
    {
        if (Object == null || !Object.IsValid || amount <= 0) return;
        RPC_Shot(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_Shot(int amount)
    {
        if (amount <= 0 || Health <= 0 || Current == State.Hidden || Current == State.Tunnel
            || Current == State.Burrow || Current == State.Death) return;
        int previous = Health;
        Health = Mathf.Max(0, Health - amount);
        RPC_CombatDamage(previous - Health);
        if (Current == State.Hold) FreeHits++;   // cada balazo cuenta para soltar al atrapado
        if (Health <= 0)
        {
            GrabbedPlayer = PlayerRef.None;
            Enter(State.Death, BurrowTime + .5f);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_CombatDamage(int actualDamage)
    {
        GetComponent<GameJam.Combat.EnemyCombatUI>()?.Damage(actualDamage);
        var blood = GetComponent<GameJam.Combat.EnemyBloodFx>();
        if (blood != null && head != null) blood.Bleed(head.position, transform.forward);
    }

    // ------------------------------------------------------------------ visual (todos)

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            float k = Current == State.Hidden ? 1f : 1f - Mathf.Exp(-12f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, SyncedPosition, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, k);
        }
        UpdateHeldPlayer();

        bool underground = Current == State.Hidden || Current == State.Tunnel;
        foreach (var r in renderers) if (r != null && r.enabled == underground) r.enabled = !underground;
        foreach (var c in colliders) if (c != null) c.enabled = !underground && Current != State.Death;

        if (Current == State.Tunnel && Time.time >= dustAt)
        {
            dustAt = Time.time + .09f;
            Puff(transform.position + transform.forward * Size * .6f, 4, .6f);
        }

        if (animator == null || shown == Current) return;
        var prev = shown;
        shown = Current;
        if (voice != null)
        {
            if (underground || Current == State.Burrow || Current == State.Death) voice.Stop();
            else if ((Current == State.Emerge || Current == State.Grab) && !voice.isPlaying && !SceneTransitionVideo.IsPresent) voice.Play();
        }
        if (Current == State.Emerge) Puff(transform.position + transform.forward * Size * .4f, 55, 1.6f);
        if (Current == State.Burrow) Puff(transform.position + transform.forward * Size * .6f, 30, 1f);

        string clip = ClipFor(Current);
        if (clip == null) return;
        float len = ClipLength(Current);
        float offset = Current == State.Chase || Current == State.Hold ? 0f : Mathf.Clamp(Elapsed, 0f, Mathf.Max(0f, len - .02f));
        if (Current == State.Emerge || prev == State.Tunnel || prev == State.Hidden) animator.PlayInFixedTime(clip, 0, offset);
        else animator.CrossFadeInFixedTime(clip, Current == State.Grab ? .08f : .2f, 0, offset);
    }

    static string ClipFor(State s)
    {
        switch (s)
        {
            case State.Emerge: return "Emerge";
            case State.Chase: return "Slither";
            case State.Grab: return "Grab";
            case State.Hold: return "GrabHold";
            case State.Release: return "Release";
            case State.Burrow:
            case State.Death: return "Burrow";
            default: return null;
        }
    }

    static float ClipLength(State s)
    {
        switch (s)
        {
            case State.Emerge: return EmergeTime;
            case State.Grab: return GrabTime;
            case State.Release: return ReleaseTime;
            case State.Burrow:
            case State.Death: return BurrowTime;
            default: return 1f;
        }
    }

    /// <summary>En el cliente del atrapado: lo pega a la boca y bloquea sus controles.</summary>
    void UpdateHeldPlayer()
    {
        bool holding = (Current == State.Grab || Current == State.Hold || (Current == State.Release && Elapsed < .2f));
        bool heldHere = holding && GrabbedPlayer != PlayerRef.None && GrabbedPlayer == Runner.LocalPlayer;
        if (heldHere)
        {
            holder = this;
            LocalPlayerHeld = true;
            dropped = false;
            MarsFusionAvatar me = Grabbed();
            var player = me != null ? me.GetComponent<Starter.ThirdPersonCharacter.Player>() : null;
            if (player != null && player.KCC != null)
            {
                // Centro de la boca (promedio de las 3 mandíbulas); los pies bajan para que el cuerpo quede adentro.
                Vector3 grip = MouthCenter - Vector3.up * .9f + Mathf.Sin(Time.time * 9f) * .08f * transform.right;
                player.KCC.SetPosition(grip);
            }
        }
        else if (holder == this)
        {
            // Al soltarlo: lo deja en el suelo delante de la boca.
            if (!dropped)
            {
                dropped = true;
                MarsFusionAvatar me = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
                    .FirstOrDefault(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.Object.StateAuthority == Runner.LocalPlayer);
                var player = me != null ? me.GetComponent<Starter.ThirdPersonCharacter.Player>() : null;
                if (player != null && player.KCC != null)
                    player.KCC.SetPosition(OnGround(MouthCenter + Flat(transform.forward) * 1.2f * Size / 2.6f) + Vector3.up * .1f);
            }
            holder = null;
            LocalPlayerHeld = false;
        }
    }

    void OnGUI()
    {
        if (Object == null || !Object.IsValid || Current != State.Hold || GrabbedPlayer == PlayerRef.None) return;
        bool mine = GrabbedPlayer == Runner.LocalPlayer;
        string text = mine
            ? $"¡EL GUSANO TE ATRAPÓ!  Que tus compañeros le disparen para soltarte ({FreeHits}/{hitsToFree})"
            : $"¡El gusano atrapó a un compañero!  Dispárale para liberarlo ({FreeHits}/{hitsToFree})";
        var style = new GUIStyle(GUI.skin.box) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(1f, .55f, .3f);
        GUI.Box(new Rect(Screen.width * .5f - 440f, Screen.height * .18f, 880f, 56f), text, style);
    }

    void OnDisable()
    {
        if (holder == this) { holder = null; LocalPlayerHeld = false; }
    }

    void OnDestroy()
    {
        if (dust != null) Destroy(dust.gameObject);
    }

    // ------------------------------------------------------------------ polvo

    ParticleSystem BuildDust()
    {
        var go = new GameObject("PolvoGusanoMarte");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.playOnAwake = false; m.loop = false;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.2f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(.72f, .4f, .24f, .7f), new Color(.55f, .3f, .18f, .55f));
        m.gravityModifier = .2f;
        m.maxParticles = 900;
        var e = ps.emission; e.rateOverTime = 0f;
        var s = ps.shape; s.enabled = false;
        var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 2f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .1f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 1.8f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dustMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    void Puff(Vector3 at, int count, float power)
    {
        if (dust == null || dustMaterial == null) return;
        at = OnGround(at) + Vector3.up * .1f;
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            var d = Random.insideUnitCircle;
            var dir = new Vector3(d.x, 0f, d.y);
            p.position = at + dir * Size * .5f;
            p.velocity = dir.normalized * Random.Range(1f, 4f) * power + Vector3.up * Random.Range(.8f, 4f) * power;
            p.startSize = Random.Range(.7f, 1.6f) * power;
            dust.Emit(p, 1);
        }
    }

    void Face(Vector3 point, float degreesPerSecond)
    {
        Vector3 d = Flat(point - transform.position);
        if (d.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degreesPerSecond * Runner.DeltaTime);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static Vector3 OnGround(Vector3 point)
    {
        var terrain = Terrain.activeTerrains.FirstOrDefault(t => point.x >= t.transform.position.x && point.z >= t.transform.position.z
            && point.x <= t.transform.position.x + t.terrainData.size.x && point.z <= t.transform.position.z + t.terrainData.size.z);
        if (terrain != null) point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        return point;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .5f, .1f, .8f);
        Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, territoryRadius);
    }
}
