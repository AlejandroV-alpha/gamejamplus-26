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

        // ───────────── Marcadores de compañeros caídos ─────────────

        private SurvivalRevive[] allRevives = new SurvivalRevive[0];
        private float nextReviveScan;
        private GUIStyle markerStyle, markerNameStyle;

        /// <summary>
        /// Marca en pantalla a cada compañero caído con su distancia. Si está fuera de la vista,
        /// el marcador se pega al borde con una flecha que apunta hacia él.
        /// </summary>
        private void DrawDownedMarkers()
        {
            if (!avatar.IsAlive) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (Time.unscaledTime >= nextReviveScan)
            {
                nextReviveScan = Time.unscaledTime + .4f;
                allRevives = FindObjectsByType<SurvivalRevive>(FindObjectsSortMode.None);
            }
            float s = GameJam.UI.InteractionPromptCard.Scale;
            if (markerStyle == null)
            {
                markerStyle = new GUIStyle(GameJam.UI.UiFont.Label()) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, padding = new RectOffset() };
                markerNameStyle = new GUIStyle(markerStyle) { fontStyle = FontStyle.Normal };
                markerStyle.font = GameJam.UI.UiFont.TitleFont;
            }
            markerStyle.fontSize = GameJam.UI.UiFont.Px(13 * s);
            markerNameStyle.fontSize = GameJam.UI.UiFont.Px(11 * s);
            var accent = new Color(.25f, 1f, .75f);
            var icon = GameJam.UI.ExpeditionSprites.Get("revive");
            Vector3 me = revive.BodyPosition;
            foreach (var other in allRevives)
            {
                if (other == null || other == revive || other.Object == null || !other.Object.IsValid || !other.CanBeRevived) continue;
                Vector3 world = other.BodyPosition + Vector3.up * .9f;
                float distance = Vector3.Distance(me, other.BodyPosition);
                if (distance < 3.2f) continue; // de cerca ya se ve la tarjeta de reanimar

                Vector3 sp = cam.WorldToScreenPoint(world);
                bool behind = sp.z < 0f;
                if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }
                Vector2 gui = new Vector2(sp.x, Screen.height - sp.y);
                float margin = 56 * s;
                bool off = behind || gui.x < margin || gui.x > Screen.width - margin || gui.y < margin || gui.y > Screen.height - margin;
                Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f);
                Vector2 dir = (gui - center);
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                if (off)
                {
                    // Proyecta sobre el rectangulo interior de la pantalla.
                    float hw = Screen.width * .5f - margin, hh = Screen.height * .5f - margin;
                    float k = Mathf.Min(hw / Mathf.Max(.001f, Mathf.Abs(dir.x)), hh / Mathf.Max(.001f, Mathf.Abs(dir.y)));
                    gui = center + dir * k;
                }

                float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 4f);
                float size = 34 * s;
                var circle = new Rect(gui.x - size * .5f, gui.y - size * .5f, size, size);
                // Halo que late para llamar la atención.
                float halo = size * (1.25f + .35f * pulse);
                GameJam.UI.InteractionPromptCard.Fill(new Rect(gui.x - halo * .5f, gui.y - halo * .5f, halo, halo), GameJam.UI.InteractionPromptCard.Fade(accent, .25f * (1f - pulse)), 1f, halo * .5f);
                GameJam.UI.InteractionPromptCard.Fill(circle, GameJam.UI.InteractionPromptCard.Panel, 1f, size * .5f);
                GameJam.UI.InteractionPromptCard.Fill(new Rect(circle.x + 2 * s, circle.y + 2 * s, size - 4 * s, size - 4 * s), GameJam.UI.InteractionPromptCard.Fade(accent, .18f), 1f, size * .5f);
                if (icon != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(circle.x + 6 * s, circle.y + 6 * s, size - 12 * s, size - 12 * s), icon.texture, ScaleMode.ScaleToFit, true);
                }
                if (off)
                {
                    // Flecha hacia el compañero.
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    Matrix4x4 m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(angle, gui);
                    var tip = new Rect(gui.x + size * .5f + 3 * s, gui.y - 5 * s, 10 * s, 10 * s);
                    GUIUtility.RotateAroundPivot(45f, tip.center);
                    GameJam.UI.InteractionPromptCard.Fill(tip, accent, 1f, 2 * s);
                    GUI.matrix = m;
                }

                // Distancia y nombre debajo (o encima si está pegado abajo).
                var teammate = other.GetComponent<MarsFusionAvatar>();
                string name = teammate != null ? teammate.PlayerName.ToString() : "";
                string dist = Mathf.RoundToInt(distance) + " m";
                bool above = gui.y > Screen.height - 110 * s;
                float ty = above ? circle.y - 34 * s : circle.yMax + 4 * s;
                float tw = Mathf.Max(markerStyle.CalcSize(new GUIContent(dist)).x, markerNameStyle.CalcSize(new GUIContent(name)).x) + 16 * s;
                var label = new Rect(gui.x - tw * .5f, ty, tw, 30 * s);
                GameJam.UI.InteractionPromptCard.Fill(label, GameJam.UI.InteractionPromptCard.Fade(GameJam.UI.InteractionPromptCard.Panel, .85f), 1f, 6 * s);
                markerStyle.normal.textColor = accent;
                GUI.Label(new Rect(label.x, label.y + 1 * s, label.width, 15 * s), dist, markerStyle);
                markerNameStyle.normal.textColor = GameJam.UI.InteractionPromptCard.TextSoft;
                GUI.Label(new Rect(label.x, label.y + 15 * s, label.width, 13 * s), name, markerNameStyle);
            }
        }

        private void OnGUI()
        {
            if (!IsLocal || avatar == null) return;
            DrawDownedMarkers();
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
            else if (avatar.IsAlive && avatar.IsRagdoll)
            {
                // Derribado por un golpe: se puede levantar a mano o espera a que se acabe el tiempo.
                progress = -1f;
                title = "¡Derribado!";
                hint = avatar.CanGetUpManually
                    ? "Pulsa [Espacio] para levantarte"
                    : "Recuperándote…";
                color = new Color(1f, .68f, .25f);
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
