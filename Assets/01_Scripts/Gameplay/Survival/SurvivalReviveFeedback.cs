using GameJam.Multiplayer;
using UnityEngine;

namespace GameJam.Survival
{
    // Local HUD only. Progress comes from the target's authoritative reservation.
    [DisallowMultipleComponent]
    public sealed class SurvivalReviveFeedback : MonoBehaviour
    {
        private SurvivalRevive revive, observedTarget;
        private MarsFusionAvatar avatar;
        private string notice;
        private float noticeUntil;
        private bool completed, wasRescuing;

        private void Awake()
        {
            revive = GetComponent<SurvivalRevive>();
            avatar = GetComponent<MarsFusionAvatar>();
        }

        private bool IsLocal => revive != null && revive.Object != null
            && revive.Object.IsValid && revive.HasStateAuthority;

        private void Update()
        {
            if (!IsLocal) { observedTarget = null; wasRescuing = false; return; }
            var target = revive.RescueTarget;
            if (wasRescuing && (target == null || target != observedTarget))
            {
                var teammate = observedTarget != null ? observedTarget.GetComponent<MarsFusionAvatar>() : null;
                completed = observedTarget != null && observedTarget.Object != null && observedTarget.Object.IsValid
                    && teammate != null && teammate.IsAlive;
                notice = completed ? "Compañero reanimado" : "Reanimación cancelada";
                noticeUntil = Time.unscaledTime + 2f;
            }
            observedTarget = target;
            wasRescuing = target != null;
        }

        private GUIStyle promptStyle;
        private Texture2D promptBack;

        /// <summary>Letrero flotante sobre el compañero caido cercano: [E] reanimar, [Clic] arrastrar.</summary>
        private void DrawWorldPrompt()
        {
            if (!avatar.IsAlive || revive.IsRescuing || revive.IsDragging) return;
            var target = revive.NearbyTeammate;
            var cam = Camera.main;
            if (target == null || cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(target.BodyPosition + Vector3.up * 1.1f);
            if (screen.z <= 0f) return;
            if (promptBack == null)
            {
                promptBack = new Texture2D(1, 1);
                promptBack.SetPixel(0, 0, new Color(0f, 0f, 0f, .62f));
                promptBack.Apply();
            }
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, richText = true };
                promptStyle.normal.textColor = Color.white;
                promptStyle.normal.background = promptBack;
                promptStyle.padding = new RectOffset(12, 12, 6, 6);
            }
            string line = "";
            if (target.CanBeRevived) line = "<color=#3fffc0><b>[E]</b></color> Mantén para reanimar";
            bool empty = revive.HandsEmpty;
            string drag = empty ? "<color=#73ccff><b>[Clic izq.]</b></color> Arrastrar"
                : "<color=#888888>Manos vacías para arrastrar</color>";
            line = line.Length > 0 ? line + "\n" + drag : drag;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4f) * .04f;
            var content = new GUIContent(line);
            Vector2 size = promptStyle.CalcSize(content) * pulse;
            var rect = new Rect(screen.x - size.x * .5f, Screen.height - screen.y - size.y, size.x, size.y);
            GUI.Label(rect, content, promptStyle);
        }

        private void OnGUI()
        {
            if (!IsLocal || avatar == null) return;
            DrawWorldPrompt();
            string title, hint;
            float progress;
            var color = new Color(.25f, 1f, .75f);
            var target = revive.RescueTarget;
            if (target != null)
            {
                progress = target.ReviveProgress;
                title = "Reanimando compañero";
                hint = "Mantén E · Suelta para cancelar";
            }
            else if (!avatar.IsAlive && revive.Rescuer != null && revive.ActiveAttempt != 0)
            {
                progress = revive.ReviveProgress;
                title = "Te están reanimando";
                hint = "Tu compañero está ayudándote";
            }
            else if (revive.IsRecovering)
            {
                progress = 1f;
                title = "Reanimado";
                hint = "Levantándote…";
            }
            else if (revive.IsDragging)
            {
                progress = -1f;
                title = "Arrastrando compañero";
                hint = "Suelta clic izquierdo para soltarlo";
                color = new Color(.45f, .8f, 1f);
            }
            else if (!avatar.IsAlive && revive.IsBeingDragged)
            {
                progress = -1f;
                title = "Te están arrastrando";
                hint = "Un compañero te lleva a un lugar seguro";
                color = new Color(.45f, .8f, 1f);
            }
            else if (avatar.IsAlive && Time.unscaledTime < noticeUntil && !string.IsNullOrEmpty(notice))
            {
                progress = completed ? 1f : 0f;
                title = notice;
                hint = completed ? "Tu compañero vuelve a la acción" : "Mantén E para volver a intentarlo";
                if (!completed) color = new Color(1f, .65f, .25f);
            }
            else return;

            GameJam.UI.GameplayFeedbackPanel.Draw(title, hint, color, progress, icon: "revive");
        }

        private void OnDisable()
        {
            observedTarget = null;
            wasRescuing = false;
            noticeUntil = 0f;
        }
    }
}
