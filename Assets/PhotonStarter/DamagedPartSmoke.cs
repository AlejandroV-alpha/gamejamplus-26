using UnityEngine;
using UnityEngine.Rendering;
using GameJam.Collectibles;

namespace GameJam.Multiplayer
{
    // Only the three mission parts emit smoke. Presentation stays local on each peer.
    public sealed class DamagedPartSmoke : MonoBehaviour
    {
        public Material smokeMaterial;
        [Min(20)] public float columnHeight=100;
        [Range(24,120)] public int particleBudget=80;
        public Vector3 wind=new Vector3(.6f,8,.25f);
        private MaterialPickup pickup;
        private ParticleSystem smoke;
        private void Awake(){pickup=GetComponent<MaterialPickup>();}
        private void Update()
        {
            bool available=pickup!=null && pickup.Object!=null && pickup.Object.IsValid && pickup.IsAvailable();
            if(!available){if(smoke!=null && smoke.isPlaying)smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return;}
            if(smoke==null && smokeMaterial!=null)CreateSmoke();
            if(smoke!=null && !smoke.isPlaying)smoke.Play();
        }
        private void CreateSmoke()
        {
            var go=new GameObject("Broken Part Smoke");go.transform.SetParent(transform,false);
            // Ignore the wreck's rotation: smoke always rises in world space.
            go.transform.rotation=Quaternion.identity;
            go.transform.position=transform.position+Vector3.up*.3f;
            smoke=go.AddComponent<ParticleSystem>();smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=smoke.main;main.loop=true;main.playOnAwake=false;main.prewarm=true;
            main.startLifetime=columnHeight/Mathf.Max(1,wind.y);main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(2.5f,4);
            main.maxParticles=particleBudget;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=smoke.emission;emission.rateOverTime=Mathf.Min(6,particleBudget/main.startLifetime.constant);
            var shape=smoke.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.7f;
            var velocity=smoke.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=wind.x;velocity.y=wind.y;velocity.z=wind.z;
            var size=smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,4));
            var colors=smoke.colorOverLifetime;colors.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(new Color(.13f,.10f,.085f),0),new GradientColorKey(new Color(.30f,.25f,.22f),1)},
                new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(.6f,.6f),new GradientAlphaKey(0,1)});colors.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=smokeMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            smoke.Play();
        }
        private void OnDisable(){if(smoke!=null)smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        private void OnDrawGizmosSelected(){Gizmos.color=new Color(.9f,.6f,.15f);Gizmos.DrawLine(transform.position,transform.position+Vector3.up*columnHeight);}
    }
}
