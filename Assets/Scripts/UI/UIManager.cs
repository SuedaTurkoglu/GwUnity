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
            if (feedbackText == null) return;

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
    }
}