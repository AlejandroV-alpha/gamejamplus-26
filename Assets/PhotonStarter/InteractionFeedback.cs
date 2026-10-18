using UnityEngine;
using UnityEngine.Rendering;
namespace GameJam.Multiplayer
{
    public static class InteractionFeedback
    {
        public static void Burst(Vector3 position, Material material, Color color, int count=18)
        {
            if(material==null) return;
            var go=new GameObject("Interaction feedback");go.transform.position=position;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=.5f;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.25f,.65f);main.startSpeed=new ParticleSystem.MinMaxCurve(.5f,2);
            main.startSize=new ParticleSystem.MinMaxCurve(.04f,.12f);main.startColor=color;main.maxParticles=24;main.gravityModifier=.15f;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.enabled=false;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.15f;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            ps.Play();ps.Emit(Mathf.Min(count,24));Object.Destroy(go,1.2f);
        }
    }
}
