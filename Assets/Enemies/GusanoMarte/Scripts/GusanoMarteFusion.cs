using System.Linq;
using Fusion;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// "Fauces de Óxido", el gusano de Marte (version de "Pruebas de Animacion" adaptada a red). La autoridad de Fusion decide; todos lo ven igual.
///  Reposo (alzado) -> al detectar a un jugador se ENTIERRA -> avanza bajo tierra (monticulo de polvo)
///  -> EMERGE a su lado -> lo PERSIGUE reptando -> lo MUERDE y lo atrapa en la boca (lo mastica)
///  -> con 10 balazos de los compañeros lo SUELTA -> se vuelve a enterrar.
/// </summary>
public sealed class GusanoMarteFusion : NetworkBehaviour, ITakeDamage
{
    public enum State { Idle, Burrow, Tunnel, Emerge, Chase, Grab, Hold, Release, Death }

    [Header("Territorio y detección")]
    [SerializeField, Min(5f)] float territoryRadius = 55f;
    [SerializeField, Min(5f)] float detectionRange = 30f;
    [SerializeField, Tooltip("Segundos minimos en reposo antes de volver a atacar.")] Vector2 hiddenSeconds = new Vector2(1.2f, 2.5f);

    [Header("Movimiento")]
    [SerializeField, Min(1f)] float tunnelSpeed = 16f;
    [SerializeField, Min(1f), Tooltip("Sale a esta distancia del jugador.")] float emergeDistance = 3.5f;
    [SerializeField, Min(.5f)] float chaseSpeed = 9f;
    [SerializeField, Min(10f)] float turnDegreesPerSecond = 240f;
    [SerializeField, Tooltip("Ya no se usa: persigue mientras el jugador siga cerca.")] Vector2 chaseSeconds = new Vector2(7f, 11f);

    [Header("Mordida y agarre")]
    [SerializeField, Min(1f)] float grabStartRange = 5.5f;
    [SerializeField, Min(.5f), Tooltip("Radio alrededor de la boca en el que la mordida atrapa.")] float grabRadius = 2.6f;
    [SerializeField, Min(0)] int biteDamage = 15;
    [SerializeField, Min(1), Tooltip("Balazos al gusano necesarios para soltar al jugador.")] int hitsToFree = 10;
    [SerializeField, Min(1f), Tooltip("Segundos máximos que retiene al jugador.")] float holdSeconds = 15f;
    [SerializeField, Min(0)] int holdDamage = 4;
    [SerializeField, Min(.2f)] float holdDamageInterval = 1f;
    [SerializeField, Min(0f), Tooltip("Pausa tras soltar a alguien antes de poder morder otra vez.")] float regrabCooldown = 3f;

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
    [Networked] float NextGrabAt { get; set; }
    [Networked] public int Health { get; private set; }

    /// <summary>Verdadero en el cliente cuyo astronauta está atrapado: el avatar bloquea sus controles.</summary>
    public static bool LocalPlayerHeld { get; private set; }
    static GusanoMarteFusion holder;

    public State CurrentState => Object != null && Object.IsValid ? Current : State.Idle;
    float Elapsed => Runner.SimulationTime - StateStart;
    float Size => Mathf.Max(.1f, transform.localScale.x);
    // El polvo escala con el tamaño del gusano (2.6 es el tamaño de referencia).
    float DustScale => Mathf.Max(.5f, Size / 2.6f);

    Animator animator;
    Renderer[] renderers;
    Collider[] colliders;
    Transform[] jaws;
    Transform head;
    Vector3 home;
    bool biteDone, dropped, burrowBurst;
    float nextHoldDamage, dustAt, shownAt;
    State shown = (State)(-1);
    AudioSource voice, crawl, digVoice, emergeVoice, tunnelVoice;
    ParticleSystem dust, debris, cubes;

    public Vector3 MouthCenter => jaws != null && jaws.Length > 0
        ? jaws.Aggregate(Vector3.zero, (s, j) => s + j.position) / jaws.Length
        : transform.position + transform.forward * 2f * Size + Vector3.up * Size;

