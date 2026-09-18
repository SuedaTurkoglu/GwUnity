using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gwent.Models;

namespace Gwent.UI
{
    // CardPrefab'ın kök objesine eklenir. Kart verisini alıp görseli,
    // ismi ve güç rozetini kendi başına doldurur — UIManager'ın
    // "hangi child'ta ne var" bilmesine gerek kalmaz.
    public class CardView : MonoBehaviour
    {
        [Header("Refs")]
        public Image artworkImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI powerText;
        public Button infoButton; // YENİ: detay popup'ını açar

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
            if (powerText != null) powerText.text = data.strength.ToString();

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
        }
    }
}