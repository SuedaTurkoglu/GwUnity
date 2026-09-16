using UnityEngine;
using Gwent.Models;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Gwent.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; }
        public string LocalPlayerId { get; set; }

        // Event to notify UI when the game officially starts
        public event Action OnGameStarted;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void UpdateGameState(GameState newState)
        {
            CurrentState = newState;
            CalculateScores();

            // Notify UI to refresh board
            if (Gwent.UI.UIManager.Instance != null)
            {
                Gwent.UI.UIManager.Instance.RefreshBoard(CurrentState);
            }

            // If the game just transitioned to 'Playing', trigger the event
            if (CurrentState.status == GameStatus.Playing)
            {
                OnGameStarted?.Invoke();
            }
        }

        private void CalculateScores()
        {
            if (CurrentState == null) return;

            int p1Sum = 0;
            int p2Sum = 0;

            p1Sum += SumRow(CurrentState.p1Melee);
            p1Sum += SumRow(CurrentState.p1Ranged);
            p1Sum += SumRow(CurrentState.p1Siege);

            p2Sum += SumRow(CurrentState.p2Melee);
            p2Sum += SumRow(CurrentState.p2Ranged);
            p2Sum += SumRow(CurrentState.p2Siege);

            CurrentState.p1TotalStrength = p1Sum;
            CurrentState.p2TotalStrength = p2Sum;
        }

        private int SumRow(List<string> cardIds)
        {
            int sum = 0;
            foreach (var id in cardIds)
            {
                var card = CardManager.Instance.GetCardById(id);
                if (card != null) sum += card.strength;
            }
            return sum;
        }

        public bool CanPlayCard()
        {
            if (CurrentState == null) return false;
            return CurrentState.currentTurnPlayerId == LocalPlayerId;
        }

        public void PlayCard(string cardId, string rowType)
        {
            if (!CanPlayCard()) return;
            // Handled by FirestoreGameManager.Instance.PushMove
        }
    }
}