    public override void Spawned()
    {
        voice = MakeAudio("GusanoExcavador", .7f, 4f, 45f, false);       // rugido al emerger y morder
        crawl = MakeAudio("GusanoArrastrandose", .8f, 4f, 40f, true);    // en bucle mientras persigue
        digVoice = MakeAudio("GusanoEnterrandose", 1f, 5f, 50f, false);  // al empezar a enterrarse
        emergeVoice = MakeAudio("GusanoSaliendo", 1f, 5f, 50f, false);   // al salir de la tierra
        tunnelVoice = MakeAudio("GusanoBajoTierra", 1f, 5f, 50f, true);  // en bucle viajando bajo tierra

        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        var all = GetComponentsInChildren<Transform>(true);
        jaws = all.Where(t => t.name.StartsWith("Jaw_")).ToArray();
        head = all.FirstOrDefault(t => t.name == "Head");
        home = transform.position;
        dust = BuildDust();
        debris = BuildDebris();
        cubes = BuildCubes();
        if (HasStateAuthority)
        {
            Health = maxHealth;
            transform.position = OnGround(transform.position);
            Enter(State.Idle, Random.Range(hiddenSeconds.x, hiddenSeconds.y));
        }
        GameJam.Combat.EnemyCombatUI.Attach(gameObject, "FAUCES DE ÓXIDO", () => Health, maxHealth,
            () => Object != null && Object.IsValid && StateBufferIsValid
                && Current != State.Tunnel && Current != State.Death);
    }

