using UnityEngine;
using UnityEngine.UI;
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
        public Text p1ScoreText;
        public Text p2ScoreText;
        public Transform p1MeleeContainer;
        public Transform p1RangedContainer;
        public Transform p1SiegeContainer;
        public Transform p2MeleeContainer;
        public Transform p2RangedContainer;
        public Transform p2SiegeContainer;

        [Header("Player Hand")]
        public Transform handContainer;
        public GameObject cardPrefab;

        private string _selectedCardId;

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public void RefreshBoard(GameState state)
        {
            if (state == null) return;

            p1ScoreText.text = $"P1 Total: {state.p1TotalStrength}";
            p2ScoreText.text = $"P2 Total: {state.p2TotalStrength}";

            UpdateRowUI(p1MeleeContainer, state.p1Melee);
            UpdateRowUI(p1RangedContainer, state.p1Ranged);
            UpdateRowUI(p1SiegeContainer, state.p1Siege);

            UpdateRowUI(p2MeleeContainer, state.p2Melee);
            UpdateRowUI(p2RangedContainer, state.p2Ranged);
            UpdateRowUI(p2SiegeContainer, state.p2Siege);

            // Update the local player's hand
            UpdateLocalHand(state);
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
            // Clear existing cards
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }

            // Add cards from state
            foreach (var id in cardIds)
            {
                var cardData = CardManager.Instance.GetCardById(id);
                GameObject cardObj = Instantiate(cardPrefab, container);
                cardObj.GetComponentInChildren<Text>().text = cardData.name + " (" + cardData.strength + ")";
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
                GameObject cardObj = Instantiate(cardPrefab, handContainer);
                cardObj.GetComponentInChildren<Text>().text = cardData.name;

                // Add button listener to select card
                Button btn = cardObj.GetComponent<Button>();
                btn.onClick.AddListener(() => SelectCard(id));
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

            FirestoreGameManager.Instance.PushMove(_selectedCardId, rowType);
            _selectedCardId = null;
        }
    }
}
