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

        public event Action OnGameStarted;
        public event Action OnGameFinished; // YENİ: winnerId dolunca (status == Finished) tetiklenir

        private GameStatus _previousStatus = GameStatus.Waiting;

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

            if (Gwent.UI.UIManager.Instance != null)
            {
                Gwent.UI.UIManager.Instance.RefreshBoard(CurrentState);
            }

            if (CurrentState.status == GameStatus.Playing && _previousStatus != GameStatus.Playing)
            {
                OnGameStarted?.Invoke();
            }
            else if (CurrentState.status == GameStatus.Finished && _previousStatus != GameStatus.Finished)
            {
                OnGameFinished?.Invoke();
            }

            _previousStatus = CurrentState.status;
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
    }
}