    AudioSource MakeAudio(string clipName, float volume, float min, float max, bool loop)
    {
        var clip = Resources.Load<AudioClip>("Audio/Monsters/" + clipName);
        if (clip == null) return null;
        var a = gameObject.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.clip = clip;
        a.loop = loop;
        a.volume = volume;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = min;
        a.maxDistance = max;
        a.dopplerLevel = 0f;
        return a;
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
        MarsFusionAvatar target = PickTarget();
        // Bicho de oleada sin jugador cerca: su presa es el casco de la nave.
        bool wave = GameJam.Waves.EnemyAggro.IsWave(Object);
        bool shipMode = target == null && GameJam.Waves.EnemyAggro.WantsShip(Object);
        bool hasGoal = target != null || shipMode;
        Vector3 goal = target != null ? target.transform.position
            : shipMode ? GameJam.Waves.EnemyAggro.Ship.HullPoint(transform.position) : transform.position;
        bool hunting = shipMode || (target != null && target == aggroTarget && Runner.SimulationTime < aggroUntil);
        Vector3 to = hasGoal ? Flat(goal - transform.position) : Vector3.zero;
        float dist = to.magnitude;

        switch (Current)
        {
            case State.Idle:
                // Alzado esperando. Al detectar a un jugador se entierra para ir a por él.
                if (Elapsed >= StateLength && hasGoal && (hunting || dist <= detectionRange)) Enter(State.Burrow, BurrowTime);
                break;

            case State.Burrow:
                if (Elapsed >= StateLength) Enter(State.Tunnel, 6f);
                break;

            case State.Tunnel:
                // Bajo tierra: avanza hacia el jugador; solo se ve el montículo de polvo.
                if (!hasGoal)
                {
                    if (Elapsed > 2f) Enter(State.Emerge, EmergeTime);
                    break;
                }
                Face(goal, 400f);
                float step = Mathf.Min(tunnelSpeed * Runner.DeltaTime, Mathf.Max(0f, dist - emergeDistance * .8f));
                Vector3 next = OnGround(transform.position + to.normalized * step);
                if (wave || Flat(next - home).magnitude < territoryRadius) transform.position = next;
                if ((dist <= emergeDistance && Elapsed > .6f) || Elapsed >= StateLength)
                {
                    Face(goal, 9999f);
                    Enter(State.Emerge, EmergeTime);
                }
                break;

            case State.Emerge:
                // Sale mirando a su presa y, si la tiene a tiro, muerde sin pausa al terminar de salir.
                if (hasGoal) Face(goal, 220f);
                if (Elapsed >= StateLength * .8f)
                {
                    if (hasGoal && dist <= grabStartRange && Runner.SimulationTime >= NextGrabAt) Enter(State.Grab, GrabTime);
                    else Enter(State.Chase, 0f);
                }
                break;

            case State.Chase:
                if (!hasGoal || (!hunting && dist > detectionRange * 1.6f))
                {
                    Enter(State.Idle, Random.Range(hiddenSeconds.x, hiddenSeconds.y));
                    break;
                }
                Face(goal, turnDegreesPerSecond);
                if (dist <= grabStartRange && Runner.SimulationTime >= NextGrabAt) { Enter(State.Grab, GrabTime); break; }
                // Si la presa se le escapa, no se arrastra detrás: se vuelve a enterrar para emboscarla.
                if (dist > emergeDistance * (shipMode ? 2.5f : 4f) && Elapsed > 1.5f) { Enter(State.Burrow, BurrowTime); break; }
                // Avanza a empujones al ritmo de la onda del cuerpo (dos impulsos por ciclo del clip Slither de 0.8 s).
                float surge = .7f + .45f * Mathf.Max(0f, Mathf.Sin(Elapsed * Mathf.PI * 2f / .8f * 2f));
                Vector3 ahead = OnGround(transform.position + transform.forward
                    * Mathf.Min(chaseSpeed * surge * Runner.DeltaTime, Mathf.Max(0f, dist - grabStartRange * .7f)));
                if (wave || Flat(ahead - home).magnitude < territoryRadius) transform.position = ahead;
                else Enter(State.Burrow, BurrowTime);
                break;

            case State.Grab:
                if (Elapsed < BiteTime * .8f && hasGoal) Face(goal, 200f);
                if (!biteDone && Elapsed >= BiteTime)
                {
                    biteDone = true;
                    RPC_ActionSound(EnemyActionAudio.Cue.WormBite, head != null ? head.position : transform.position);
                    if (shipMode) GameJam.Waves.EnemyAggro.Ship.Damage(biteDamage * 1.5f, MouthCenter);
                    else Bite(target);
                }
                if (Elapsed >= StateLength)
                {
                    if (GrabbedPlayer != PlayerRef.None) Enter(State.Hold, holdSeconds);
                    else
                    {
                        NextGrabAt = Runner.SimulationTime + .4f;   // falló: reintenta casi enseguida
                        Enter(State.Chase, 0f);
                    }
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
                if (GrabbedPlayer != PlayerRef.None && Elapsed >= .2f)
                {
                    GrabbedPlayer = PlayerRef.None;
                    RPC_ActionSound(EnemyActionAudio.Cue.WormRelease, head != null ? head.position : transform.position);
                }
                if (Elapsed >= StateLength)
                {
                    NextGrabAt = Runner.SimulationTime + regrabCooldown;
                    Enter(State.Burrow, BurrowTime);
                }
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Unreliable)]
    private void RPC_ActionSound(EnemyActionAudio.Cue cue, Vector3 point)
        => EnemyActionAudio.Play(cue, point, gameObject.scene);

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

    MarsFusionAvatar aggroTarget;
    float aggroUntil;

    // Quien le disparo manda; en oleada solo lo distrae un jugador muy cerca; si no, el de su territorio.
    MarsFusionAvatar PickTarget()
    {
        if (aggroTarget != null && aggroTarget.Object != null && aggroTarget.Object.IsValid && aggroTarget.IsAlive
            && Runner.SimulationTime < aggroUntil) return aggroTarget;
        aggroTarget = null;
        if (GameJam.Waves.EnemyAggro.WantsShip(Object))
            return GameJam.Waves.EnemyAggro.NearestPlayer(Runner, transform.position, GameJam.Waves.EnemyAggro.ShipDistraction);
        return NearestPlayer();
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
    void RPC_Shot(int amount, RpcInfo info = default)
    {
        if (amount > 0 && Current != State.Death)
        {
            var shooter = GameJam.Waves.EnemyAggro.FindShooter(Runner, info.Source, transform.position);
            if (shooter != null) { aggroTarget = shooter; aggroUntil = Runner.SimulationTime + GameJam.Waves.EnemyAggro.AggroSeconds; }
        }
        if (amount <= 0 || Health <= 0 || Current == State.Tunnel || Current == State.Death) return;
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
        flinch = 1f;
        GetComponent<GameJam.Combat.EnemyCombatUI>()?.Damage(actualDamage);
        var blood = GetComponent<GameJam.Combat.EnemyBloodFx>();
        if (blood != null && head != null) blood.Bleed(head.position, transform.forward);
    }

    // ------------------------------------------------------------------ visual (todos)

    float flinch;

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            float k = Current == State.Tunnel ? 1f : 1f - Mathf.Exp(-12f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, SyncedPosition, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, k);
        }
        flinch = Mathf.MoveTowards(flinch, 0f, Time.deltaTime * 4f);
        UpdateHeldPlayer();

        bool underground = Current == State.Tunnel;
        foreach (var r in renderers) if (r != null && r.enabled == underground) r.enabled = !underground;
        foreach (var c in colliders) if (c != null) c.enabled = !underground && Current != State.Death;

        if (shown != Current) OnStateShown();
        float t = Time.time - shownAt;

        // Polvo continuo mientras se hunde, viaja bajo tierra o sale.
        if (Current == State.Burrow || Current == State.Death)
        {
            if (!burrowBurst && t > .55f) { burrowBurst = true; DirtBurst(transform.position + transform.forward * 1.5f * DustScale, 1.8f); }
            if (t < BurrowTime * .9f && Time.time >= dustAt) { dustAt = Time.time + .05f; DirtPuff(transform.position + transform.forward * .8f * DustScale, 1.7f); }
        }
        else if (Current == State.Tunnel && Time.time >= dustAt)
        {
            dustAt = Time.time + .05f;
            DirtPuff(transform.position + transform.forward * DustScale, 1.5f);
        }
        else if (Current == State.Emerge && t < .9f && Time.time >= dustAt)
        {
            dustAt = Time.time + .05f;
            DirtPuff(transform.position, 2f);
        }
    }

