using System.Linq;
using Fusion;
using GameJam.Multiplayer;
using UnityEngine;

/// <summary>
/// Gusano excavador de Marte: espera bajo tierra, emerge cerca de un jugador (a veces saltando),
/// repta, muerde y vuelve a enterrarse. La autoridad de Fusion decide; todos lo ven igual.
/// </summary>
public sealed class GusanoExcavadorFusion : NetworkBehaviour, ITakeDamage
{
    public enum State { Hidden, Emerge, EmergeJump, Crawl, Attack, Burrow, Hold, Death }

    [SerializeField, Min(5f)] float territoryRadius = 45f;
    [SerializeField, Min(1f)] float emergeDistanceFromPlayer = 9f;
    [SerializeField] Vector2 hiddenSeconds = new Vector2(4f, 9f);
    [SerializeField] Vector2 crawlSeconds = new Vector2(3f, 6f);
    [SerializeField, Min(0f)] float crawlSpeed = 3f;
    [SerializeField, Min(1f)] float attackRange = 8f;
    [SerializeField, Min(0)] int damage = 20;
    [SerializeField, Range(0f, 1f)] float jumpChance = .3f;
    [SerializeField, Min(0f)] float crossFade = .25f;

    [Header("Atrapar al jugador")]
    [SerializeField, Min(1f), Tooltip("Segundos maximos que retiene al jugador.")] float holdSeconds = 10f;
    [SerializeField, Min(1), Tooltip("Balazos al gusano necesarios para soltar al jugador.")] int hitsToFree = 5;
    [SerializeField, Min(0)] int holdDamage = 5;
    [SerializeField, Min(.2f)] float holdDamageInterval = 1f;

    [Networked] PlayerRef GrabbedPlayer { get; set; }
    [Networked] int FreeHits { get; set; }
    [SerializeField, Min(1)] int maxHealth = 180;
    [Networked] public int Health { get; private set; }
    float nextHoldDamage;

    /// <summary>
    /// Verdadero en el cliente cuyo astronauta esta atrapado: el avatar bloquea sus controles.
    /// </summary>
    public static bool LocalPlayerHeld { get; private set; }
    static GusanoExcavadorFusion holder;
    Transform mouth;
    Transform[] jaws;
    public Vector3 MouthCenter => jaws != null && jaws.Length > 0 ? jaws.Aggregate(Vector3.zero, (s, j) => s + j.position) / jaws.Length : (mouth != null ? mouth.position : transform.position);

    [Networked] State Current { get; set; }
    public State CurrentState => Object != null && Object.IsValid ? Current : State.Hidden;
    [Networked] float StateStart { get; set; }
    [Networked] float StateLength { get; set; }
    [Networked] Vector3 SyncedPosition { get; set; }
    [Networked] Quaternion SyncedRotation { get; set; }

    AudioSource wormVoice;
    Animator animator;
    Renderer[] renderers;
    Vector3 home;
    bool attackHit;
    State shown = (State)(-1);

    float Elapsed => Runner.SimulationTime - StateStart;

