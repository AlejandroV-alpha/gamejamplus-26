using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

namespace GameJam.Multiplayer
{
    public sealed class LobbyPlayerCountScreen : MonoBehaviour
    {
        public TextMeshPro label;
        public Sprite astronautIcon;
        public float refreshInterval = .25f;
        private MarsFusionSession session;
        private float nextRefresh;
        private Transform content;
        private TextMeshPro[] names, states;
        private SpriteRenderer[] icons;

        private void Start() => BuildSlots();
        private void BuildSlots()
        {
            if (label == null || content != null) return;
            content = new GameObject("Crew Slots").transform;
            content.SetParent(label.transform, false);
            names = new TextMeshPro[4]; states = new TextMeshPro[4]; icons = new SpriteRenderer[4];
            label.alignment = TextAlignmentOptions.Top;
            label.fontSize = 1.9f;
            label.enableAutoSizing = false;
            label.text = "TRIPULACIÓN";
            for (int i = 0; i < 4; i++)
            {
                float y = .3f - i * .26f;
                var icon = new GameObject("Astronaut " + i).AddComponent<SpriteRenderer>();
                icon.transform.SetParent(content, false);
                icon.transform.localPosition = new Vector3(-1.16f, y, -.008f);
                icon.sprite = astronautIcon;
                if (astronautIcon != null) icon.transform.localScale = Vector3.one * (.29f / astronautIcon.bounds.size.y);
                icons[i] = icon;
                names[i] = CreateText("Name " + i, new Vector3(-.95f, y, -.01f), new Vector2(1.32f,.24f));
                states[i] = CreateText("Ready " + i, new Vector3(.53f, y, -.01f), new Vector2(.8f,.24f));
            }
        }
        private TextMeshPro CreateText(string title, Vector3 position, Vector2 size)
        {
            var text = new GameObject(title).AddComponent<TextMeshPro>();
            text.transform.SetParent(content, false);
            text.transform.localPosition = position;
            text.rectTransform.pivot = new Vector2(0,.5f);
            text.rectTransform.sizeDelta = size;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontSize = 1.65f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 1f; text.fontSizeMax = 1.65f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = false;
            return text;
        }
        private void Update()
        {
            if (label == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            BuildSlots();
            if (session == null) session = FindFirstObjectByType<MarsFusionSession>();
            var runner = session != null ? session.Runner : null;
            var players = runner != null && runner.IsRunning
                ? runner.ActivePlayers.OrderBy(p => p.PlayerId).ToArray() : new PlayerRef[0];
            label.text = "TRIPULACIÓN  ·  " + players.Length + " / 4";
            for (int i = 0; i < 4; i++)
            {
                bool occupied = i < players.Length;
                var player = occupied ? runner.GetPlayerObject(players[i]) : null;
                var avatar = player != null ? player.GetComponent<MarsFusionAvatar>() : null;
                Color color = avatar != null ? MarsPlayerIdentityView.Palette[avatar.SuitColorIndex % MarsPlayerIdentityView.Palette.Length] : new Color(.25f,.36f,.4f);
                icons[i].color = color;
                names[i].text = avatar != null ? avatar.PlayerName.ToString() : occupied ? "CONECTANDO..." : "PLAZA LIBRE";
                names[i].color = occupied ? Color.white : new Color(.4f,.52f,.57f);
                states[i].text = !occupied ? "—" : avatar != null && avatar.LobbyReady ? "LISTO" : "ESPERANDO";
                states[i].color = avatar != null && avatar.LobbyReady ? new Color(.4f,1f,.65f) : new Color(.9f,.67f,.35f);
            }
        }
        private void OnDestroy()
        {
            if (content != null) Destroy(content.gameObject);
        }
    }
}
