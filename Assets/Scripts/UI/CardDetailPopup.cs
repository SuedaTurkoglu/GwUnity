using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gwent.Models;

namespace Gwent.UI
{
    public class CardDetailPopup : MonoBehaviour
    {
        public static CardDetailPopup Instance { get; private set; }

        [Header("Refs")]
        public GameObject root;             // CardDetailOverlay'in kendisi (aç/kapat için)
        public Image artworkImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI statsText;       // "Güç: 15   Kuzey Krallıkları   [Hero]" gibi
        public TextMeshProUGUI descriptionText;
        public Button closeButton;              // panel üstündeki X butonu
        public Button backgroundCloseButton;    // karanlık arka plana tıklayınca da kapansın

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backgroundCloseButton != null) backgroundCloseButton.onClick.AddListener(Hide);

            if (root != null) root.SetActive(false);
        }

        public void Show(CardData data)
        {
            if (data == null || root == null) return;

            if (nameText != null) nameText.text = data.name;
            if (descriptionText != null) descriptionText.text = data.description ?? string.Empty;

            if (statsText != null)
            {
                string faction = FactionLabel(data.faction);
                string ability = string.IsNullOrEmpty(data.ability) || data.ability == "None"
                    ? string.Empty
                    : $"   [{data.ability}]";
                statsText.text = $"Güç: {data.strength}   {faction}{ability}";
            }

            if (artworkImage != null)
            {
                Sprite sprite = Core.CardManager.LoadCardSprite(data.imagePath);
                if (sprite != null)
                {
                    artworkImage.sprite = sprite;
                    artworkImage.color = Color.white;
                }
                else
                {
                    artworkImage.sprite = null;
                    artworkImage.color = new Color(0.16f, 0.14f, 0.12f, 1f);
                }
            }

            root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private string FactionLabel(string code)
        {
            switch (code)
            {
                case "Northern": return "Kuzey Krallıkları";
                case "Nilfgaard": return "Nilfgaard İmparatorluğu";
                case "ScoiaTael": return "Scoia'tael";
                case "Monsters": return "Canavarlar";
                case "Skellige": return "Skellige";
                case "Neutral": return "Tarafsız";
                default: return code;
            }
        }
    }
}