    public override void Spawned()
    {
        var voiceClip = Resources.Load<AudioClip>("Audio/Monsters/GusanoExcavador");
        if (voiceClip != null)
        {
            wormVoice = gameObject.AddComponent<AudioSource>();
            wormVoice.playOnAwake = false;
            wormVoice.loop = false;
            wormVoice.clip = voiceClip;
            wormVoice.volume = .6f;
            wormVoice.spatialBlend = 1f;
            wormVoice.rolloffMode = AudioRolloffMode.Linear;
            wormVoice.minDistance = 3f;
            wormVoice.maxDistance = 35f;
            wormVoice.dopplerLevel = 0f;
            wormVoice.priority = 128;
        }
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        renderers = GetComponentsInChildren<Renderer>();
        home = transform.position;
        // La boca: donde nacen los colmillos (o el hueso mas adelantado si no los hay).
        jaws = GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Mandibula_")).ToArray();
        mouth = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Mandibula_0"))
            ?? GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Colmillo_"))
            ?? transform;
        if (HasStateAuthority)
        {
            Health = maxHealth;
            Enter(State.Hidden, Random.Range(hiddenSeconds.x, hiddenSeconds.y));
        }
        GameJam.Combat.EnemyCombatUI.Attach(gameObject, "GUSANO EXCAVADOR", () => Health, maxHealth,
            () => Object != null && Object.IsValid && StateBufferIsValid
                && Current != State.Hidden && Current != State.Burrow && Current != State.Death);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (Current == State.Death)
        {
            if (Elapsed >= StateLength) Runner.Despawn(Object);
            return;
        }
        MarsFusionAvatar target = NearestPlayer();

        switch (Current)
        {
            case State.Hidden:
                if (Elapsed >= StateLength)
                {
                    MoveUnderground(target);
                    bool jump = Random.value < jumpChance;
                    Enter(jump ? State.EmergeJump : State.Emerge, Clip(jump ? "Salir_y_Saltar" : "Salir"));
                }
                break;
            case State.Emerge:
            case State.EmergeJump:
                if (target != null) Face(target.transform.position, 90f);
                if (Elapsed >= StateLength) Enter(State.Crawl, Random.Range(crawlSeconds.x, crawlSeconds.y));
                break;
            case State.Crawl:
                if (target != null && Flat(target.transform.position - transform.position).magnitude <= attackRange)
                {
                    Enter(State.Attack, Clip("Atacar"));
                    break;
                }
                Vector3 goal = target != null ? target.transform.position : home;
                Face(goal, 60f);
                Vector3 next = transform.position + transform.forward * crawlSpeed * Runner.DeltaTime;
                if (Flat(next - home).magnitude < territoryRadius) transform.position = OnGround(next);
                if (Elapsed >= StateLength) Enter(State.Burrow, Clip("Enterrarse"));
                break;
            case State.Attack:
                // El clip cierra los colmillos hacia el cuadro 10 de 30.
                if (!attackHit && Elapsed >= StateLength * .33f)
                {
                    attackHit = true;
                    Bite(target);
                }
                if (Elapsed >= StateLength)
                {
                    if (Random.value < .5f) Enter(State.Crawl, Random.Range(crawlSeconds.x, crawlSeconds.y));
                    else Enter(State.Burrow, Clip("Enterrarse"));
                }
                break;
            case State.Burrow:
                if (Elapsed >= StateLength) Enter(State.Hidden, Random.Range(hiddenSeconds.x, hiddenSeconds.y));
                break;
            case State.Hold:
                MarsFusionAvatar victim = Grabbed();
                if (victim == null || !victim.IsAlive || FreeHits >= hitsToFree || Elapsed >= StateLength)
                {
                    // Liberado por los disparos, por morir o porque el gusano se cansa: lo suelta y se entierra.
                    GrabbedPlayer = PlayerRef.None;
                    Enter(State.Burrow, Clip("Enterrarse"));
                    break;
                }
                if (Runner.SimulationTime >= nextHoldDamage)
                {
                    nextHoldDamage = Runner.SimulationTime + holdDamageInterval;
                    victim.TakeMonsterDamage(holdDamage);
                }
                break;
        }

        SyncedPosition = transform.position;
        SyncedRotation = transform.rotation;
    }

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            // Bajo tierra se teletransporta; en superficie se suaviza.
            float k = Current == State.Hidden ? 1f : 1f - Mathf.Exp(-12f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, SyncedPosition, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, k);
        }
        UpdateHeldPlayer();
        if (animator == null) return;
        // Atrapado: la boca queda cerrada sobre el jugador y los dientes mastican (GusanoTeethMotion).
        animator.speed = Current == State.Hold ? 0f : 1f;

        bool visible = Current != State.Hidden;
        foreach (var r in renderers) if (r.enabled != visible) r.enabled = visible;
        if (shown == Current) return;
        shown = Current;
        if (wormVoice != null)
        {
            if (Current == State.Hidden || Current == State.Burrow || Current == State.Death) wormVoice.Stop();
            else if ((Current == State.Emerge || Current == State.EmergeJump || Current == State.Attack || Current == State.Hold)
                && !wormVoice.isPlaying && !SceneTransitionVideo.IsPresent) wormVoice.Play();
        }
        string clip = ClipFor(Current);
        if (clip != null)
        {
            float offset = Mathf.Clamp01(Elapsed / Mathf.Max(.01f, Clip(clip)));
            if (Current == State.Hold) animator.PlayInFixedTime(clip, 0, Clip(clip) * .35f); // boca cerrada
            else animator.CrossFadeInFixedTime(clip, crossFade, 0, Current == State.Crawl ? 0f : offset * Clip(clip));
        }
    }

    void Enter(State state, float length)
    {
        Current = state;
        StateStart = Runner.SimulationTime;
        StateLength = length;
        attackHit = false;
    }

    static string ClipFor(State state)
    {
        switch (state)
        {
            case State.Emerge: return "Salir";
            case State.EmergeJump: return "Salir_y_Saltar";
            case State.Crawl: return "Mover";
            case State.Attack: return "Atacar";
            case State.Hold: return "Atacar";
            case State.Burrow:
            case State.Death: return "Enterrarse";
            default: return null;
        }
    }

    float Clip(string name)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 1.4f;
        var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == name);
        return clip != null ? clip.length : 1.4f;
    }

    MarsFusionAvatar NearestPlayer()
    {
        return FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .Where(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.IsAlive
                && Flat(a.transform.position - home).magnitude < territoryRadius)
            .OrderBy(a => (a.transform.position - transform.position).sqrMagnitude)
            .FirstOrDefault();
    }

    /// <summary>
    /// Bajo tierra se acerca al jugador y sale delante de el, mirandolo.
    /// </summary>
    void MoveUnderground(MarsFusionAvatar target)
    {
        Vector3 spot;
        if (target != null)
        {
            Vector2 around = Random.insideUnitCircle.normalized * emergeDistanceFromPlayer;
            spot = target.transform.position + new Vector3(around.x, 0f, around.y);
        }
        else
        {
            Vector2 around = Random.insideUnitCircle * territoryRadius * .6f;
            spot = home + new Vector3(around.x, 0f, around.y);
        }
        if (Flat(spot - home).magnitude > territoryRadius) spot = home + Vector3.ClampMagnitude(Flat(spot - home), territoryRadius);
        transform.position = OnGround(spot);
        Vector3 look = Flat((target != null ? target.transform.position : home) - transform.position);
        if (look.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(look);
    }

    void Bite(MarsFusionAvatar target)
    {
        if (target == null || damage <= 0) return;
        Vector3 delta = Flat(target.transform.position - transform.position);
        if (delta.magnitude > attackRange + 1f || Vector3.Angle(transform.forward, delta) > 70f) return;
        target.TakeMonsterDamage(damage);
        if (target.IsAlive && target.Object != null)
        {
            // Lo atrapa entre los dientes hasta que lo liberen a balazos.
            GrabbedPlayer = target.Object.StateAuthority;
            FreeHits = 0;
            nextHoldDamage = Runner.SimulationTime + holdDamageInterval;
            Enter(State.Hold, holdSeconds);
        }
    }

    MarsFusionAvatar Grabbed()
    {
        if (GrabbedPlayer == PlayerRef.None) return null;
        return FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .FirstOrDefault(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.Object.StateAuthority == GrabbedPlayer);
    }

    // Cada balazo mientras sujeta a alguien cuenta para liberarlo.
    public void TakeDamage(int amount)
    {
        if (Object == null || !Object.IsValid || amount <= 0) return;
        RPC_Shot(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_Shot(int amount)
    {
        if (amount <= 0 || Health <= 0 || Current == State.Hidden || Current == State.Burrow || Current == State.Death) return;
        int previousHealth = Health;
        Health = Mathf.Max(0, Health - amount);
        RPC_CombatDamage(previousHealth - Health);
        if (Current == State.Hold) FreeHits++;
        if (Health <= 0)
        {
            GrabbedPlayer = PlayerRef.None;
            Enter(State.Death, Mathf.Max(1f, Clip("Enterrarse")));
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_CombatDamage(int actualDamage)
    {
        GetComponent<GameJam.Combat.EnemyCombatUI>()?.Damage(actualDamage);
    }

    /// <summary>
    /// En el cliente del atrapado: lo pega a la boca y bloquea sus controles.
    /// </summary>
    void UpdateHeldPlayer()
    {
        bool heldHere = Current == State.Hold && GrabbedPlayer != PlayerRef.None && GrabbedPlayer == Runner.LocalPlayer;
        if (heldHere)
        {
            holder = this;
            LocalPlayerHeld = true;
            MarsFusionAvatar me = Grabbed();
            var player = me != null ? me.GetComponent<Starter.ThirdPersonCharacter.Player>() : null;
            if (player != null && player.KCC != null)
            {
                // Centro de la boca: promedio de las tres mandibulas; los pies bajan para que el cuerpo quede dentro.
                Vector3 center = jaws.Length > 0 ? jaws.Aggregate(Vector3.zero, (s, j) => s + j.position) / jaws.Length : mouth.position;
                Vector3 grip = center - Vector3.up * .9f + Mathf.Sin(Time.time * 7f) * .1f * transform.right;
                player.KCC.SetPosition(grip);
            }
        }
        else if (holder == this)
        {
            holder = null;
            LocalPlayerHeld = false;
        }
    }

    void OnGUI()
    {
        if (Object == null || !Object.IsValid || Current != State.Hold || GrabbedPlayer == PlayerRef.None) return;
        bool mine = GrabbedPlayer == Runner.LocalPlayer;
        string text = mine
            ? $"¡EL GUSANO TE ATRAPÓ!  Que te disparen al gusano para soltarte ({FreeHits}/{hitsToFree})"
            : $"¡El gusano atrapó a un compañero!  Dispárale para liberarlo ({FreeHits}/{hitsToFree})";
        var style = new GUIStyle(GUI.skin.box) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(1f, .55f, .3f);
        GUI.Box(new Rect(Screen.width * .5f - 420f, Screen.height * .18f, 840f, 56f), text, style);
    }

    void OnDisable()
    {
        if (holder == this) { holder = null; LocalPlayerHeld = false; }
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
