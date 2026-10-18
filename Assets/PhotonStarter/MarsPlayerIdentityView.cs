using TMPro;
using UnityEngine;

namespace GameJam.Multiplayer
{
    // Presentation only; identity is replicated by MarsFusionAvatar.
    public sealed class MarsPlayerIdentityView : MonoBehaviour
    {
        public static readonly Color[] Palette =
        {
            new Color(1f, .22f, .25f), new Color(.15f, .65f, 1f),
            new Color(.25f, 1f, .4f), new Color(1f, .8f, .12f),
            new Color(.85f, .25f, 1f), new Color(.1f, 1f, .9f),
            new Color(1f, .4f, .08f), new Color(1f, .3f, .65f)
        };

        private MarsFusionAvatar avatar;
        private SkinnedMeshRenderer[] suitMeshes;
        private MaterialPropertyBlock properties;
        private TextMeshPro label;
        private Camera viewCamera;
        private int appliedColor = -1;
        private string appliedName;
        private float labelHeight;
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
        }

        private void LateUpdate()
        {
            if (avatar == null || avatar.Object == null || !avatar.Object.IsValid || label == null) return;
            int index = avatar.SuitColorIndex % Palette.Length;
            if (index != appliedColor)
            {
                appliedColor = index;
                foreach (var mesh in suitMeshes)
                {
                    mesh.GetPropertyBlock(properties);
                    properties.SetColor(BaseColor, Palette[index]);
                    mesh.SetPropertyBlock(properties);
                }
            }
            string playerName = avatar.PlayerName.ToString();
            if (playerName != appliedName) { appliedName = playerName; label.text = playerName; }
            if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
            bool visible = viewCamera != null && !avatar.HasStateAuthority && !string.IsNullOrEmpty(playerName)
                && (viewCamera.transform.position - transform.position).sqrMagnitude < 1600f;
            label.gameObject.SetActive(visible);
            if (!visible) return;
            label.transform.position = transform.position + Vector3.up * labelHeight;
            label.transform.rotation = viewCamera.transform.rotation;
        }

        private void OnDestroy()
        {
            if (label != null) Destroy(label.gameObject);
        }
    }
}
