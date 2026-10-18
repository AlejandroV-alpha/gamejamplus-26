using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(b_NeptuneDiamondRain))]
public sealed class b_NeptuneDiamondStorm : MonoBehaviour
{
    [Min(0)] public float damagePerSecond = 14;
    [Min(.1f)] public float damageInterval = .75f;
    [Min(0)] public float exposureGrace = 1.5f;
    [Min(0)] public float warningSeconds = 6;
    public LayerMask shelterLayers = 1;
    public float shelterCheckDistance = 200;

    sealed class Exposure {public float exposed, tick;}
    static readonly List<IWeatherDamageTarget> targets = new List<IWeatherDamageTarget>();
    readonly Dictionary<IWeatherDamageTarget,Exposure> exposures = new Dictionary<IWeatherDamageTarget,Exposure>();
    readonly RaycastHit[] shelterHits = new RaycastHit[32];
    static readonly Vector3[] probes = {Vector3.zero,Vector3.right*.3f,Vector3.left*.3f,Vector3.forward*.3f,Vector3.back*.3f};
    b_NeptuneDiamondRain rain;
    bool hasLocalPlayer, sheltered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() {targets.Clear();}
    public static void Register(IWeatherDamageTarget target) {if(target!=null && !targets.Contains(target))targets.Add(target);}
    public static void Unregister(IWeatherDamageTarget target) {targets.Remove(target);}
    void OnEnable() {rain=GetComponent<b_NeptuneDiamondRain>();}

    void Update()
    {
        hasLocalPlayer=false;
        for(int i=targets.Count-1;i>=0;i--)
        {
            var target=targets[i];
            if(target is Object obj && obj==null) {targets.RemoveAt(i);exposures.Remove(target);continue;}
            if(!target.CanReceiveWeatherDamage) {exposures.Remove(target);continue;}
            if(!hasLocalPlayer)rain.SetStormTime(target.WeatherTime);
            hasLocalPlayer=true;
            bool protectedByIce=IsSheltered(target.WeatherTransform);
            sheltered=protectedByIce;
            TickExposure(target,rain.IsRaining,protectedByIce,Mathf.Min(Time.deltaTime,.25f));
        }
        if(!hasLocalPlayer)rain.ClearStormTime();
    }

    public bool IsSheltered(Transform player)
    {
        if(player==null)return false;
        var head=player.position+Vector3.up*1.7f;
        // Rain drifts (2,-9,-.5): check back along its approach, including shoulders.
        var skyDirection=new Vector3(-2,9,.5f).normalized;
        foreach(var offset in probes)
        {
            int count=Physics.RaycastNonAlloc(head+offset,skyDirection,shelterHits,Mathf.Max(1,shelterCheckDistance),shelterLayers,QueryTriggerInteraction.Ignore);
            bool blocked=false;
            for(int i=0;i<count;i++)
            {
                var collider=shelterHits[i].collider;
                if(collider==null || collider.transform.IsChildOf(player) || collider is TerrainCollider)continue;
                // Dynamic characters and loose items cannot act as roofs.
                if(collider is CharacterController || collider.attachedRigidbody!=null)continue;
                blocked=true;break;
            }
            if(!blocked)return false;
        }
        return true;
    }

    void TickExposure(IWeatherDamageTarget target,bool raining,bool covered,float deltaTime)
    {
        if(!exposures.TryGetValue(target,out var state)) {state=new Exposure();exposures.Add(target,state);}
        if(!raining || covered || !target.CanReceiveWeatherDamage) {state.exposed=state.tick=0;return;}
        float previous=state.exposed;
        state.exposed+=Mathf.Max(0,deltaTime);
        // Only the part of the frame after the grace period contributes damage.
        state.tick+=Mathf.Max(0,state.exposed-exposureGrace)-Mathf.Max(0,previous-exposureGrace);
        float interval=Mathf.Max(.1f,damageInterval);
        if(state.tick<interval)return;
        int ticks=Mathf.FloorToInt(state.tick/interval);
        state.tick-=ticks*interval;
        target.ReceiveWeatherDamage(Mathf.Max(0,damagePerSecond)*interval*ticks);
    }

    void OnGUI()
    {
        if(!hasLocalPlayer || rain==null)return;
        string message;
        if(rain.IsRaining)message=sheltered?"BAJO EL HIELO: A SALVO":"LLUVIA DE DIAMANTES: BUSCA UN TECHO DE HIELO";
        else if(rain.SecondsUntilRain<=warningSeconds)message="LLUVIA DE DIAMANTES EN "+Mathf.CeilToInt(rain.SecondsUntilRain)+" s · BUSCA REFUGIO";
        else return;
        var previous=GUI.color;
        GUI.color=rain.IsRaining&&!sheltered?new Color(1,.5f,.6f):new Color(.65f,.85f,1);
        float width=Mathf.Min(560,Screen.width-20);
        GUI.Box(new Rect((Screen.width-width)*.5f,20,width,44),message);
        GUI.color=previous;
    }

    void OnDisable() {exposures.Clear();hasLocalPlayer=false;if(rain!=null)rain.ClearStormTime();}
}
