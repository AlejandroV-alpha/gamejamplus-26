using Fusion;
using UnityEngine;
using GameJam.Collectibles;
using GameJam.Ship;
using Starter.ThirdPersonCharacter;
using System.Collections.Generic;

namespace GameJam.Multiplayer
{
    [DefaultExecutionOrder(12000)]
    public sealed class PlayerInteractionAction : NetworkBehaviour
    {
        public Material feedbackMaterial;
        public Transform weaponVisual;
        [Min(.2f)] public float pickupSeconds=.8f;
        [Min(.5f)] public float repairSeconds=2.4f;
        [Networked] public int ActionKind {get;private set;}
        [Networked] private TickTimer deadline {get;set;}
        [Networked] private NetworkObject target {get;set;}
        private MarsFusionAvatar avatar;
        private Starter.ThirdPersonCharacter.PlayerInput input;
        private float startingHealth, nextSpark;
        private Transform spine, upperArm, lowerArm, hand;
        private Renderer[] weaponRenderers;
        private readonly Dictionary<Renderer,bool> weaponVisibility=new Dictionary<Renderer,bool>();
        private readonly Dictionary<Transform,Quaternion> rest=new Dictionary<Transform,Quaternion>();
        public bool IsBusy => Object!=null && Object.IsValid && ActionKind!=0;
        public float Progress => !IsBusy?0:1-Mathf.Clamp01(deadline.RemainingTime(Runner).GetValueOrDefault()/(ActionKind==1?pickupSeconds:repairSeconds));
        public override void Spawned()
        {
            avatar=GetComponent<MarsFusionAvatar>();input=GetComponent<Starter.ThirdPersonCharacter.PlayerInput>();
            weaponRenderers=weaponVisual!=null?weaponVisual.GetComponentsInChildren<Renderer>(true):new Renderer[0];
            var animator=GetComponentInChildren<Animator>();
            if(animator!=null && animator.isHuman)
            {
                spine=animator.GetBoneTransform(HumanBodyBones.Spine);upperArm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);lowerArm=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            else foreach(var t in GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="Spine")spine=t;if(t.name=="RightArm"||t.name=="RightUpperArm")upperArm=t;if(t.name=="RightForeArm"||t.name=="RightLowerArm")lowerArm=t;if(t.name=="RightHand")hand=t;
            }
        }
        public bool TryCollect(MaterialPickup pickup) {return pickup!=null && pickup.IsAvailable() && Begin(1,pickup.Object);}
        public bool TryRepair(ShipRepairStation station)
        {
            if(station==null) return false;
            if(!station.CanBeginRepair()){station.RequestRepair();return false;}
            return Begin(2,station.Object);
        }
        private bool Begin(int kind,NetworkObject item)
        {
            if((GetComponent<GameJam.Survival.SurvivalRevive>()?.IsAnimating ?? false) || Object==null || !Object.IsValid || !HasStateAuthority || (GetComponent<GameJam.Combat.PlayerHurtReaction>()?.IsReacting ?? false) || IsBusy || item==null || !item.IsValid || item.Runner!=Runner || avatar==null || !avatar.IsAlive || avatar.TabletOpen || input==null || !input.ControlsEnabled) return false;
            var player=GetComponent<Player>();if(player!=null && player.KCC!=null && !player.KCC.IsGrounded) return false;
            ActionKind=kind;target=item;deadline=TickTimer.CreateFromSeconds(Runner,kind==1?pickupSeconds:repairSeconds);
            startingHealth=avatar.HealthSource!=null?avatar.HealthSource.CurrentHealth:0;
            input.ControlsEnabled=false;input.ResetInput();return true;
        }
        public override void FixedUpdateNetwork()
        {
            if(!HasStateAuthority || !IsBusy) return;
            float range=ActionKind==1?2.5f:3;
            if(avatar==null || !avatar.IsAlive || avatar.TabletOpen || target==null || !target.IsValid
                || Vector3.Distance(transform.position,target.transform.position)>range
                || (avatar.HealthSource!=null && avatar.HealthSource.CurrentHealth<startingHealth))
            {Cancel();return;}
            var player=GetComponent<Player>();
            Vector3 direction=target.transform.position-transform.position;direction.y=0;
            if(player!=null && player.KCC!=null && direction.sqrMagnitude>.01f)
                player.KCC.SetLookRotation(Quaternion.Slerp(player.KCC.TransformRotation,Quaternion.LookRotation(direction),Runner.DeltaTime*8).eulerAngles);
            if(!deadline.Expired(Runner))return;
            var completedTarget=target;int kind=ActionKind;Cancel();
            if(kind==1){var pickup=completedTarget.GetComponent<MaterialPickup>();if(pickup!=null)pickup.RequestCollection();}
            else {var station=completedTarget.GetComponent<ShipRepairStation>();if(station!=null)station.RequestRepair(true);}
        }
        private void Cancel(){ActionKind=0;deadline=TickTimer.None;target=null;}
        private void Update(){RestorePose();}
        private void BendForward(Transform bone,float angle)
        {
            if(bone==null)return;rest[bone]=bone.localRotation;
            bone.rotation=Quaternion.AngleAxis(angle,transform.right)*bone.rotation;
        }
        private void Reach(Transform bone,Transform end,Vector3 point,float weight)
        {
            if(bone==null || end==null)return;
            Vector3 from=end.position-bone.position,to=point-bone.position;
            if(from.sqrMagnitude<.0001f || to.sqrMagnitude<.0001f)return;
            rest[bone]=bone.localRotation;
            bone.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(from,to),weight)*bone.rotation;
        }
        private void LateUpdate()
        {
            if(!IsBusy || avatar==null || !avatar.IsAlive || (GetComponent<GameJam.Combat.PlayerHurtReaction>()?.IsReacting ?? false)){RestoreWeapon();return;}
            foreach(var renderer in weaponRenderers) if(renderer!=null)
            {
                if(!weaponVisibility.ContainsKey(renderer))weaponVisibility[renderer]=renderer.enabled;
                renderer.enabled=false;
            }
            float p=Progress;
            float weight=Mathf.SmoothStep(0,1,Mathf.Min(p/.2f,(1-p)/.2f));
            float work=ActionKind==2?Mathf.Sin(p*Mathf.PI*12)*8:0;
            BendForward(spine,(ActionKind==1?32:18)*weight);
            Vector3 point=target!=null?target.transform.position+Vector3.up*(ActionKind==1?.3f:.9f):transform.position+transform.forward;
            point+=Vector3.up*work*.012f;
            Reach(upperArm,lowerArm,point,weight*.85f);
            Reach(lowerArm,hand,point,weight*.8f);
            if(ActionKind==2 && Time.time>=nextSpark && target!=null)
            {
                nextSpark=Time.time+.4f;
                InteractionFeedback.Burst(target.transform.position+Vector3.up*.8f,feedbackMaterial,new Color(1,.65f,.15f),3);
            }
        }
        private void RestorePose(){foreach(var entry in rest)if(entry.Key!=null)entry.Key.localRotation=entry.Value;rest.Clear();}
        private void RestoreWeapon(){foreach(var entry in weaponVisibility)if(entry.Key!=null)entry.Key.enabled=entry.Value;weaponVisibility.Clear();}
        private void OnDisable(){RestorePose();RestoreWeapon();}
        public override void Despawned(NetworkRunner runner,bool hasState){RestorePose();RestoreWeapon();}
        private void OnGUI()
        {
            if(!IsBusy || !HasStateAuthority)return;
            float width=Mathf.Min(340,Screen.width*.65f),x=(Screen.width-width)/2,y=Screen.height*.76f;
            GUI.Box(new Rect(x,y,width,48),ActionKind==1?"Recogiendo…":"Reparando…");
            var old=GUI.color;GUI.color=new Color(.25f,1,.75f);GUI.DrawTexture(new Rect(x+8,y+34,(width-16)*Progress,5),Texture2D.whiteTexture);GUI.color=old;
        }
    }
}
