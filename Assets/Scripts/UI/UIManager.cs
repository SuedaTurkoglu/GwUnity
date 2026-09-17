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

        private string _selectedCardId;

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
                var label = cardObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = $"{cardData.name} ({cardData.strength})";
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
                var label = cardObj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = cardData.name;

                Button btn = cardObj.GetComponent<Button>();
                if (btn != null)
                {
                    string capturedId = id; // closure için yerel kopya
                    btn.onClick.AddListener(() => SelectCard(capturedId));
                }
            }
        }

        private void SelectCard(string id)
        {
            _selectedCardId = id;
            Debug.Log($"Selected card: {id}");
        }

        public void OnRowClicked(string rowType)
        {
            if (string.IsNullOrEmpty(_selectedCardId)) return;

            // DÜZELTME: sıra kontrolü olmadan rakip de kart oynayabiliyordu
            if (!Core.GameManager.Instance.CanPlayCard())
            {
                Debug.Log("Sıra sende değil, kart oynayamazsın.");
                return;
            }

            FirestoreGameManager.Instance.PushMove(_selectedCardId, rowType);
            _selectedCardId = null;
        }

        public void OnPassClicked()
        {
            if (!Core.GameManager.Instance.CanPlayCard())
            {
                Debug.Log("Sıra sende değil, pas geçemezsin.");
                return;
            }

            FirestoreGameManager.Instance.PushPass();
        }
    }
}