using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Gwent.Models;
using Gwent.Networking;

namespace Gwent.UI
{
    public class DeckBuilderUI : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject rootPanel;
        public Transform availableCardsContainer; // Tüm kartların listelendiği alan
        public Transform selectedCardsContainer;   // Desteye eklenen kartların alanı
        public Transform leaderSelectionContainer; // Lider seçim alanı

        [Header("UI Elements")]
        public TextMeshProUGUI countText; // "Deste: 12 / 22"
        public Button saveButton;
        public TMP_Dropdown factionFilter; // Faksiyon filtreleme

        [Header("Prefabs")]
        public GameObject cardPrefab;

        private List<string> currentDeck = new List<string>();
        private string selectedLeaderId = "";
        private string currentFaction = "All";

        void Start()
        {
            if (rootPanel != null) rootPanel.SetActive(false);
            if (saveButton != null) saveButton.onClick.AddListener(HandleSaveDeck);
            if (factionFilter != null) factionFilter.onValueChanged.AddListener(OnFactionChanged);
        }

        public void OpenDeckBuilder()
        {
            rootPanel.SetActive(true);
            RefreshAvailableCards();
            RefreshSelectedCards();
        }

        public void CloseDeckBuilder()
        {
            rootPanel.SetActive(false);
        }

        private void OnFactionChanged(int index)
        {
            string factionName = factionFilter.options[index].text;

            switch (factionName)
            {
                case "Hepsi": currentFaction = "All"; break;
                case "Kuzey Krallıkları": currentFaction = "Northern"; break;
                case "Nilfgaard İmaparatorluğu": currentFaction = "Nilfgaard"; break;
                case "Scoia'tael": currentFaction = "ScoiaTael"; break;
                case "Canavarlar": currentFaction = "Monsters"; break;
                case "Skellige": currentFaction = "Skellige"; break;
                default: currentFaction = "All"; break;
            }
            RefreshAvailableCards();
        }

        public void RefreshAvailableCards()
        {
            foreach (Transform child in availableCardsContainer) Destroy(child.gameObject);

            var allCards = Core.CardManager.Instance.GetAllCards();
            var filtered = allCards.Where(c => currentFaction == "All" || c.faction == currentFaction).ToList();

            foreach (var card in filtered)
            {
                GameObject obj = Instantiate(cardPrefab, availableCardsContainer);
                var view = obj.GetComponent<CardView>();
                view.Setup(card);

                Button btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    string id = card.id;
                    btn.onClick.AddListener(() => ToggleCardInDeck(id));
                }
            }
        }

        private void ToggleCardInDeck(string cardId)
        {
            var card = Core.CardManager.Instance.GetCardById(cardId);
            if (card == null) return;

            if (currentDeck.Contains(cardId))
            {
                currentDeck.Remove(cardId);
            }
            else
            {
                // --- GWENT KURALLARI KONTROLÜ ---

                // 1. Tek Faksiyon Kuralı (Neutral kartlar her desteye girebilir)
                if (currentDeck.Count > 0)
                {
                    var firstCard = Core.CardManager.Instance.GetCardById(currentDeck[0]);
                    // Eğer destedeki ilk kart Neutral ise, gerçek faksiyonu bulana kadar ara
                    string deckFaction = "Neutral";
                    foreach(var id in currentDeck) {
                        var c = Core.CardManager.Instance.GetCardById(id);
                        if(c != null && c.faction != "Neutral") {
                            deckFaction = c.faction;
                            break;
                        }
                    }

                    if (deckFaction != "Neutral" && card.faction != "Neutral" && card.faction != deckFaction)
                    {
                        Gwent.UI.UIManager.Instance.ShowFeedback("Sadece tek bir faksiyon seçebilirsiniz!");
                        return;
                    }
                }

                // 2. Özel Kart Limiti (Max 10)
                if (card.cardType == CardType.Special)
                {
                    int specialCount = currentDeck.Count(id => {
                        var c = Core.CardManager.Instance.GetCardById(id);
                        return c != null && c.cardType == CardType.Special;
                    });

                    if (specialCount >= 10)
                    {
                        Gwent.UI.UIManager.Instance.ShowFeedback("En fazla 10 Özel kart ekleyebilirsiniz!");
                        return;
                    }
                }

                currentDeck.Add(cardId);
            }

            RefreshSelectedCards();
        }

        public void RefreshSelectedCards()
        {
            foreach (Transform child in selectedCardsContainer) Destroy(child.gameObject);

            foreach (var id in currentDeck)
            {
                var card = Core.CardManager.Instance.GetCardById(id);
                GameObject obj = Instantiate(cardPrefab, selectedCardsContainer);
                var view = obj.GetComponent<CardView>();
                view.Setup(card);

                Button btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    string capturedId = id;
                    btn.onClick.AddListener(() => ToggleCardInDeck(capturedId));
                }
            }

            if (countText != null)
                countText.text = $"Deste: {currentDeck.Count} / 22+";
        }

        public void SelectLeader(string leaderId)
        {
            selectedLeaderId = leaderId;
        }

        private async void HandleSaveDeck()
        {
            // userId kontrolü
            string userId = Core.GameManager.Instance.LocalPlayerId;
            if (string.IsNullOrEmpty(userId))
            {
                Gwent.UI.UIManager.Instance.ShowFeedback("Hata: Kullanıcı kimliği bulunamadı!");
                return;
            }

            // Final Kontrol: En az 22 birim kart (Unit/Hero) olmalı
            int unitCount = currentDeck.Count(id => {
                var c = Core.CardManager.Instance.GetCardById(id);
                return c != null && (c.cardType == CardType.Unit || c.cardType == CardType.Hero);
            });

            if (unitCount < 22)
            {
                Gwent.UI.UIManager.Instance.ShowFeedback($"Yetersiz birim kartı! ({unitCount}/22)");
                return;
            }

            if (string.IsNullOrEmpty(selectedLeaderId))
            {
                Gwent.UI.UIManager.Instance.ShowFeedback("Lütfen bir Lider kartı seçin!");
                return;
            }

            await UserProfileManager.Instance.SaveDeck(userId, selectedLeaderId, currentDeck);
            Gwent.UI.UIManager.Instance.ShowFeedback("Deste başarıyla kaydedildi!");
            CloseDeckBuilder();
        }
    }
}
