using Fusion;
using UnityEngine;

namespace GameJam.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class PlayerExplorationBoundary : NetworkBehaviour
    {
        [Networked] private TickTimer returnDeadline { get; set; }
        private MarsFusionAvatar avatar;
        private ExplorationZone zone;
        private GUIStyle title, countdown, detail;
        public bool IsOutside => returnDeadline.IsRunning;
        public float RemainingSeconds => Object != null && Object.IsValid
            ? returnDeadline.RemainingTime(Runner).GetValueOrDefault() : 0;

        public override void Spawned() { avatar=GetComponent<MarsFusionAvatar>(); }
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (zone == null || !zone.isActiveAndEnabled)
                zone=ExplorationZone.ForPlayer(this);
            if (zone == null || avatar == null || !avatar.IsAlive || zone.Contains(transform.position))
            {
                returnDeadline=TickTimer.None;
                return;
            }
            if (!returnDeadline.IsRunning)
            {
                returnDeadline=TickTimer.CreateFromSeconds(Runner,zone.returnSeconds);
                Debug.Log("[Exploration zone] Fuera de zona: vuelve en "+zone.returnSeconds+" segundos.",this);
            }
            if (returnDeadline.Expired(Runner))
            {
                if (avatar.HealthSource != null)
                {
                    avatar.TakeDamage(Mathf.CeilToInt(avatar.HealthSource.CurrentHealth));
                    returnDeadline=TickTimer.None;
                    Debug.Log("[Exploration zone] Tiempo agotado: dano letal aplicado.",this);
                }
            }
        }
        private void OnGUI()
        {
            if (Object == null || !Object.IsValid || !HasStateAuthority || !IsOutside || avatar == null || !avatar.IsAlive) return;
            if (title == null)
            {
                title=new GUIStyle(GUI.skin.label) {alignment=TextAnchor.MiddleCenter,fontSize=23,fontStyle=FontStyle.Bold};
                title.normal.textColor=Color.white;
                countdown=new GUIStyle(title) {fontSize=42};
                countdown.normal.textColor=new Color(1,.45f,.2f);
                detail=new GUIStyle(title) {fontSize=15,fontStyle=FontStyle.Normal};
            }
            Matrix4x4 previous=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3(Screen.width*.5f,0,0),Quaternion.identity,Vector3.one*scale);
            Color old=GUI.color;
            GUI.color=new Color(.12f,.025f,.02f,.94f);
            GUI.DrawTexture(new Rect(-270,65,540,150),Texture2D.whiteTexture);
            GUI.color=Color.white;
            GUI.Label(new Rect(-260,73,520,34),"REGRESA A LA ZONA EXPLORABLE",title);
            GUI.Label(new Rect(-260,108,520,57),Mathf.CeilToInt(Mathf.Max(0,RemainingSeconds)).ToString("00"),countdown);
            GUI.Label(new Rect(-260,171,520,30),"Vuelve antes de que termine el contador",detail);
            GUI.color=old; GUI.matrix=previous;
        }
    }
}
