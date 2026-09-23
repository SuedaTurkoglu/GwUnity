using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gwent.Models;

namespace Gwent.UI
{
    // CardPrefab'ın kök objesine eklenir.
    public class CardView : MonoBehaviour
    {
        [Header("Refs")]
        public Image artworkImage;
        public Image powerBadgeImage; // Faksiyona göre değişecek Güç Rozeti
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI powerText;
        public GameObject outline;    // Seçili olduğunda açılan Çerçeve Görseli
        public Button infoButton;     // Detay popup'ını açar

        private CardData _data;

        void Awake()
        {
            if (infoButton != null)
            {
                infoButton.onClick.AddListener(() =>
                {
                    if (_data != null) CardDetailPopup.Instance?.Show(_data);
                });
            }
        }

        public void Setup(CardData data)
        {
            if (data == null) return;
            _data = data;

            if (nameText != null) nameText.text = data.name;

            // --- LİDER KARTLARI KONTROLÜ ---
            // Lider kartlarında güç, rozet ve güçle ilgili hiçbir şey uygulanmaz.
            if (data.Type == CardType.Leader)
            {
                if (powerText != null) powerText.text = "";
                if (powerBadgeImage != null) powerBadgeImage.gameObject.SetActive(false);
            }
            else
            {
                // Normal Birim / Kahraman / Özel Kartlar
                if (powerText != null) powerText.text = data.strength.ToString();

                // Faksiyona Özel Power Badge Yükleme
                if (powerBadgeImage != null)
                {
                    powerBadgeImage.gameObject.SetActive(true);
                    
                    string badgePath = $"images/badges/badge_{data.faction}";
                    Sprite badgeSprite = Core.CardManager.LoadCardSprite(badgePath);

                    if (badgeSprite != null)
                    {
                        powerBadgeImage.sprite = badgeSprite;
                        powerBadgeImage.color = Color.white;
                    }
                    else
                    {
                        // Bulunamadıysa varsayılan Neutral rozetini yükle
                        Sprite defaultBadge = Core.CardManager.LoadCardSprite("images/badges/badge_Neutral");
                        powerBadgeImage.sprite = defaultBadge;
                    }
                }
            }

            // --- KARAKTER GÖRSELİ (ARTWORK) YÜKLEME ---
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

            // Kart ilk oluşturulduğunda outline her zaman kapalı olmalı
            SetSelected(false);
        }

        public void SetSelected(bool isSelected)
        {
            if (outline != null)
            {
                outline.SetActive(isSelected);
            }
        }
    }
}