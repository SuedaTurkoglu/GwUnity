using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
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
        public TextMeshProUGUI statusText; // YENİ: hata / bekleme mesajları için
        public Button createButton;
        public Button joinButton;

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
            lobbyPanel.SetActive(true);
            gamePanel.SetActive(false);
            SetStatus(string.Empty);

            createButton.onClick.AddListener(OnCreateMatchClicked);
            joinButton.onClick.AddListener(OnJoinMatchClicked);

            GameManager.Instance.OnGameStarted += EnterGame;
        }

        private async void OnCreateMatchClicked()
        {
            string pId = playerIdInput.text.Trim();
            if (string.IsNullOrEmpty(pId))
            {
                SetStatus("Lütfen bir Player ID girin.", isError: true);
                return;
            }

            SetButtonsInteractable(false);
            SetStatus("Maç oluşturuluyor...");

            try
            {
                GameManager.Instance.LocalPlayerId = pId;
                string matchId = await FirestoreGameManager.Instance.CreateMatch(pId);

                matchIdDisplay.text = $"Match ID: {matchId}";
                SetStatus("Rakip bekleniyor... Bu ID'yi ikinci cihazla paylaş.");
            }
            catch (Exception e)
            {
                Debug.LogError($"CreateMatch failed: {e}");
                SetStatus("Maç oluşturulamadı. İnternet bağlantınızı kontrol edin.", isError: true);
                SetButtonsInteractable(true);
            }
            // Not: create başarılı olunca joinButton'ı da kapalı tutuyoruz (SetButtonsInteractable(false) kalır),
            // çünkü bu oyuncu artık kurucu taraf; sırada karşı tarafın katılması ve OnGameStarted event'inin
            // tetiklenmesi var.
        }

        private async void OnJoinMatchClicked()
        {
            string pId = playerIdInput.text.Trim();
            string mId = matchIdInput.text.Trim();

            if (string.IsNullOrEmpty(pId) || string.IsNullOrEmpty(mId))
            {
                SetStatus("Lütfen hem Player ID hem Match ID girin.", isError: true);
                return;
            }

            SetButtonsInteractable(false);
            SetStatus("Maça katılınıyor...");

            try
            {
                GameManager.Instance.LocalPlayerId = pId;
                FirestoreGameManager.Instance.JoinMatch(mId); // void: sadece Firestore listener'ı kuruyor
                SetStatus("Maça katılınıyor, kartlar dağıtılıyor...");
                // UI geçişi burada değil, OnGameStarted event'i tetiklenince (EnterGame) olacak.
            }
            catch (Exception e)
            {
                Debug.LogError($"JoinMatch failed: {e}");
                SetStatus("Maça katılamadınız. Match ID'yi kontrol edin.", isError: true);
                SetButtonsInteractable(true);
            }
        }

        public void EnterGame()
        {
            lobbyPanel.SetActive(false);
            gamePanel.SetActive(true);
        }

        private void SetButtonsInteractable(bool value)
        {
            createButton.interactable = value;
            joinButton.interactable = value;
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = isError ? new Color(0.85f, 0.3f, 0.25f) : new Color(0.85f, 0.77f, 0.56f);
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