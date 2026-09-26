using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Gwent.Models;

namespace Gwent.UI
{
    // CardPrefab'ın kök objesine eklenir.
    public class CardView : MonoBehaviour
    {
        [Header("Refs")]
        public Image artworkImage;
        public Image powerBadgeImage; // Faksiyona göre değişecek Güç Rozeti
        public Image abilityIcon;
        public Image rowIcon;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI powerText;
        public GameObject outline;    // Seçili olduğunda açılan Çerçeve Görseli
        public Button infoButton;     // Detay popup'ını açar

        private CardData _data;
        private Vector3 _originalScale;
        // Bu kartın hangi veriye ait olduğunu dışarıdan (UIManager) okuyabilmek için
        public string CardId => _data?.id;

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
                    
                    string badgePath = (data.Type == CardType.Hero) 
                        ? "images/badges/badge_Neutral" 
                        : $"images/badges/badge_{data.faction}";
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

            // --- YETENEK İKONU (ABILITY ICON) YÜKLEME ---
            SetupRowIcon(data);
            SetupAbilityIcon(data);

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

        private void SetupRowIcon(CardData data)
        {
            if (rowIcon == null) return;

            // Liderler, Özel ve Hava kartlarının sırası olmaz
            if (data.Type == CardType.Leader || data.Type == CardType.Special || data.Type == CardType.Weather || data.row == "None" || data.row == "Any")
            {
                rowIcon.gameObject.SetActive(false);
                return;
            }

            string iconName = GetRowIconFileName(data.row);

            if (!string.IsNullOrEmpty(iconName))
            {
                string iconPath = $"images/rows/{iconName}";
                Sprite iconSprite = Core.CardManager.LoadCardSprite(iconPath);

                if (iconSprite != null)
                {
                    rowIcon.sprite = iconSprite;
                    rowIcon.gameObject.SetActive(true);
                    return;
                }
            }

            rowIcon.gameObject.SetActive(false);
        }

        private string GetRowIconFileName(string row)
        {
            if (string.IsNullOrEmpty(row)) return null;

            switch (row)
            {
                case "Melee":
                case "Close Combat": return "row_Melee";
                case "Ranged": return "row_Ranged";
                case "Siege": return "row_Siege";
                case "Agile": return "row_Agile";
                default: return null;
            }
        }

        private void SetupAbilityIcon(CardData data)
        {
            if (abilityIcon == null) return;

            string iconName = GetAbilityIconFileName(data);

            if (!string.IsNullOrEmpty(iconName))
            {
                string iconPath = $"images/abilities/{iconName}";
                Sprite iconSprite = Core.CardManager.LoadCardSprite(iconPath);

                if (iconSprite != null)
                {
                    abilityIcon.sprite = iconSprite;
                    abilityIcon.gameObject.SetActive(true);
                    return;
                }
            }

            // Yeteneği yoksa veya görsel bulunamadıysa ikonu gizle
            abilityIcon.gameObject.SetActive(false);
        }

        private string GetAbilityIconFileName(CardData data)
        {
            if (data == null) return null;

            // Hava kartları yetenek adı "Weather" gelse bile isme/id'ye göre özelleştirilebilir
            if (data.id.Contains("biting_frost") || data.name.Contains("Dondurucu Soğuk")) return "icon_BitingFrost";
            if (data.id.Contains("impenetrable_fog") || data.name.Contains("Yoğun Sis")) return "icon_ImpenetrableFog";
            if (data.id.Contains("torrential_rain") || data.name.Contains("Sağanak Yağmur")) return "icon_TorrentialRain";
            if (data.ability == "WeatherClear" || data.ability == "ClearWeather" || data.name.Contains("Temiz Hava")) return "icon_ClearWeather";

            switch (data.ability)
            {
                case "Medic": return "icon_Medic";
                case "Spy": return "icon_Spy";
                case "TightBond": return "icon_TightBond";
                case "MoraleBoost": 
                case "Morale": return "icon_MoraleBoost";
                case "Muster": return "icon_Muster";
                case "Scorch": 
                case "Scorches": return "icon_Scorch";
                case "CommandersHorn": 
                case "Horn": return "icon_CommandersHorn";
                default: return null;
            }
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