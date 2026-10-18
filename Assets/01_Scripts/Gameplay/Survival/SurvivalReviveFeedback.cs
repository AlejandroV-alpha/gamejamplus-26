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
        private GUIStyle titleStyle, hintStyle;

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

        private void OnGUI()
        {
            if (!IsLocal || avatar == null) return;
            string title, hint;
            float progress;
            var color = new Color(.25f, 1f, .75f);
            var target = revive.RescueTarget;
            if (target != null)
            {
                progress = target.ReviveProgress;
                title = "Reanimando compañero… " + Mathf.FloorToInt(progress * 100f) + "%";
                hint = "Mantén F · Suelta para cancelar";
            }
            else if (!avatar.IsAlive && revive.Rescuer != null && revive.ActiveAttempt != 0)
            {
                progress = revive.ReviveProgress;
                title = "Te están reanimando… " + Mathf.FloorToInt(progress * 100f) + "%";
                hint = "Tu compañero está ayudándote";
            }
            else if (revive.IsRecovering)
            {
                progress = 1f;
                title = "Reanimado";
                hint = "Levantándote…";
            }
            else if (avatar.IsAlive && Time.unscaledTime < noticeUntil && !string.IsNullOrEmpty(notice))
            {
                progress = completed ? 1f : 0f;
                title = notice;
                hint = completed ? "Tu compañero vuelve a la acción" : "Mantén F para volver a intentarlo";
                if (!completed) color = new Color(1f, .65f, .25f);
            }
            else return;

            DrawFeedback(title, hint, progress, color, new Rect(0f, 0f, Screen.width, Screen.height));
        }

        private void DrawFeedback(string title, string hint, float progress, Color color, Rect viewport)
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            }
            float width = Mathf.Min(380f, viewport.width * .8f);
            float x = viewport.x + (viewport.width - width) * .5f, y = viewport.y + viewport.height * .76f;
            GUI.Box(new Rect(x, y, width, 72f), GUIContent.none);
            GUI.Label(new Rect(x + 8f, y + 5f, width - 16f, 24f), title, titleStyle);
            var previous = GUI.color;
            GUI.color = new Color(.15f, .2f, .2f);
            GUI.DrawTexture(new Rect(x + 8f, y + 34f, width - 16f, 5f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x + 8f, y + 34f, (width - 16f) * Mathf.Clamp01(progress), 5f), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(x + 8f, y + 44f, width - 16f, 22f), hint, hintStyle);
        }

        private void OnDisable()
        {
            observedTarget = null;
            wasRescuing = false;
            noticeUntil = 0f;
        }
    }
}
