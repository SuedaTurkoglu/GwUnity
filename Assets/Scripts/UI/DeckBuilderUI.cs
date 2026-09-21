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
        public Transform availableCardsContainer; // Havuzdaki kartların listelendiği alan
        public Transform selectedCardsContainer;  // Desteye eklenen kartların alanı
        public Transform leaderSelectionContainer; // Lider seçim alanı

        [Header("UI Elements")]
        public TextMeshProUGUI countText;       // Örn: "Birlik: 18 / 22 (Min) | Özel: 4 / 10 (Max)"
        public TextMeshProUGUI leaderText;      // Seçili Lider Bilgisi
        public Button saveButton;
        public TMP_Dropdown factionFilter;      // Faksiyon filtreleme

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
            RefreshLeaderCards();
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
                case "Nilfgaard İmparatorluğu": currentFaction = "Nilfgaard"; break;
                case "Scoia'tael": currentFaction = "ScoiaTael"; break;
                case "Canavarlar": currentFaction = "Monsters"; break;
                case "Skellige": currentFaction = "Skellige"; break;
                default: currentFaction = "All"; break;
            }
            RefreshAvailableCards();
            RefreshLeaderCards();
        }

        public void RefreshAvailableCards()
        {
            foreach (Transform child in availableCardsContainer) Destroy(child.gameObject);

            var allCards = Core.CardManager.Instance.GetAllCards();
            
            // Liderler haricindeki birim ve özel kartları filtrele
            var filtered = allCards.Where(c => 
                c.cardType != CardType.Leader && 
                (currentFaction == "All" || c.faction == currentFaction || c.faction == "Neutral")
            ).ToList();

            foreach (var card in filtered)
            {
                GameObject obj = Instantiate(cardPrefab, availableCardsContainer);
                var view = obj.GetComponent<CardView>();
                if (view != null) view.Setup(card);

                Button btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    string id = card.id;
                    btn.onClick.AddListener(() => ToggleCardInDeck(id));
                }
            }
        }

        public void RefreshLeaderCards()
        {
            if (leaderSelectionContainer == null) return;
            foreach (Transform child in leaderSelectionContainer) Destroy(child.gameObject);

            var allCards = Core.CardManager.Instance.GetAllCards();
            var leaders = allCards.Where(c => 
                (c.cardType == CardType.Leader || c.id.Contains("_l")) &&
                (currentFaction == "All" || c.faction == currentFaction)
            ).ToList();

            foreach (var leader in leaders)
            {
                GameObject obj = Instantiate(cardPrefab, leaderSelectionContainer);
                var view = obj.GetComponent<CardView>();
                if (view != null) view.Setup(leader);

                Button btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    string lId = leader.id;
                    btn.onClick.AddListener(() => SelectLeader(lId));
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
                // --- GWENT RESMİ DESSA OLUŞTURMA KURALLARI ---

                // KURAL 1: Tek Faksiyon Kuralı (Neutral Kartlar İstisnadır)
                string activeDeckFaction = GetCurrentDeckFaction();
                
                if (activeDeckFaction != "Neutral" && card.faction != "Neutral" && card.faction != activeDeckFaction)
                {
                    UI.UIManager.Instance.ShowFeedback($"Sadece {activeDeckFaction} faksiyonuna ait kartlar ekleyebilirsiniz!");
                    return;
                }

                // Seçili Lider varsa, liderin faksiyonuna uymayan kart eklenemez
                if (!string.IsNullOrEmpty(selectedLeaderId))
                {
                    var leaderCard = Core.CardManager.Instance.GetCardById(selectedLeaderId);
                    if (leaderCard != null && card.faction != "Neutral" && card.faction != leaderCard.faction)
                    {
                        UI.UIManager.Instance.ShowFeedback($"Seçilen Lider ({leaderCard.faction}) ile kart faksiyonu uyuşmuyor!");
                        return;
                    }
                }

                // KURAL 2: Özel Kart Limiti (Maksimum 10)
                if (card.cardType == CardType.Special)
                {
                    int specialCount = currentDeck.Count(id => {
                        var c = Core.CardManager.Instance.GetCardById(id);
                        return c != null && c.cardType == CardType.Special;
                    });

                    if (specialCount >= 10)
                    {
                        UI.UIManager.Instance.ShowFeedback("En fazla 10 Özel Kart ekleyebilirsiniz!");
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
                if (view != null) view.Setup(card);

                Button btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    string capturedId = id;
                    btn.onClick.AddListener(() => ToggleCardInDeck(capturedId));
                }
            }

            // --- GWENT SAYAÇ HESAPLAMALARI ---
            int unitCount = currentDeck.Count(id => {
                var c = Core.CardManager.Instance.GetCardById(id);
                return c != null && (c.cardType == CardType.Unit || c.cardType == CardType.Hero);
            });

            int specialCount = currentDeck.Count(id => {
                var c = Core.CardManager.Instance.GetCardById(id);
                return c != null && c.cardType == CardType.Special;
            });

            if (countText != null)
                countText.text = $"Birim: {unitCount}/22 (Min) | Özel: {specialCount}/10 (Max)";

            if (leaderText != null)
            {
                var leader = Core.CardManager.Instance.GetCardById(selectedLeaderId);
                leaderText.text = leader != null ? $"Lider: {leader.name}" : "Lider: Seçilmedi";
            }
        }

        public void SelectLeader(string leaderId)
        {
            var leader = Core.CardManager.Instance.GetCardById(leaderId);
            if (leader == null) return;

            // Destedeki mevcut faksiyon ile Lider faksiyonu çakışıyor mu?
            string deckFaction = GetCurrentDeckFaction();
            if (deckFaction != "Neutral" && leader.faction != deckFaction)
            {
                UI.UIManager.Instance.ShowFeedback($"Lider faksiyonu ({leader.faction}) destedeki kartlarla ({deckFaction}) uyuşmuyor!");
                return;
            }

            selectedLeaderId = leaderId;
            UI.UIManager.Instance.ShowFeedback($"Lider Seçildi: {leader.name}");
            RefreshSelectedCards();
        }

        private string GetCurrentDeckFaction()
        {
            foreach (var id in currentDeck)
            {
                var c = Core.CardManager.Instance.GetCardById(id);
                if (c != null && c.faction != "Neutral")
                {
                    return c.faction;
                }
            }
            return "Neutral";
        }

        private async void HandleSaveDeck()
        {
            string userId = Core.GameManager.Instance.LocalPlayerId;
            if (string.IsNullOrEmpty(userId))
            {
                UI.UIManager.Instance.ShowFeedback("Hata: Kullanıcı kimliği bulunamadı!");
                return;
            }

            // KURAL 3: Tam Olarak 1 Lider Kartı Olmalı
            if (string.IsNullOrEmpty(selectedLeaderId))
            {
                UI.UIManager.Instance.ShowFeedback("Lütfen desteniz için 1 adet Lider Kartı seçin!");
                return;
            }

            // KURAL 4: En Az 22 Birim Kartı (Unit/Hero) Olmalı
            int unitCount = currentDeck.Count(id => {
                var c = Core.CardManager.Instance.GetCardById(id);
                return c != null && (c.cardType == CardType.Unit || c.cardType == CardType.Hero);
            });

            if (unitCount < 22)
            {
                UI.UIManager.Instance.ShowFeedback($"Yetersiz Birim Kartı! En az 22 Birlik kartı eklemelisiniz. (Mevcut: {unitCount})");
                return;
            }

            await UserProfileManager.Instance.SaveDeck(userId, selectedLeaderId, currentDeck);
            UI.UIManager.Instance.ShowFeedback("Deste başarıyla kaydedildi!");
            CloseDeckBuilder();
        }
    }
}