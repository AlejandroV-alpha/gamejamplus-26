using GameJam.Localization;
using GameJam.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameJam.UI
{
    /// <summary>Local, non-blocking lobby guide; owned and cleaned up by the player's HUD.</summary>
    public sealed class LobbyGameplayTutorial : MonoBehaviour
    {
        private MarsFusionAvatar avatar;
        private MarsFusionSession session;
        private CanvasGroup group;
        private RectTransform panel;
        private TMP_Text title, body, footer;
        private bool expanded = true;
        private int page;

        public void Build(MarsFusionAvatar source, RectTransform parent, TMP_FontAsset font)
        {
            avatar = source;
            session = FindAnyObjectByType<MarsFusionSession>();
            panel = new GameObject("Lobby gameplay guide", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(parent, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
            panel.anchoredPosition = new Vector2(24, -24);
            panel.sizeDelta = new Vector2(490, 320);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color32(10, 20, 32, 240);
            background.raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(255, 166, 89, 180);
            outline.effectDistance = new Vector2(1, -1);
            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0;
            title = Text("Title", 20, 38, 23, font);
            title.color = new Color32(255, 185, 110, 255);
            body = Text("Instructions", 68, 190, 22, font);
            footer = Text("Navigation", 274, 30, 18, font);
            footer.color = new Color32(150, 190, 210, 255);
            GameLocalization.OnLanguageChanged += Refresh;
            Refresh();
        }

        private TMP_Text Text(string name, float top, float height, int size, TMP_FontAsset font)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(-40, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = size - 4;
            text.fontSizeMax = size;
            text.color = new Color32(235, 244, 250, 255);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.richText = false;
            return text;
        }

        private void LateUpdate()
        {
            if (group == null) return;
            bool visible = SciFiGameMenuManager.IsLobbyScene && avatar != null && avatar.IsNetworkReady
                && avatar.IsAlive && !avatar.TabletOpen && (avatar.LocalUI == null || !avatar.LocalUI.IsVisible)
                && (session == null || !session.MenuOpen)
                && (avatar.LocalHud == null || !avatar.LocalHud.IsInventoryOpen)
                && !LobbyDepartureEffects.IsBlockingInput && !SceneTransitionVideo.IsBlockingInput
                && !PortalWhiteTransition.IsBlockingInput && !GameJam.Crafting.CraftingBox.IsBlockingInput;
            group.alpha = visible ? 1 : 0;
            if (!visible) return;
            // Keep the panel inside the HUD safe area on narrow windows.
            var parent = (RectTransform)panel.parent;
            float scale = Mathf.Min(1f, Mathf.Max(0.1f, (parent.rect.width - 48) / 490f),
                Mathf.Max(0.1f, (parent.rect.height - 48) / 320f));
            panel.localScale = Vector3.one * scale;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) { expanded = !expanded; Refresh(); }
            if (expanded && keyboard.f2Key.wasPressedThisFrame) { page = (page + 1) % 4; Refresh(); }
        }

        private static string Tr(string es, string en, string pt)
        {
            return GameLocalization.CurrentLanguage switch
            {
                GameLanguage.English => en,
                GameLanguage.Portuguese => pt,
                _ => es
            };
        }

        private void Refresh()
        {
            if (panel == null) return;
            panel.sizeDelta = new Vector2(490, expanded ? 320 : 76);
            body.gameObject.SetActive(expanded);
            footer.gameObject.SetActive(expanded);
            if (!expanded)
            {
                title.text = Tr("F1 · Abrir guía de juego", "F1 · Open gameplay guide", "F1 · Abrir guia do jogo");
                return;
            }
            string heading;
            switch (page)
            {
                case 0:
                    heading = Tr("EXPLORA LA NAVE", "EXPLORE THE SHIP", "EXPLORE A NAVE");
                    body.text = Tr(
                        "WASD · Muévete por el lobby.\nRatón · Mira a tu alrededor.\nEspacio · Salta. Shift · Corre.\nV · Habla con tu tripulación si usas pulsar para hablar.\nEsc · Abre ajustes, audio y controles.",
                        "WASD · Move around the lobby.\nMouse · Look around.\nSpace · Jump. Shift · Sprint.\nV · Talk to your crew in push-to-talk mode.\nEsc · Settings, audio and controls.",
                        "WASD · Mova-se pelo lobby.\nMouse · Olhe ao redor.\nEspaço · Pule. Shift · Corra.\nV · Fale com a equipe no modo apertar para falar.\nEsc · Ajustes, áudio e controles.");
                    break;
                case 1:
                    heading = Tr("PREPÁRATE PARA LA MISIÓN", "PREPARE FOR THE MISSION", "PREPARE-SE PARA A MISSÃO");
                    body.text = Tr(
                        "Tab · Consulta la tablet y las recetas.\nI · Abre tu inventario.\nEn la misión, acércate a los recursos y pulsa E para recolectar.\nUsa materiales en las estaciones de fabricación. Vigila salud, energía y ambiente en el HUD.",
                        "Tab · Check the tablet and recipes.\nI · Open your inventory.\nOn the mission, approach resources and press E to collect them.\nUse materials at crafting stations. Watch health, stamina and environment on the HUD.",
                        "Tab · Consulte o tablet e as receitas.\nI · Abra o inventário.\nNa missão, aproxime-se dos recursos e aperte E para coletar.\nUse materiais nas estações de fabricação. Observe saúde, energia e ambiente no HUD.");
                    break;
                case 2:
                    heading = Tr("USO DEL ARMA", "USING YOUR WEAPON", "USO DA ARMA");
                    body.text = Tr(
                        "Clic derecho · Activa o desactiva el modo de apuntado.\nEspera a que aparezca la mira.\nClic izquierdo · Dispara una vez por clic mientras apuntas.\nR · Recarga el arma.\nDurante la recarga no puedes disparar.",
                        "Right click · Toggle aiming mode.\nWait for the crosshair to appear.\nLeft click · Fire once per click while aiming.\nR · Reload your weapon.\nYou cannot fire while reloading.",
                        "Clique direito · Ative ou desative a mira.\nEspere a mira aparecer.\nClique esquerdo · Dispare uma vez por clique enquanto mira.\nR · Recarregue a arma.\nNão é possível disparar durante a recarga.");
                    break;
                default:
                    heading = Tr("SAL CON TU TRIPULACIÓN", "DEPART WITH YOUR CREW", "PARTA COM A EQUIPE");
                    body.text = Tr(
                        "Acércate al monitor izquierdo de la consola y apúntalo.\nE o clic izquierdo · Marca ESTOY LISTO. Repite para cancelar.\nLa salida comienza cuando todos están listos.\nEl monitor derecho sirve para abandonar la sala.",
                        "Approach and aim at the console's left monitor.\nE or left click · Mark yourself READY. Repeat to cancel.\nDeparture begins when everyone is ready.\nUse the right monitor to leave the room.",
                        "Aproxime-se e mire no monitor esquerdo do console.\nE ou clique esquerdo · Marque PRONTO. Repita para cancelar.\nA partida começa quando todos estão prontos.\nO monitor direito serve para sair da sala.");
                    break;
            }
            title.text = $"{page + 1}/4 · {heading}";
            footer.text = Tr("F2 · Siguiente     F1 · Ocultar guía", "F2 · Next     F1 · Hide guide", "F2 · Próximo     F1 · Ocultar guia");
        }

        private void OnDestroy()
        {
            GameLocalization.OnLanguageChanged -= Refresh;
            if (panel != null) Destroy(panel.gameObject);
        }
    }
}