    void OnStateShown()
    {
        var prev = shown;
        shown = Current;
        shownAt = Time.time;
        burrowBurst = false;
        UpdateVoices();

        if (Current == State.Burrow || Current == State.Death) DirtBurst(transform.position + transform.forward * .8f * DustScale, .9f);
        if (Current == State.Emerge) DirtBurst(transform.position, 2.4f);

        if (animator == null) return;
        string clip = ClipFor(Current);
        if (clip == null) return;
        float len = ClipLength(Current);
        float offset = Current == State.Chase || Current == State.Hold || Current == State.Idle
            ? 0f : Mathf.Clamp(Elapsed, 0f, Mathf.Max(0f, len - .02f));
        if (Current == State.Emerge || prev == State.Tunnel) animator.PlayInFixedTime(clip, 0, offset);
        else animator.CrossFadeInFixedTime(clip, Current == State.Grab ? .08f : Current == State.Idle ? .3f : .2f, 0, offset);
    }

    void UpdateVoices()
    {
        bool silent = SceneTransitionVideo.IsPresent;
        if (tunnelVoice != null)
        {
            if (Current == State.Tunnel && !silent) { if (!tunnelVoice.isPlaying) tunnelVoice.Play(); }
            else if (tunnelVoice.isPlaying) tunnelVoice.Stop();
        }
        if (crawl != null)
        {
            if (Current == State.Chase && !silent) { if (!crawl.isPlaying) crawl.Play(); }
            else if (crawl.isPlaying) crawl.Stop();
        }
        if (silent) return;
        if (Current == State.Emerge && emergeVoice != null) emergeVoice.Play();
        if ((Current == State.Burrow || Current == State.Death) && digVoice != null) digVoice.Play();
        if (voice != null)
        {
            if (Current == State.Burrow || Current == State.Tunnel || Current == State.Death) voice.Stop();
            else if ((Current == State.Emerge || Current == State.Grab) && !voice.isPlaying) voice.Play();
        }
    }

