using TMPro;
using UnityEngine;

namespace GameJam.Multiplayer
{
    // Presentation only; identity is replicated by MarsFusionAvatar.
    public sealed class MarsPlayerIdentityView : MonoBehaviour
    {
        public static readonly Color[] Palette =
        {
            new Color32(255, 56, 64, 255),   // Rojo coral
            new Color32(45, 140, 255, 255),  // Azul
            new Color32(57, 230, 117, 255),  // Verde
            new Color32(255, 212, 59, 255),  // Amarillo
            new Color32(179, 107, 255, 255), // Violeta
            new Color32(39, 217, 207, 255)   // Turquesa
        };
        public static readonly Color PendingColor = new Color32(115, 128, 140, 255);

        private MarsFusionAvatar avatar;
        private SkinnedMeshRenderer[] suitMeshes;
        private MaterialPropertyBlock properties;
        private TextMeshPro label;
        private TextMeshPro calloutLabel;
        private Camera viewCamera;
        private int appliedColor = -1;
        private string appliedName;
        private float labelHeight;
        private float calloutUntil;
        private Color calloutBaseColor = Color.white;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void Initialize(MarsFusionAvatar source)
        {
            avatar = source;
            suitMeshes = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            properties = new MaterialPropertyBlock();
            float top = transform.position.y + 2.7f;
            foreach (var mesh in suitMeshes) top = Mathf.Max(top, mesh.bounds.max.y);
            labelHeight = top - transform.position.y + .35f;

            label = new GameObject("Player Nameplate").AddComponent<TextMeshPro>();
            label.transform.SetParent(transform, false);
            label.rectTransform.sizeDelta = new Vector2(4f, .55f);
            label.fontSize = 3f;
            label.alignment = TextAlignmentOptions.Center;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = Color.white;
            label.outlineWidth = .2f;
            label.outlineColor = new Color32(15, 18, 27, 255);
            label.gameObject.SetActive(false);

            calloutLabel = new GameObject("Player Troll Banner").AddComponent<TextMeshPro>();
            calloutLabel.transform.SetParent(transform, false);
            calloutLabel.rectTransform.sizeDelta = new Vector2(6.5f, .75f);
            calloutLabel.fontSize = 3.4f;
            calloutLabel.fontStyle = FontStyles.Bold;
            calloutLabel.alignment = TextAlignmentOptions.Center;
            calloutLabel.richText = false;
            calloutLabel.textWrappingMode = TextWrappingModes.NoWrap;
            calloutLabel.color = new Color32(81, 229, 45, 255);
            calloutLabel.outlineWidth = .26f;
            calloutLabel.outlineColor = new Color32(10, 14, 22, 255);
            calloutLabel.gameObject.SetActive(false);
        }

        public void ShowCalloutBanner(string message, Color color, float duration = 3.2f)
        {
            if (calloutLabel == null) return;
            calloutLabel.text = message;
            calloutBaseColor = color;
            calloutLabel.color = color;
            calloutUntil = Time.unscaledTime + Mathf.Max(.2f, duration);
            calloutLabel.gameObject.SetActive(true);
        }

        public void ClearCalloutBanner()
        {
            calloutUntil = 0f;
            if (calloutLabel != null) calloutLabel.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (avatar == null || !avatar.IsNetworkReady || label == null) return;
            int index = avatar.HasAssignedColor ? avatar.SuitColorIndex : -2;
            if (index != appliedColor)
            {
                appliedColor = index;
                foreach (var mesh in suitMeshes)
                {
                    mesh.GetPropertyBlock(properties);
                    properties.SetColor(BaseColor, index >= 0 ? Palette[index] : PendingColor);
                    mesh.SetPropertyBlock(properties);
                }
            }
            string playerName = avatar.PlayerName.ToString();
            if (playerName != appliedName) { appliedName = playerName; label.text = playerName; }
            if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
            float distSq = viewCamera != null ? (viewCamera.transform.position - transform.position).sqrMagnitude : float.MaxValue;
            bool visible = viewCamera != null && !avatar.HasStateAuthority && !string.IsNullOrEmpty(playerName)
                && distSq < 1600f;
            label.gameObject.SetActive(visible);
            if (visible)
            {
                label.transform.position = transform.position + Vector3.up * labelHeight;
                label.transform.rotation = viewCamera.transform.rotation;
            }

            if (calloutLabel != null)
            {
                bool showCallout = viewCamera != null && Time.unscaledTime < calloutUntil && distSq < 2200f;
                if (calloutLabel.gameObject.activeSelf != showCallout)
                    calloutLabel.gameObject.SetActive(showCallout);
                if (showCallout)
                {
                    float t = Time.unscaledTime;
                    float bob = Mathf.Sin(t * 7f) * .08f;
                    float pulse = 1f + Mathf.Sin(t * 14f) * .07f;
                    calloutLabel.transform.position = transform.position + Vector3.up * (labelHeight + .48f + bob);
                    calloutLabel.transform.rotation = viewCamera.transform.rotation;
                    calloutLabel.transform.localScale = Vector3.one * pulse;
                    calloutLabel.color = Color.Lerp(calloutBaseColor, Color.white, .25f + .25f * Mathf.Sin(t * 16f));
                }
            }
        }

        private void OnDestroy()
        {
            if (label != null) Destroy(label.gameObject);
            if (calloutLabel != null) Destroy(calloutLabel.gameObject);
        }
    }
}
