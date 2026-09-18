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

        public void Setup(CardData data)
        {
            if (data == null) return;

            if (nameText != null) nameText.text = data.name;
            if (powerText != null) powerText.text = data.strength.ToString();

            if (artworkImage != null)
            {
                Sprite sprite = LoadSprite(data.imagePath);
                if (sprite != null)
                {
                    artworkImage.sprite = sprite;
                    artworkImage.color = Color.white;
                }
                else
                {
                    // Görsel bulunamazsa kartı tamamen boş bırakmak yerine
                    // düz bir renk göster ki en azından isim/güç okunabilsin.
                    artworkImage.sprite = null;
                    artworkImage.color = new Color(0.16f, 0.14f, 0.12f, 1f);
                }
            }
        }

        private Sprite LoadSprite(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return null;

            // Resources.Load uzantı istemez, cards.json'da ".png" ile bıraksan da sorun değil.
            string cleanPath = imagePath;
            int dot = cleanPath.LastIndexOf('.');
            if (dot >= 0) cleanPath = cleanPath.Substring(0, dot);

            Sprite sprite = Resources.Load<Sprite>(cleanPath);
            if (sprite == null)
            {
                Debug.LogWarning($"Kart görseli bulunamadı: Resources/{cleanPath} (orijinal yol: {imagePath}). " +
                                  "Dosyanın Resources klasöründe olduğundan ve Texture Type'ının 'Sprite (2D and UI)' olduğundan emin ol.");
            }
            return sprite;
        }
    }
}