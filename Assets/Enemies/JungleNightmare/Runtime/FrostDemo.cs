using UnityEngine;
namespace JungleNightmare {
    public sealed class FrostDemo : MonoBehaviour {
        public FrostActor actor;
        float clock;
        bool attacked;
        public bool autoRun=true;
        void Update() {
            if(!actor || actor.IsDead)return;
            float phase=clock%7;
            if(autoRun && phase<5 && !actor.IsAttacking && !actor.IsReacting){
                float angle=clock*.7f;actor.transform.position=new Vector3(Mathf.Sin(angle)*1.8f,0,Mathf.Cos(angle)*1.8f);
                actor.transform.rotation=Quaternion.LookRotation(new Vector3(Mathf.Cos(angle),0,-Mathf.Sin(angle)));
                attacked=false;
            } else if(autoRun && !attacked){actor.Attack();attacked=true;}
            clock+=Time.deltaTime;
        }
        void OnGUI() {
            GUI.Box(new Rect(18,18,290,180),"JUNGLE NIGHTMARE — FROST");
            GUI.Label(new Rect(32,46,270,24),"Huellas · polvo helado · ataque");
            if(GUI.Button(new Rect(32,78,120,32),"Atacar"))actor.Attack();
            autoRun=GUI.Toggle(new Rect(170,84,125,25),autoRun,"Demo automática");
            if(GUI.Button(new Rect(32,125,120,32),"Recibir daño"))actor.ReceiveDamage();
            if(GUI.Button(new Rect(170,125,110,32),"Morir"))actor.Die();
        }
    }
}

// Integrated into gamejamplus-26.
