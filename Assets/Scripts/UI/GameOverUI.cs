using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gwent.Core;
using Gwent.Networking;

namespace Gwent.UI
{
    public class GameOverUI : MonoBehaviour
    {
        [Header("Refs")]
        public GameObject root; // GameOverOverlay'in kendisi
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI scoreText;
        public Button returnButton;

        void Awake()
        {
            if (root != null) root.SetActive(false);
            if (returnButton != null) returnButton.onClick.AddListener(HandleReturnClicked);
        }

        void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameFinished += HandleGameFinished;
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameFinished -= HandleGameFinished;
            }
        }

        private void HandleGameFinished()
        {
            var state = GameManager.Instance.CurrentState;
            if (state == null) return;

            bool localWon = state.winnerId == GameManager.Instance.LocalPlayerId;

            if (titleText != null)
            {
                titleText.text = localWon ? "Kazandın!" : "Kaybettin!";
                titleText.color = localWon ? new Color(0.72f, 0.85f, 0.55f) : new Color(0.85f, 0.35f, 0.3f);
            }

            if (scoreText != null)
            {
                bool isPlayer1 = GameManager.Instance.LocalPlayerId == state.player1Id;
                int myRounds = isPlayer1 ? state.p1RoundsWon : state.p2RoundsWon;
                int oppRounds = isPlayer1 ? state.p2RoundsWon : state.p1RoundsWon;
                scoreText.text = $"Round Skoru: {myRounds} - {oppRounds}";
            }

            if (root != null) root.SetActive(true);
        }

        private void HandleReturnClicked()
        {
            if (root != null) root.SetActive(false);

            FirestoreGameManager.Instance?.LeaveMatch();
            GameManager.Instance?.ResetState();
            LobbyManager.Instance?.ReturnToLobby();
        }
    }
}