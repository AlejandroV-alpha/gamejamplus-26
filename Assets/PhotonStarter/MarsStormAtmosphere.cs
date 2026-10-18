using GameJam.Environment.Mars;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Multiplayer
{
    // One local particle budget per view, independent of map size or remote players.
    [DefaultExecutionOrder(11000)]
    public sealed class MarsStormAtmosphere : MonoBehaviour
    {
        public SandstormSpawner storm;
        public Material dustMaterial;
        [Min(8)] public float visibilityDistance = 38;
        [Range(20,300)] public int particleBudget = 180;
        public Vector3 wind = new Vector3(11,-.3f,3);
        private bool oldEnabled, initialized;
        private FogMode oldMode;
        private Color oldColor;
        private float oldStart, oldEnd, oldDensity, blend, emissionBudget;
        private ParticleSystem dust;
        private Camera view;
        private void Start()
        {
            initialized=true; oldEnabled=RenderSettings.fog; oldMode=RenderSettings.fogMode;
            oldColor=RenderSettings.fogColor; oldStart=RenderSettings.fogStartDistance;
            oldEnd=RenderSettings.fogEndDistance; oldDensity=RenderSettings.fogDensity;
        }
        private void LateUpdate()
        {
            bool active=storm!=null && storm.Object!=null && storm.Object.IsValid && storm.IsStormActive();
            blend=Mathf.MoveTowards(blend,active?1:0,Time.deltaTime*(active?.4f:.25f));
            if(blend<=0) { Restore(); ClearDust(); return; }
            RenderSettings.fog=true;
            RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=Color.Lerp(oldColor,new Color(.62f,.29f,.14f),blend);
            float clearEnd=oldMode==FogMode.Linear ? oldEnd : 2/Mathf.Max(.0001f,oldDensity);
            RenderSettings.fogStartDistance=Mathf.Lerp(oldMode==FogMode.Linear?oldStart:0,3,blend);
            RenderSettings.fogEndDistance=Mathf.Lerp(clearEnd,visibilityDistance,blend);
            if(view==null || !view.isActiveAndEnabled) view=Camera.main;
            if(view==null || dustMaterial==null) return;
            if(dust==null) CreateDust();
            // World-space particles retain their path when the player turns the camera.
            dust.transform.position=view.transform.position;
            emissionBudget+=Time.deltaTime*60*blend;
            int count=Mathf.Min(12,Mathf.FloorToInt(emissionBudget));
            emissionBudget-=count;
            for(int i=0;i<count;i++)
            {
                Vector3 offset=view.transform.rotation*new Vector3(Random.Range(-12f,12f),Random.Range(-6f,6f),Random.Range(3f,20f));
                var particle=new ParticleSystem.EmitParams();
                particle.position=view.transform.position+offset;
                particle.velocity=wind+Random.insideUnitSphere*1.8f;
                particle.startLifetime=Random.Range(1.5f,3);
                particle.startSize=Random.Range(1.2f,4f);
                particle.startColor=new Color(.75f,.4f,.2f,.45f*blend);
                dust.Emit(particle,1);
            }
        }
        private void CreateDust()
        {
            var go=new GameObject("Local Storm Dust");
            go.transform.SetParent(transform,false);
            dust=go.AddComponent<ParticleSystem>();
            dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=dust.main; main.playOnAwake=false; main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.maxParticles=particleBudget; main.startSpeed=0; main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=dust.emission; emission.enabled=false;
            var shape=dust.shape; shape.enabled=false;
            var fade=dust.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});
            fade.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=dustMaterial; renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false; renderer.maxParticleSize=.25f;
            dust.Play();
        }
        private void ClearDust()
        {
            emissionBudget=0;
            if(dust!=null && dust.particleCount>0) dust.Clear();
        }
        private void Restore()
        {
            if (!initialized) return;
            RenderSettings.fog=oldEnabled; RenderSettings.fogMode=oldMode;
            RenderSettings.fogColor=oldColor; RenderSettings.fogStartDistance=oldStart;
            RenderSettings.fogEndDistance=oldEnd; RenderSettings.fogDensity=oldDensity;
        }
        private void OnDisable(){ Restore(); ClearDust(); }
        private void OnDestroy(){ Restore(); }
    }
}
