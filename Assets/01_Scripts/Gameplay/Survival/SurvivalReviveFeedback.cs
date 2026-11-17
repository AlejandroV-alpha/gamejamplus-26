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

        private float promptAlpha;
        private SurvivalRevive promptTarget;
        private readonly GameJam.UI.InteractionPromptCard.Row[] reviveRows = new GameJam.UI.InteractionPromptCard.Row[2];
        private readonly GameJam.UI.InteractionPromptCard.Row[] dragRows = new GameJam.UI.InteractionPromptCard.Row[1];

        /// <summary>Tarjeta flotante sobre el compañero caido cercano: [E] reanimar, [Clic izq.] arrastrar.</summary>
        private void DrawWorldPrompt()
        {
            bool show = avatar.IsAlive && !revive.IsRescuing && !revive.IsDragging;
            var target = show ? revive.NearbyTeammate : null;
            if (target != null) promptTarget = target;
            if (Event.current.type == EventType.Repaint)
                promptAlpha = Mathf.MoveTowards(promptAlpha, target != null ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            var cam = Camera.main;
            if (promptAlpha <= .01f || promptTarget == null || promptTarget.Object == null || !promptTarget.Object.IsValid || cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(promptTarget.BodyPosition + Vector3.up * 1.2f);
            var teammate = promptTarget.GetComponent<MarsFusionAvatar>();
            string name = teammate != null ? teammate.PlayerName.ToString() : "";
            bool empty = revive.HandsEmpty;
            var drag = new GameJam.UI.InteractionPromptCard.Row
            {
                Key = GameJam.UI.InteractionPromptCard.MouseLeft, Tag = empty ? "MANTENER" : null,
                Action = empty ? "Arrastrar" : "Guarda tu objeto para arrastrar",
                Enabled = empty
            };
            if (promptTarget.CanBeRevived)
            {
                reviveRows[0] = new GameJam.UI.InteractionPromptCard.Row { Key = "E", Action = "Reanimar", Tag = "MANTENER", Enabled = true };
                reviveRows[1] = drag;
                GameJam.UI.InteractionPromptCard.Draw(screen, "COMPAÑERO CAÍDO", name, new Color(.25f, 1f, .75f), reviveRows, promptAlpha, "revive");
            }
            else
            {
                dragRows[0] = drag;
                GameJam.UI.InteractionPromptCard.Draw(screen, "COMPAÑERO NOQUEADO", name, new Color(.45f, .8f, 1f), dragRows, promptAlpha, "crew");
            }
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
                hint = "Mantén [E]  ·  suelta para cancelar";
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
                hint = "Suelta [@LMB] para soltarlo";
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
                hint = completed ? "Tu compañero vuelve a la acción" : "Mantén [E] para volver a intentarlo";
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
