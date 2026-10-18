using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam.Localization
{
    /// <summary>
    /// Componente que actualiza automáticamente un TextMeshProUGUI o Text cuando cambia el idioma.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string translationKey;
        [SerializeField] private string fallbackText;

        private TMP_Text tmpText;
        private Text legacyText;

        public string Key
        {
            get => translationKey;
            set
            {
                translationKey = value;
                Refresh();
            }
        }

        public void SetKey(string key)
        {
            Key = key;
        }

        private HeyHeyThere.PixelUISciFiFree.PixelButton pixelButton;

        private void Awake()
        {
            tmpText = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>(true);
            legacyText = GetComponent<Text>() ?? GetComponentInChildren<Text>(true);
            pixelButton = GetComponent<HeyHeyThere.PixelUISciFiFree.PixelButton>();
        }

        private void OnEnable()
        {
            GameLocalization.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameLocalization.OnLanguageChanged -= Refresh;
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(translationKey)) return;
            if (tmpText == null) tmpText = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>(true);
            if (legacyText == null) legacyText = GetComponent<Text>() ?? GetComponentInChildren<Text>(true);
            if (pixelButton == null) pixelButton = GetComponent<HeyHeyThere.PixelUISciFiFree.PixelButton>();

            string translated = GameLocalization.Get(translationKey, fallbackText);
            if (pixelButton != null)
            {
                pixelButton.text = translated;
            }
            else
            {
                if (tmpText != null) tmpText.text = translated;
                if (legacyText != null) legacyText.text = translated;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Refresh();
        }
#endif
    }
}
