using UnityEngine;
using UnityEngine.UI;
using TMPro; // Use TextMeshPro for better quality
using System.Collections.Generic;
using Gwent.Core;
using Gwent.Networking;

namespace Gwent.UI
{
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        [Header("UI Elements")]
        public GameObject lobbyPanel;
        public GameObject gamePanel;
        public TMP_InputField playerIdInput;
        public TMP_InputField matchIdInput;
        public TextMeshProUGUI matchIdDisplay;
        public Button createButton;
        public Button joinButton;

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        void Start()
        {
            lobbyPanel.SetActive(true);
            gamePanel.SetActive(false);

            createButton.onClick.AddListener(OnCreateMatchClicked);
            joinButton.onClick.AddListener(OnJoinMatchClicked);

            // Listen for game start event
            GameManager.Instance.OnGameStarted += EnterGame;
        }

        private async void OnCreateMatchClicked()
        {
            string pId = playerIdInput.text;
            if (string.IsNullOrEmpty(pId))
            {
                Debug.LogError("Please enter a Player ID");
                return;
            }

            GameManager.Instance.LocalPlayerId = pId;
            string matchId = await FirestoreGameManager.Instance.CreateMatch(pId);

            matchIdDisplay.text = $"Match ID: {matchId}";
            Debug.Log($"Match Created: {matchId}");

            createButton.interactable = false;
        }

        private void OnJoinMatchClicked()
        {
            string pId = playerIdInput.text;
            string mId = matchIdInput.text;

            if (string.IsNullOrEmpty(pId) || string.IsNullOrEmpty(mId))
            {
                Debug.LogError("Please enter both Player ID and Match ID");
                return;
            }

            GameManager.Instance.LocalPlayerId = pId;
            FirestoreGameManager.Instance.JoinMatch(mId);
            Debug.Log($"Joining Match: {mId}");
        }

        public void EnterGame()
        {
            lobbyPanel.SetActive(false);
            gamePanel.SetActive(true);
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted -= EnterGame;
            }
        }
    }
}
