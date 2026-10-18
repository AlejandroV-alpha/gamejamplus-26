using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace JungleNightmare {
    public sealed class FrostGroundFX : MonoBehaviour {
        public Transform[] feet = new Transform[4];
        public LayerMask groundMask = ~0;
        public Material footprintMaterial, dustMaterial, shardMaterial;
        [Min(8)] public int footprintPoolSize = 96;
        [Min(.2f)] public float footprintLifetime = 4.5f;
        public float pawWidth = .105f, contactHeight = .085f, movementSpeed;
        public float minimumSpeed = .15f;
        public int dustPerStep = 9, shardsPerStep = 5;
        public bool allowRunningInPlace;
        [Tooltip("Brillo de las huellas (>1 brilla con Bloom).")] public float glowIntensity = 4f;
        public Color footprintColor = new Color(.4f,.85f,1,.8f);
        float Size => Mathf.Max(.01f, transform.lossyScale.x);
        [HideInInspector] public bool suppressSteps;
        public int ActiveFootprints { get; private set; }
        public int TotalSteps { get; private set; }
        public int AttackBursts { get; private set; }
        sealed class Mark { public Transform t; public MeshRenderer renderer; public float born = -100; }
        Mark[] marks;
        bool[] wasContact;
        float[] lastStep;
        int cursor;
        GameObject worldRoot;
        ParticleSystem dust, shards;
        Mesh pawMesh, shardMesh;
        MaterialPropertyBlock props;
        RaycastHit[] hits = new RaycastHit[24];
        static readonly int Tint = Shader.PropertyToID("_Tint");

        void Awake() {
            props = new MaterialPropertyBlock();
            wasContact = new bool[feet.Length]; lastStep = new float[feet.Length];
            for (int i=0;i<lastStep.Length;i++) lastStep[i]=-1;
            worldRoot = new GameObject(name + " — frost in world");
            pawMesh = MakePaw(); shardMesh = MakeShard();
            marks = new Mark[Mathf.Max(8,footprintPoolSize)];
            for (int i=0;i<marks.Length;i++) {
                var go = new GameObject("Frost paw " + i); go.transform.SetParent(worldRoot.transform);
                go.AddComponent<MeshFilter>().sharedMesh=pawMesh;
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=footprintMaterial;
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                go.SetActive(false); marks[i]=new Mark{t=go.transform,renderer=r};
            }
            dust=MakeParticles("Ice powder",dustMaterial,false);
            shards=MakeParticles("Ice crystals",shardMaterial,true);
        }
        ParticleSystem MakeParticles(string label, Material mat, bool crystal) {
            var go=new GameObject(label);go.transform.SetParent(worldRoot.transform);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;main.duration=1;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=crystal?600:1000;
            main.startLifetime=crystal?.65f:.9f;main.startSpeed=0;main.gravityModifier=crystal?.7f:.02f;
            main.startRotation3D=crystal;
            var emission=ps.emission;emission.enabled=false;
            var shape=ps.shape;shape.enabled=false;
            var color=ps.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.08f),new GradientAlphaKey(.5f,.5f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,crystal?1:.45f,1,crystal?.25f:2.3f));
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            if(crystal){renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=shardMesh;}
            ps.Play();return ps;
        }
        bool Ground(Vector3 point, out RaycastHit nearest) {
            int count=Physics.RaycastNonAlloc(point+Vector3.up*.22f*Size,Vector3.down,hits,.65f*Size,groundMask,QueryTriggerInteraction.Ignore);
            nearest=default;float distance=float.PositiveInfinity;
            for(int i=0;i<count;i++) {
                if(hits[i].transform.IsChildOf(transform) || hits[i].normal.y<.35f)continue;
                if(hits[i].distance<distance){distance=hits[i].distance;nearest=hits[i];}
            }
            return distance<float.PositiveInfinity;
        }
        void LateUpdate() {
            if(marks==null)return;
            bool moving=!suppressSteps && (movementSpeed>=minimumSpeed || allowRunningInPlace);
            for(int i=0;i<feet.Length;i++) {
                if(!feet[i])continue;
                bool contact=Ground(feet[i].position,out var hit) && feet[i].position.y-hit.point.y<=contactHeight*Size;
                if(moving && contact && !wasContact[i] && Time.time-lastStep[i]>.12f) {
                    Stamp(hit.point,hit.normal,transform.forward);
                    EmitIce(hit.point+hit.normal*.025f,dustPerStep,shardsPerStep,Size);
                    lastStep[i]=Time.time;TotalSteps++;
                }
                wasContact[i]=contact;
            }
            ActiveFootprints=0;
            foreach(var m in marks) {
                float age=(Time.time-m.born)/Mathf.Max(.2f,footprintLifetime);
                if(age>=1){if(m.t.gameObject.activeSelf)m.t.gameObject.SetActive(false);continue;}
                ActiveFootprints++;
                float alpha=footprintColor.a*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,age)));
                props.SetColor(Tint,Glow(alpha));m.renderer.SetPropertyBlock(props);
            }
        }
        Color Glow(float alpha){var c=footprintColor*glowIntensity;c.a=alpha;return c;}
        void Stamp(Vector3 p,Vector3 normal,Vector3 direction) {
            var mark=marks[cursor++%marks.Length];Vector3 forward=Vector3.ProjectOnPlane(direction,normal).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.Cross(normal,Vector3.right).normalized;
            mark.t.SetPositionAndRotation(p+normal*.006f,Quaternion.LookRotation(forward,normal));
            mark.t.localScale=Vector3.one*pawWidth*Size;mark.born=Time.time;mark.t.gameObject.SetActive(true);
            props.SetColor(Tint,Glow(footprintColor.a));mark.renderer.SetPropertyBlock(props);
        }
        void EmitIce(Vector3 p,int powderCount,int crystalCount,float strength) {
            for(int i=0;i<powderCount;i++) {
                var v=Random.insideUnitCircle;var ep=new ParticleSystem.EmitParams {
                    position=p+new Vector3(v.x,0,v.y)*.035f,
                    velocity=(new Vector3(v.x*.18f,Random.Range(.08f,.25f),v.y*.18f)-transform.forward*.1f)*strength,
                    startSize=Random.Range(.055f,.12f)*strength,startLifetime=Random.Range(.55f,1.0f),
                    startColor=new Color(.55f,.86f,1,.55f)};dust.Emit(ep,1);
            }
            for(int i=0;i<crystalCount;i++) {
                var v=Random.insideUnitCircle;var ep=new ParticleSystem.EmitParams {
                    position=p,velocity=new Vector3(v.x*.55f,Random.Range(.35f,.9f),v.y*.55f)*strength,
                    startSize=Random.Range(.018f,.045f)*strength,startLifetime=Random.Range(.3f,.65f),
                    startColor=new Color(.65f,.95f,1,.9f),rotation3D=Random.insideUnitSphere*180,
                    angularVelocity3D=Random.insideUnitSphere*180};shards.Emit(ep,1);
            }
        }
        public void AttackBurst() {
            if(marks==null)return;
            Vector3 p=transform.position+transform.forward*.85f;
            if(!Ground(p,out var hit))return;
            AttackBursts++;
            EmitIce(hit.point+hit.normal*.04f,42,36,1.7f);
            for(int i=0;i<9;i++) {
                float a=(i-4)*.19f;Vector3 dir=Quaternion.AngleAxis(a*Mathf.Rad2Deg,hit.normal)*transform.forward;
                // Every fragment is projected independently to avoid floating on slopes.
                if(Ground(hit.point+dir*.24f,out var h))Stamp(h.point,h.normal,dir);
            }
        }
        static Mesh MakePaw() {
            var v=new List<Vector3>();var t=new List<int>();
            AddOval(v,t,0,-.12f,.32f,.35f);
            for(int i=0;i<4;i++){float x=(i-1.5f)*.23f;AddOval(v,t,x,.26f+(1-Mathf.Abs(i-1.5f)/1.5f)*.1f,.105f,.16f);}
            var m=new Mesh{name="Four-toed frost footprint"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        static void AddOval(List<Vector3> v,List<int> t,float x,float z,float rx,float rz) {
            int b=v.Count;v.Add(new Vector3(x,0,z));
            for(int i=0;i<16;i++){float a=i*Mathf.PI/8;v.Add(new Vector3(x+Mathf.Sin(a)*rx,0,z+Mathf.Cos(a)*rz));}
            for(int i=0;i<16;i++){t.Add(b);t.Add(b+1+i);t.Add(b+1+(i+1)%16);}
        }
        static Mesh MakeShard() {
            var m=new Mesh{name="Ice crystal"};m.vertices=new[]{new Vector3(0,1.5f,0),new Vector3(0,-1,0),Vector3.right,Vector3.forward,Vector3.left,Vector3.back};
            m.triangles=new[]{0,2,3,0,3,4,0,4,5,0,5,2,1,3,2,1,4,3,1,5,4,1,2,5};m.RecalculateNormals();return m;
        }
        void OnDisable(){if(worldRoot)worldRoot.SetActive(false);}
        void OnEnable(){if(worldRoot)worldRoot.SetActive(true);}
        void OnDestroy(){if(worldRoot)Destroy(worldRoot);if(pawMesh)Destroy(pawMesh);if(shardMesh)Destroy(shardMesh);}
    }
}

// Integrated into gamejamplus-26.
