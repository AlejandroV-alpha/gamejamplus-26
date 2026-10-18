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
        public string missionScenePath = "Assets/00_Scenes/Mars.unity";
        public Transform cap;
        public TextMeshPro label;
        public float interactRange = 2.2f;
        [Tooltip("Optional physical touchscreen. Aim at it and press E or left click.")]
        public Collider touchSurface;

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
            var localPlayer = runner != null && runner.IsRunning ? runner.GetPlayerObject(runner.LocalPlayer) : null;
            var avatar = localPlayer != null ? localPlayer.GetComponent<MarsFusionAvatar>() : null;
            var departure = FindFirstObjectByType<LobbyAlert>();
            bool near = runner != null && runner.IsRunning && !session.MenuOpen
                && (avatar == null || (avatar.IsAlive && !avatar.TabletOpen && (avatar.LocalUI == null || !avatar.LocalUI.IsVisible)))
                && (touchSurface != null ? IsTouchTarget(localPlayer) : IsLocalPlayerNear(runner));

            if (near && (departure == null || !departure.DepartureActive) && ((Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                || (touchSurface != null && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))) Press(runner);

            if (cap != null)
                cap.localPosition = Time.time < pressedUntil ? capRest - Vector3.up * 0.03f : capRest;
            if (label != null)
            {
                string actionTitle = title;
                if (action == ButtonAction.StartGame && avatar != null)
                    actionTitle = departure != null && departure.DepartureActive ? "SECUENCIA\nACTIVA"
                        : avatar.LobbyReady ? "CANCELAR\nLISTO" : "ESTOY\nLISTO";
                string text = Time.time < messageUntil ? message : near ? "<color=#FFFFFF>[E]</color> " + actionTitle : actionTitle;
                if (label.text != text) label.text = text;
                label.alpha = near || Time.time < messageUntil ? 1f : 0.8f;
            }
        }

        private bool IsTouchTarget(NetworkObject player)
        {
            var camera = Camera.main;
            if (player == null || camera == null
                || (touchSurface.ClosestPoint(player.transform.position) - player.transform.position).sqrMagnitude > interactRange * interactRange) return false;
            Vector2 point = Cursor.lockState == CursorLockMode.Locked || Mouse.current == null
                ? new Vector2(Screen.width * .5f, Screen.height * .5f) : Mouse.current.position.ReadValue();
            var hits = Physics.RaycastAll(camera.ScreenPointToRay(point), 12f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(player.transform)) continue;
                return hit.collider == touchSurface;
            }
            return false;
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
            var player = runner.GetPlayerObject(runner.LocalPlayer);
            var avatar = player != null ? player.GetComponent<MarsFusionAvatar>() : null;
            if (avatar == null) { Show("ESPERANDO JUGADOR"); return; }
            if (!avatar.LobbyReady)
            {
                avatar.SetLobbyReady(true);
                Show("LISTO");
                return;
            }
            avatar.SetLobbyReady(false);
            Show("LISTO CANCELADO");
        }

        private void Show(string text)
        {
            message = text;
            messageUntil = Time.time + 2f;
        }

        public static bool AllPlayersReady(NetworkRunner runner)
        {
            if (runner == null || !runner.IsRunning) return false;
            bool any = false;
            foreach (var id in runner.ActivePlayers)
            {
                any = true;
                var player = runner.GetPlayerObject(id);
                var avatar = player != null ? player.GetComponent<MarsFusionAvatar>() : null;
                if (avatar == null || !avatar.LobbyReady) return false;
            }
            return any;
        }
    }
}