    static string ClipFor(State s)
    {
        switch (s)
        {
            case State.Idle: return "Idle";
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
                // Centro de la boca (promedio de las 3 mandíbulas); se sacude un poco al recibir balazos.
                Vector3 grip = MouthCenter - Vector3.up * .9f + Mathf.Sin(Time.time * 9f) * .08f * transform.right
                    + Random.insideUnitSphere * .06f * flinch;
                player.KCC.SetPosition(grip);
            }
        }
        else if (holder == this)
        {
            // Al soltarlo: lo deja en el suelo delante de la boca y lo lanza un poco.
            if (!dropped)
            {
                dropped = true;
                MarsFusionAvatar me = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
                    .FirstOrDefault(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.Object.StateAuthority == Runner.LocalPlayer);
                var player = me != null ? me.GetComponent<Starter.ThirdPersonCharacter.Player>() : null;
                if (player != null && player.KCC != null)
                {
                    player.KCC.SetPosition(OnGround(MouthCenter + Flat(transform.forward) * 1.2f * Size / 2.6f) + Vector3.up * .1f);
                    me?.RequestRagdollKnockdown(transform.forward * 4.5f + Vector3.up * 2f, 1.8f);
                }
            }
            holder = null;
            LocalPlayerHeld = false;
        }
    }

    void OnGUI()
    {
        if (Object == null || !Object.IsValid || Current != State.Hold || GrabbedPlayer == PlayerRef.None) return;
        bool mine = GrabbedPlayer == Runner.LocalPlayer;
        GameJam.UI.GameplayFeedbackPanel.Draw(
            mine ? "¡El gusano te atrapó!" : "¡El gusano atrapó a un compañero!",
            mine ? "Tus compañeros deben dispararle para soltarte" : "Dispárale con [@LMB] para liberarlo",
            new Color(1f, .45f, .3f), Mathf.Clamp01(FreeHits / (float)hitsToFree), top: true, icon: "danger0");
    }

    void OnDisable()
    {
        if (holder == this) { holder = null; LocalPlayerHeld = false; }
    }

    void OnDestroy()
    {
        if (dust != null) Destroy(dust.gameObject);
        if (debris != null) Destroy(debris.gameObject);
        if (cubes != null) Destroy(cubes.gameObject);
    }

    // ------------------------------------------------------------------ polvo / tierra

    ParticleSystem BuildDust()
    {
        var go = new GameObject("PolvoGusanoMarte");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.playOnAwake = false; m.loop = false;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 3f);
        m.startSize = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(.72f, .46f, .3f, .58f), new Color(.55f, .33f, .2f, .45f));
        m.gravityModifier = .05f;
        m.maxParticles = 3000;
        var e = ps.emission; e.rateOverTime = 0f;
        var s = ps.shape; s.enabled = false;
        // Sin multiplicar por el tamaño: con particulas grandes el freno seria enorme y la columna no subiria.
        var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 1.7f;
        drag.multiplyDragByParticleSize = false; drag.multiplyDragByParticleVelocity = false;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .08f), new GradientAlphaKey(.55f, .55f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .45f, 1f, 1.7f));
        // Giran y se arremolinan: se ve como una nube densa, no como bolitas.
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.7f, .7f);
        var noise = ps.noise; noise.enabled = true; noise.strength = .9f; noise.frequency = .3f; noise.scrollSpeed = .35f; noise.damping = true;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dustMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    // Terrones y piedritas que saltan y caen (mas oscuros y alargados que el polvo).
    ParticleSystem BuildDebris()
    {
        var go = new GameObject("EscombrosGusanoMarte");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.playOnAwake = false; m.loop = false;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.9f);
        m.startSize = new ParticleSystem.MinMaxCurve(.15f, .42f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(.32f, .19f, .12f, 1f), new Color(.22f, .13f, .08f, 1f));
        m.gravityModifier = 2.6f;
        m.maxParticles = 600;
        var e = ps.emission; e.rateOverTime = 0f;
        var s = ps.shape; s.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var c = ps.collision; c.enabled = true; c.type = ParticleSystemCollisionType.World; c.bounce = .15f; c.lifetimeLoss = .35f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dustMaterial;
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .05f; r.lengthScale = 1.5f;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    // Cubitos solidos de tierra: giran en el aire, rebotan en el suelo y se encogen hasta desaparecer.
    ParticleSystem BuildCubes()
    {
        var go = new GameObject("CubosTierraGusanoMarte");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.playOnAwake = false; m.loop = false;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
        m.startSize = new ParticleSystem.MinMaxCurve(.22f, .6f);
        m.startRotation3D = true;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(.55f, .33f, .21f, 1f), new Color(.33f, .19f, .12f, 1f));
        m.gravityModifier = 3f;
        m.maxParticles = 400;
        var e = ps.emission; e.rateOverTime = 0f;
        var s = ps.shape; s.enabled = false;
        // Los ultimos instantes se encogen (el material es opaco, no admite desvanecer por transparencia).
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.72f, 1f), new Keyframe(1f, 0f)));
        var c = ps.collision; c.enabled = true; c.type = ParticleSystemCollisionType.World; c.bounce = .3f; c.dampen = .35f; c.lifetimeLoss = 0f; c.radiusScale = .7f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        var prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
        r.mesh = prim.GetComponent<MeshFilter>().sharedMesh;
        Destroy(prim);
        r.sharedMaterial = CubeMaterial();
        r.shadowCastingMode = ShadowCastingMode.On;
        r.receiveShadows = true;
        return ps;
    }

    static Material cubeMat;
    static Material CubeMaterial()
    {
        if (cubeMat != null) return cubeMat;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (sh == null || !sh.isSupported) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        cubeMat = new Material(sh) { name = "M_CuboTierra" };
        cubeMat.SetColor("_BaseColor", Color.white);
        if (cubeMat.HasProperty("_Smoothness")) cubeMat.SetFloat("_Smoothness", .05f);
        if (cubeMat.HasProperty("_Metallic")) cubeMat.SetFloat("_Metallic", 0f);
        return cubeMat;
    }

    void EmitCubes(Vector3 at, int count, float k, float outward, float up)
    {
        if (cubes == null) return;
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            var d = Random.insideUnitCircle.normalized;
            p.position = at + new Vector3(d.x, 0f, d.y) * Random.Range(.2f, 1f) * k + Vector3.up * .3f;
            p.velocity = new Vector3(d.x, 0f, d.y) * Random.Range(.4f, 1f) * outward * k + Vector3.up * Random.Range(.5f, 1f) * up;
            p.startSize = Random.Range(.22f, .6f) * k;
            p.rotation3D = Random.insideUnitSphere * 360f;
            p.angularVelocity3D = Random.insideUnitSphere * 520f;
            cubes.Emit(p, 1);
        }
    }

    // Explosion grande: anillo de polvo a ras de suelo, columna que sube y terrones que saltan.
    void DirtBurst(Vector3 at, float power)
    {
        if (dust == null || dustMaterial == null) return;
        float k = DustScale;
        at = OnGround(at) + Vector3.up * .1f;
        var p = new ParticleSystem.EmitParams();
        int ring = Mathf.RoundToInt(70 * power);
        for (int i = 0; i < ring; i++)
        {
            var d = Random.insideUnitCircle.normalized;
            p.position = at + new Vector3(d.x, 0f, d.y) * Random.Range(.3f, 1.3f) * k;
            p.velocity = new Vector3(d.x, 0f, d.y) * Random.Range(4f, 10f) * k + Vector3.up * Random.Range(.4f, 2.2f);
            p.startSize = Random.Range(2.2f, 4.8f) * k;
            dust.Emit(p, 1);
        }
        int column = Mathf.RoundToInt(50 * power);
        for (int i = 0; i < column; i++)
        {
            var d = Random.insideUnitCircle;
            p.position = at + new Vector3(d.x, 0f, d.y) * .9f * k;
            p.velocity = new Vector3(d.x, 0f, d.y) * Random.Range(.5f, 3f) + Vector3.up * Random.Range(8f, 16f) * Mathf.Sqrt(power);
            p.startSize = Random.Range(1.8f, 4f) * k;
            dust.Emit(p, 1);
        }
        EmitCubes(at, Mathf.RoundToInt(24 * power), k, 9f, 13f);
        if (debris == null) return;
        int chunks = Mathf.RoundToInt(35 * power);
        for (int i = 0; i < chunks; i++)
        {
            var d = Random.insideUnitCircle.normalized;
            p.position = at + new Vector3(d.x, 0f, d.y) * Random.Range(.2f, 1f) * k + Vector3.up * .2f;
            p.velocity = new Vector3(d.x, 0f, d.y) * Random.Range(2f, 7f) * k + Vector3.up * Random.Range(4f, 12f);
            p.startSize = Random.Range(.15f, .42f) * k;
            debris.Emit(p, 1);
        }
    }

    // Bocanadas de polvo seguidas: se usan mientras se hunde, sale o viaja bajo tierra.
    void DirtPuff(Vector3 at, float power = 1f)
    {
        if (dust == null || dustMaterial == null) return;
        float k = DustScale;
        at = OnGround(at) + Vector3.up * .05f;
        var p = new ParticleSystem.EmitParams();
        int n = Mathf.RoundToInt(6 * power);
        for (int i = 0; i < n; i++)
        {
            var d = Random.insideUnitCircle;
            p.position = at + new Vector3(d.x, 0f, d.y) * 1.1f * k;
            p.velocity = new Vector3(d.x, 0f, d.y) * Random.Range(1.5f, 4f) * k + Vector3.up * Random.Range(1f, 3.5f);
            p.startSize = Random.Range(1.6f, 3.4f) * k;
            dust.Emit(p, 1);
        }
        if (Random.value < .45f) EmitCubes(at, 1, k, 5f, 7f);
        if (debris != null && Random.value < .35f)
        {
            var d = Random.insideUnitCircle;
            p.position = at + new Vector3(d.x, 0f, d.y) * .8f * k + Vector3.up * .2f;
            p.velocity = new Vector3(d.x, 0f, d.y) * Random.Range(1f, 3f) + Vector3.up * Random.Range(3f, 6f);
            p.startSize = Random.Range(.15f, .35f) * k;
            debris.Emit(p, 1);
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
