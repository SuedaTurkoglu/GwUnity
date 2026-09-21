using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Gwent.Models;
using Gwent.Core;
using Gwent.Networking;

namespace Gwent.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Player Boards")]
        public TextMeshProUGUI p1ScoreText;
        public TextMeshProUGUI p2ScoreText;
        public Transform p1MeleeContainer;
        public Transform p1RangedContainer;
        public Transform p1SiegeContainer;
        public Transform p2MeleeContainer;
        public Transform p2RangedContainer;
        public Transform p2SiegeContainer;

        [Header("Leaders & Deck")]
        public UnityEngine.UI.Image myLeaderImage;
        public UnityEngine.UI.Image opponentLeaderImage;
        public GameObject myLeaderUsedOverlay;
        public GameObject opponentLeaderUsedOverlay;
        public TextMeshProUGUI remainingDeckText;
        public GameObject deckPopupPanel;
        public Transform deckGridContainer;


        [Header("Player Hand")]
        public Transform handContainer;
        public GameObject cardPrefab;

        [Header("Round / Game Info")]
        public TextMeshProUGUI roundInfoText; // "Round 2 - Senin Canın: 2 / Rakip: 1" gibi
        public TextMeshProUGUI feedbackText; // Kullanıcıya anlık geri bildirimler (Sıra sende, hata vb.)


        private string _selectedCardId;
        private CardView _selectedCardView; // Seçili olan kartın görsel referansı
        private System.Collections.IEnumerator _feedbackCoroutine;


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
        }

        void Start()
        {
            // Deste textine tıklama özelliğini kodla ekle
            if (remainingDeckText != null)
            {
                Button btn = remainingDeckText.GetComponent<Button>();
                if (btn == null)
                {
                    btn = remainingDeckText.gameObject.AddComponent<Button>();
                }
                btn.onClick.AddListener(() => {
                    OpenDeckPopup(Core.GameManager.Instance.CurrentState);
                });
            }
        }

        public void RefreshBoard(GameState state)
        {
            if (state == null) return;

            p1ScoreText.text = $"P1 Total: {state.p1TotalStrength}";
            p2ScoreText.text = $"P2 Total: {state.p2TotalStrength}";

            if (roundInfoText != null)
            {
                roundInfoText.text = $"Round {state.currentRound}  |  Sen: {GetLocalLives(state)} can  Rakip: {GetOpponentLives(state)} can";
            }

            UpdateRowUI(p1MeleeContainer, state.p1Melee);
            UpdateRowUI(p1RangedContainer, state.p1Ranged);
            UpdateRowUI(p1SiegeContainer, state.p1Siege);

            UpdateRowUI(p2MeleeContainer, state.p2Melee);
            UpdateRowUI(p2RangedContainer, state.p2Ranged);
            UpdateRowUI(p2SiegeContainer, state.p2Siege);

            UpdateLocalHand(state);
            UpdateLeaderUI(state);
            UpdateDeckCount(state);
        }

        private int GetLocalLives(GameState state)
        {
            string localId = Core.GameManager.Instance.LocalPlayerId;
            return localId == state.player1Id ? state.p1Lives : state.p2Lives;
        }

        private int GetOpponentLives(GameState state)
        {
            string localId = Core.GameManager.Instance.LocalPlayerId;
            return localId == state.player1Id ? state.p2Lives : state.p1Lives;
        }

        private void UpdateLocalHand(GameState state)
        {
            string localId = Core.GameManager.Instance.LocalPlayerId;
            if (localId == state.player1Id)
            {
                UpdateHand(state.p1Hand);
            }
            else if (localId == state.player2Id)
            {
                UpdateHand(state.p2Hand);
            }
        }

        private void UpdateRowUI(Transform container, List<string> cardIds)
        {
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }

            foreach (var id in cardIds)
            {
                var cardData = CardManager.Instance.GetCardById(id);
                if (cardData == null)
                {
                    Debug.LogWarning($"Card id bulunamadı (henüz yüklenmemiş olabilir): {id}");
                    continue;
                }

                GameObject cardObj = Instantiate(cardPrefab, container);
                var cardView = cardObj.GetComponent<CardView>();
                if (cardView != null) cardView.Setup(cardData);
            }
        }

        public void UpdateHand(List<string> handCardIds)
        {
            foreach (Transform child in handContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var id in handCardIds)
            {
                var cardData = CardManager.Instance.GetCardById(id);
                if (cardData == null) continue;

                GameObject cardObj = Instantiate(cardPrefab, handContainer);
                var cardView = cardObj.GetComponent<CardView>();
                if (cardView != null) cardView.Setup(cardData);

                Button btn = cardObj.GetComponent<Button>();
                if (btn != null)
                {
                    string capturedId = id; // closure için yerel kopya
                    CardView capturedView = cardView; // closure için yerel kopya
                    btn.onClick.AddListener(() => SelectCard(capturedView, capturedId));
                }
            }
        }

        private void SelectCard(CardView cardView, string id)
        {
            // Önceki seçili kartın outline'ını kapat
            if (_selectedCardView != null)
            {
                _selectedCardView.SetSelected(false);
            }

            _selectedCardId = id;
            _selectedCardView = cardView;

            // Yeni seçilen kartın outline'ını aç
            if (_selectedCardView != null)
            {
                _selectedCardView.SetSelected(true);
            }

            Debug.Log($"Selected card: {id}");
        }

        public void OnRowClicked(string rowType)
        {
            if (string.IsNullOrEmpty(_selectedCardId)) return;

            if (!Core.GameManager.Instance.CanPlayCard())
            {
                ShowFeedback("Sıra sende değil, kart oynayamazsın.");
                return;
            }

            // YENİ: kart sadece kendi tanımlı satırında (row) oynanabilir.
            var cardData = CardManager.Instance.GetCardById(_selectedCardId);
            if (cardData == null)
            {
                ShowFeedback("Kart verisi bulunamadı!");
                return;
            }

            if (cardData.row != "Any" && cardData.row != rowType)
            {
                ShowFeedback($"'{cardData.name}' sadece {cardData.row} sırasına oynanabilir.");
                return; // seçim iptal olmuyor, kullanıcı doğru sıraya tıklayabilir
            }

            FirestoreGameManager.Instance.PushMove(_selectedCardId, rowType);

            // Kart oynandı, outline'ı kapat
            if (_selectedCardView != null)
            {
                _selectedCardView.SetSelected(false);
                _selectedCardView = null;
            }

            _selectedCardId = null;
        }

        public void OnPassClicked()
        {
            if (!Core.GameManager.Instance.CanPlayCard())
            {
                ShowFeedback("Sıra sende değil, pas geçemezsin.");
                return;
            }

            FirestoreGameManager.Instance.PushPass();
        }

        public void ShowFeedback(string message, float duration = 3f)
        {
            Debug.Log($"[Feedback System] Mesaj gönderildi: {message}");

            if (feedbackText == null)
            {
                Debug.LogError("[Feedback System] HATA: feedbackText referansı atanmamış! Lütfen Inspector panelinden atama yapın.");
                return;
            }

            if (_feedbackCoroutine != null)
            {
                StopCoroutine(_feedbackCoroutine);
            }

            feedbackText.text = message;
            _feedbackCoroutine = ClearFeedbackAfterDelay(duration);
            StartCoroutine(_feedbackCoroutine);
        }

        private System.Collections.IEnumerator ClearFeedbackAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            feedbackText.text = "";
            _feedbackCoroutine = null;
        }

        private void UpdateLeaderUI(GameState state)
        {
            if (state == null) return;

            string localId = Core.GameManager.Instance.LocalPlayerId;
            string myLeaderId = (localId == state.player1Id) ? state.p1LeaderId : state.p2LeaderId;
            string oppLeaderId = (localId == state.player1Id) ? state.p2LeaderId : state.p1LeaderId;
            bool myAbilityUsed = (localId == state.player1Id) ? state.p1LeaderAbilityUsed : state.p2LeaderAbilityUsed;
            bool oppAbilityUsed = (localId == state.player1Id) ? state.p2LeaderAbilityUsed : state.p1LeaderAbilityUsed;

            if (myLeaderImage != null && !string.IsNullOrEmpty(myLeaderId))
            {
                var card = CardManager.Instance.GetCardById(myLeaderId);
                if (card != null)
                {
                    // CardData.imagePath kullanılıyor, ancak Image component'ine atama yapmak için
                    // CardView'dan veya bir asset loader'dan sprite alınmalı.
                    // Geçici olarak log basıyoruz, CardView'daki sprite atama mantığına göre güncellenmeli.
                    Debug.Log($"My Leader Sprite Path: {card.imagePath}");
                }
            }

            if (opponentLeaderImage != null && !string.IsNullOrEmpty(oppLeaderId))
            {
                var card = CardManager.Instance.GetCardById(oppLeaderId);
                if (card != null)
                {
                    Debug.Log($"Opponent Leader Sprite Path: {card.imagePath}");
                }
            }

            if (myLeaderUsedOverlay != null)
            {
                myLeaderUsedOverlay.SetActive(myAbilityUsed);
            }

            if (opponentLeaderUsedOverlay != null)
            {
                opponentLeaderUsedOverlay.SetActive(oppAbilityUsed);
            }
        }

        private void UpdateDeckCount(GameState state)
        {
            if (state == null || remainingDeckText == null) return;

            string localId = Core.GameManager.Instance.LocalPlayerId;
            int count = (localId == state.player1Id) ? state.p1Deck.Count : state.p2Deck.Count;
            remainingDeckText.text = $"Kalan Deste: {count}";
        }

        public void OpenDeckPopup(GameState state)
        {
            if (state == null || deckPopupPanel == null || deckGridContainer == null) return;

            deckPopupPanel.SetActive(true);

            // Temizle
            foreach (Transform child in deckGridContainer) Destroy(child.gameObject);

            string localId = Core.GameManager.Instance.LocalPlayerId;
            List<string> localDeck = (localId == state.player1Id) ? state.p1Deck : state.p2Deck;

            foreach (var id in localDeck)
            {
                var cardData = CardManager.Instance.GetCardById(id);
                if (cardData == null) continue;

                GameObject cardObj = Instantiate(cardPrefab, deckGridContainer);
                var cardView = cardObj.GetComponent<CardView>();
                if (cardView != null) cardView.Setup(cardData);
            }
        }

        public void CloseDeckPopup()
        {
            if (deckPopupPanel != null) deckPopupPanel.SetActive(false);
        }
    }
}