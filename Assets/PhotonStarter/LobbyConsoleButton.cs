using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameJam.Multiplayer
{
    /// <summary>Physical lobby console button: the local player walks up to it and presses E.</summary>
    public sealed class LobbyConsoleButton : MonoBehaviour
    {
        public enum ButtonAction { StartGame, LeaveLobby }

        public ButtonAction action;
        public string title = "COMENZAR PARTIDA";
        public string missionScenePath = "Assets/00_Scenes/j_MarsFusion.unity";
        public Transform cap;
        public TextMeshPro label;
        public float interactRange = 2.2f;

        private MarsFusionSession session;
        private Vector3 capRest;
        private float pressedUntil;
        private float messageUntil;
        private string message;

        private static readonly System.Collections.Generic.List<LobbyConsoleButton> All = new();

        private void Awake()
        {
            if (cap != null) capRest = cap.localPosition;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Update()
        {
            if (session == null) session = FindFirstObjectByType<MarsFusionSession>();
            var runner = session != null ? session.Runner : null;
            bool near = runner != null && runner.IsRunning && !session.MenuOpen && IsLocalPlayerNear(runner);

            if (near && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Press(runner);

            if (cap != null)
                cap.localPosition = Time.time < pressedUntil ? capRest - Vector3.up * 0.03f : capRest;
            if (label != null)
            {
                string text = Time.time < messageUntil ? message : near ? "<color=#FFFFFF>[E]</color> " + title : title;
                if (label.text != text) label.text = text;
                label.alpha = near || Time.time < messageUntil ? 1f : 0.55f;
            }
        }

        private bool IsLocalPlayerNear(NetworkRunner runner)
        {
            var player = runner.GetPlayerObject(runner.LocalPlayer);
            if (player == null) return false;
            var p = player.transform.position;
            float mine = (p - transform.position).sqrMagnitude;
            if (mine >= interactRange * interactRange) return false;
            // Buttons sit close together: only the nearest one reacts, so E never presses both.
            foreach (var other in All)
                if (other != this && (p - other.transform.position).sqrMagnitude < mine) return false;
            return true;
        }

        private void Press(NetworkRunner runner)
        {
            pressedUntil = Time.time + 0.2f;
            if (action == ButtonAction.LeaveLobby)
            {
                Show("SALIENDO...");
                _ = session.Disconnect();
                return;
            }
            if (!runner.IsSceneAuthority) { Show("SOLO EL ANFITRIÓN"); return; }
            int index = SceneUtility.GetBuildIndexByScenePath(missionScenePath);
            if (index < 0) { Show("ESCENA NO EN BUILD"); Debug.LogError($"{missionScenePath} is not in Build Settings.", this); return; }
            Show("DESPEGANDO...");
            var alert = FindFirstObjectByType<LobbyAlert>();
            if (alert != null && alert.Object != null && alert.Object.IsValid) alert.Begin(index);
            else runner.LoadScene(SceneRef.FromIndex(index));
        }

        private void Show(string text)
        {
            message = text;
            messageUntil = Time.time + 2f;
        }
    }
}
