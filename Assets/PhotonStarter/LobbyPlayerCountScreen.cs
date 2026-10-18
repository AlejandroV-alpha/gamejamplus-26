using System.Linq;
using TMPro;
using UnityEngine;

namespace GameJam.Multiplayer
{
    /// <summary>Shows how many players are in the Fusion session on a world-space screen.</summary>
    public sealed class LobbyPlayerCountScreen : MonoBehaviour
    {
        public TextMeshPro label;
        public float refreshInterval = 0.25f;
        private MarsFusionSession session;
        private float nextRefresh;
        private string lastText;

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            if (session == null) session = FindFirstObjectByType<MarsFusionSession>();

            string text;
            var runner = session != null ? session.Runner : null;
            if (runner == null || !runner.IsRunning)
                text = "<size=60%>SALA DE ESPERA</size>\n<color=#FFB347>ESPERANDO CONEXIÓN</color>";
            else
            {
                int count = runner.ActivePlayers.Count();
                text = $"<size=60%>{session.roomName.ToUpperInvariant()}</size>\nJUGADORES\n<size=160%><color=#7CFF8A>{count}</color> / {session.maxPlayers}</size>";
            }
            if (text == lastText) return;
            lastText = text;
            label.text = text;
        }
    }
}
