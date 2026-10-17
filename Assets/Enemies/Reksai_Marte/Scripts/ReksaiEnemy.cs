using System;
using System.Linq;
using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;
// The server controls AI and hits. The player owner updates its existing health stat.
public sealed class ReksaiEnemy : HitProcessor
{
 public enum Mode { Idle, Run, Attack1, Attack2, Burrow, Hidden, Emerge, Death }
 [SerializeField] float maxHealth=160, detectionRange=28, leashRange=65, speed=6.5f, attackRange=3.8f, damage=18, burrowCooldown=18;
 [SerializeField, Min(.1f)] float attackSpeed=1.5f;
 [SerializeField, Min(0f)] float attackRecovery=.35f;
 [SerializeField, Min(1f)] float undergroundSpeed=20f;
 [SerializeField, Min(.5f)] float undergroundMaxDuration=1.8f;
 [SerializeField, Min(1.5f)] float emergeDistance=2.2f;
 readonly NetworkVariable<int> mode=new NetworkVariable<int>((int)Mode.Idle);
 readonly NetworkVariable<double> stateStarted=new NetworkVariable<double>(0);
 readonly NetworkVariable<float> health=new NetworkVariable<float>(160);
 Animator animator; Transform visual; Vector3 visualRest,home; Collider bodyCollider;
 CoreStatsHandler target; float scanAt,attackAt,burrowAt; bool hitApplied,alternate; int shown=-1;
 public Mode CurrentMode => (Mode)mode.Value;
 public float Health => health.Value;
 public override void OnNetworkSpawn()
 {
  animator=GetComponentInChildren<Animator>();visual=animator.transform;visualRest=visual.localPosition;bodyCollider=GetComponent<Collider>();home=transform.position;
  if(IsServer){health.Value=maxHealth;stateStarted.Value=NetworkManager.ServerTime.Time;burrowAt=Time.time+burrowCooldown;}
  RenderState();
 }
 void Update()
 {
  if(!IsSpawned)return;
  if(IsServer)TickServer();
  RenderState();
 }
 float Elapsed => (float)(NetworkManager.ServerTime.Time-stateStarted.Value);
 void SetMode(Mode value){if(CurrentMode==value)return;stateStarted.Value=NetworkManager.ServerTime.Time;mode.Value=(int)value;hitApplied=false;}
 void TickServer()
 {
  var state=CurrentMode;
  if(state==Mode.Death){if(Elapsed>4)NetworkObject.Despawn(true);return;}
  if(Time.time>=scanAt){scanAt=Time.time+.35f;target=FindObjectsByType<CoreStatsHandler>(FindObjectsSortMode.None).Where(p=>p.IsSpawned&&p.IsAlive&&Vector3.Distance(home,p.transform.position)<leashRange).OrderBy(p=>(p.transform.position-transform.position).sqrMagnitude).FirstOrDefault();}
  if(state==Mode.Burrow){if(Elapsed>=ClipLength("spell2"))SetMode(Mode.Hidden);return;}
  if(state==Mode.Hidden)
  {
   bool close=false;
   if(target!=null && TryFindEmergencePoint(out var destination))
   {
    MoveTowards(destination,undergroundSpeed);
    var remaining=destination-transform.position;remaining.y=0;close=remaining.sqrMagnitude<=.25f;
    if(close)Face(target.transform.position);
   }
   if((close && Elapsed>=.35f)||Elapsed>=undergroundMaxDuration)SetMode(Mode.Emerge);
   return;
  }
  if(state==Mode.Emerge){if(!hitApplied&&Elapsed>=ClipLength("spell2knockup")*.48f){hitApplied=true;Strike(damage*.7f,attackRange+.7f);}if(Elapsed>=ClipLength("spell2knockup")){attackAt=Time.time+.5f;SetMode(Mode.Idle);}return;}
  if(state==Mode.Attack1||state==Mode.Attack2)
  {
   if(target!=null)Face(target.transform.position);
   var duration=ClipLength(state==Mode.Attack1?"attack1":"attack2")/Mathf.Max(.1f,attackSpeed);
   if(!hitApplied&&Elapsed>=duration*.42f){hitApplied=true;Strike(damage,attackRange+.2f);}
   if(Elapsed>=duration){attackAt=Time.time+attackRecovery;SetMode(Mode.Idle);}return;
  }
  if(target==null||Vector3.Distance(transform.position,target.transform.position)>detectionRange)
  {if(Vector3.Distance(transform.position,home)>1){SetMode(Mode.Run);MoveTowards(home,speed*.6f);}else SetMode(Mode.Idle);return;}
  float distance=Vector3.Distance(transform.position,target.transform.position);
  if(Time.time>=burrowAt&&distance>5&&distance<22){burrowAt=Time.time+burrowCooldown;SetMode(Mode.Burrow);return;}
  if(distance<=attackRange){if(Time.time>=attackAt){alternate=!alternate;SetMode(alternate?Mode.Attack1:Mode.Attack2);}else SetMode(Mode.Idle);}
  else{SetMode(Mode.Run);MoveTowards(target.transform.position,speed);}
 }
 void Face(Vector3 point){var d=point-transform.position;d.y=0;if(d.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(d),240*Time.deltaTime);}
 void MoveTowards(Vector3 point,float moveSpeed)
 {
  var d=point-transform.position;d.y=0;if(d.sqrMagnitude<.1f)return;d.Normalize();
  if(CurrentMode!=Mode.Hidden)
  {
   var hits=Physics.SphereCastAll(transform.position+Vector3.up*.9f,.45f,d,1.5f,~0,QueryTriggerInteraction.Ignore);
   var obstacle=hits.FirstOrDefault(h=>!(h.collider is TerrainCollider)&&!h.collider.transform.IsChildOf(transform)&&h.collider.GetComponentInParent<CoreStatsHandler>()==null);
   if(obstacle.collider!=null){d=Vector3.ProjectOnPlane(d,obstacle.normal);d.y=0;if(d.sqrMagnitude<.01f)d=Vector3.Cross(Vector3.up,obstacle.normal);d.Normalize();}
  }
  Face(transform.position+d);var next=transform.position+d*Mathf.Min(moveSpeed*Time.deltaTime,Vector3.ProjectOnPlane(point-transform.position,Vector3.up).magnitude);
  var terrain=Terrain.activeTerrains.FirstOrDefault(t=>next.x>=t.transform.position.x&&next.z>=t.transform.position.z&&next.x<=t.transform.position.x+t.terrainData.size.x&&next.z<=t.transform.position.z+t.terrainData.size.z);
  if(terrain!=null)next.y=terrain.SampleHeight(next)+terrain.transform.position.y;
  transform.position=next;
 }
 // Approach from the enemy's side and keep the emergence capsule off props and the player.
 bool TryFindEmergencePoint(out Vector3 point)
 {
  var direction=transform.position-target.transform.position;direction.y=0;
  if(direction.sqrMagnitude<.01f)direction=-target.transform.forward;
  direction.Normalize();
  for(int ring=0;ring<2;ring++)for(int i=0;i<8;i++)
  {
   float angle=i==0?0:((i+1)/2)*45*(i%2==1?1:-1);
   var candidate=target.transform.position+Quaternion.Euler(0,angle,0)*direction*(emergeDistance+ring*1.2f);
   var terrain=Terrain.activeTerrains.FirstOrDefault(t=>candidate.x>=t.transform.position.x&&candidate.z>=t.transform.position.z&&candidate.x<=t.transform.position.x+t.terrainData.size.x&&candidate.z<=t.transform.position.z+t.terrainData.size.z);
   if(terrain!=null)candidate.y=terrain.SampleHeight(candidate)+terrain.transform.position.y;
   bool blocked=Physics.OverlapCapsule(candidate+Vector3.up*.8f,candidate+Vector3.up*1.7f,.85f,~0,QueryTriggerInteraction.Ignore)
    .Any(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(transform));
   if(!blocked){point=candidate;return true;}
  }
  point=transform.position;return false;
 }
 void Strike(float amount,float range)
 {
  if(target==null||!target.IsSpawned||!target.IsAlive)return;
  var delta=target.transform.position-transform.position;delta.y=0;
  if(delta.magnitude>range||Vector3.Angle(transform.forward,delta)>85)return;
  var from=transform.position+Vector3.up;var to=target.transform.position+Vector3.up;
  var blocked=Physics.RaycastAll(from,(to-from).normalized,(to-from).magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.collider.transform.IsChildOf(transform)&&!h.collider.transform.IsChildOf(target.transform)&&!(h.collider is TerrainCollider));
  if(blocked)return;
  DamagePlayerRpc(new NetworkObjectReference(target.NetworkObject), amount,
   RpcTarget.Single(target.OwnerClientId, RpcTargetUse.Temp));
 }
 [Rpc(SendTo.SpecifiedInParams)]
 void DamagePlayerRpc(NetworkObjectReference playerReference,float amount,RpcParams rpcParams=default)
 {
  if(rpcParams.Receive.SenderClientId!=Unity.Netcode.NetworkManager.ServerClientId)return;
  if(!playerReference.TryGet(out var playerObject))return;
  var stats=playerObject.GetComponent<CoreStatsHandler>();
  if(stats!=null&&stats.IsSpawned&&stats.IsOwner&&stats.IsAlive&& !float.IsNaN(amount) && !float.IsInfinity(amount) && amount>0 && amount<=damage)
   stats.ModifyStat(StatKeys.Health,-amount,ulong.MaxValue,ModificationSource.Damage);
 }
 protected override void HandleHit(HitInfo info)
 {
  if(!IsServer||!IsSpawned||CurrentMode==Mode.Death||CurrentMode==Mode.Hidden||CurrentMode==Mode.Burrow)return;
  if(float.IsNaN(info.amount)||float.IsInfinity(info.amount)||info.amount<=0)return;
  health.Value=Mathf.Max(0,health.Value-Mathf.Min(info.amount,maxHealth));
  if(health.Value<=0)SetMode(Mode.Death);
 }
 string Suffix(Mode state)
 {
  switch(state){case Mode.Run:return "runhaste";case Mode.Attack1:return "attack1";case Mode.Attack2:return "attack2";case Mode.Burrow:return "spell2";case Mode.Emerge:return "spell2knockup";case Mode.Death:return "death";default:return "idle1base";}
 }
 float ClipLength(string suffix){var c=animator.runtimeAnimatorController.animationClips.FirstOrDefault(a=>a.name.EndsWith(suffix,StringComparison.Ordinal));return c!=null?c.length:1;}
 float PlaybackSpeed(Mode state) => state==Mode.Attack1 || state==Mode.Attack2 ? Mathf.Max(.1f,attackSpeed) : state==Mode.Run ? 1.35f : 1f;
 void RenderState()
 {
  if(animator==null)return;var state=CurrentMode;float elapsed=Elapsed;animator.speed=PlaybackSpeed(state);
  if(shown!=mode.Value)
  {
   shown=mode.Value;var suffix=Suffix(state);var clip=animator.runtimeAnimatorController.animationClips.FirstOrDefault(a=>a.name.EndsWith(suffix,StringComparison.Ordinal));
   if(clip!=null)animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer."+clip.name.Split('|').Last()),.10f,0,Mathf.Min(elapsed*PlaybackSpeed(state),Mathf.Max(0,clip.length-.01f)));
  }
  float depth=state==Mode.Hidden?4:state==Mode.Burrow?4*Mathf.SmoothStep(0,1,elapsed/ClipLength("spell2")):state==Mode.Emerge?4*(1-Mathf.SmoothStep(0,1,elapsed/ClipLength("spell2knockup"))):0;
  visual.localPosition=visualRest+Vector3.down*depth;
  if(bodyCollider!=null)bodyCollider.enabled=state!=Mode.Hidden&&state!=Mode.Burrow&&state!=Mode.Death;
 }
}

