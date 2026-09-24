using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using Gwent.Networking;
using Gwent.Core;
using Gwent.Models;

namespace Gwent.UI
{
    public class LobbyManager : MonoBehaviour
    {
        // --- 1. SINGLETON INSTANCE EKLEMESİ ---
        public static LobbyManager Instance { get; private set; }

        [Header("Main UI References")]
        public TextMeshProUGUI matchIdDisplay;
        public TMP_InputField matchIdInput;
        public TextMeshProUGUI statusText;

        [Header("Panels")]
        public GameObject mainLobbyPanel; // Ana Lobi Ekranı (Oluştur/Katıl paneli)
        public GameObject activeMatchesPanel; // Aktif Maçlar Listesi Penceresi (LobbyScrollView)
        public GameObject gamePanel;

        [Header("Buttons")]
        public Button createMatchButton;
        public Button joinWithInputButton;
        public Button openLobbyListButton;
        public Button refreshLobbyButton;

        [Header("Lobby List UI Panel")]
        public Transform matchItemContainer;
        public GameObject matchItemPrefab;

        private void Awake()
        {
            // Singleton kurulumu
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            FirestoreGameManager.OnGameStarted += HandleGameStarted;
        }

        private void OnDisable()
        {
            FirestoreGameManager.OnGameStarted -= HandleGameStarted;
        }

        private void Start()
        {
            if (createMatchButton != null)
                createMatchButton.onClick.AddListener(OnCreateMatchClicked);

            if (joinWithInputButton != null)
                joinWithInputButton.onClick.AddListener(OnJoinWithInputClicked);

            if (openLobbyListButton != null)
                openLobbyListButton.onClick.AddListener(ToggleLobbyListPanel);

            if (refreshLobbyButton != null)
                refreshLobbyButton.onClick.AddListener(OnRefreshLobbyClicked);

            // Başlangıçta hem maç listesini hem de yenileme butonunu gizle
            SetLobbyListVisibility(false);
        }

        /// <summary>
        /// Maç başladığında (GameStatus.Playing olduğunda) otomatik tetiklenir.
        /// Lobi panellerini kapatıp Oyun Panelini açar.
        /// </summary>
        private void HandleGameStarted(GameState state)
        {
            SetStatus("Maç başladı! Oyuna yönlendiriliyorsunuz...");

            // Lobiye ait panelleri kapat
            if (mainLobbyPanel != null)
                mainLobbyPanel.SetActive(false);

            SetLobbyListVisibility(false);

            // Oyun sahası/ekranı panelini aç
            if (gamePanel != null)
                gamePanel.SetActive(true);
        }

        /// <summary>
        /// GameOverUI veya oyun sonu ekranından tekrar lobiye dönüldüğünde çağrılır.
        /// </summary>
        public void ReturnToLobby()
        {
            if (FirestoreGameManager.Instance != null)
            {
                FirestoreGameManager.Instance.LeaveMatch();
            }

            if (mainLobbyPanel != null)
                mainLobbyPanel.SetActive(true);

            // Lobiye dönüldüğünde maç listesini ve yenile butonunu gizle
            SetLobbyListVisibility(false);

            if (matchIdInput != null) matchIdInput.text = "";
            if (matchIdDisplay != null) matchIdDisplay.text = "";

            SetStatus("Lobiye dönüldü. Yeni bir maç oluşturabilir veya var olana katılabilirsiniz.");
        }

        /// <summary>
        /// openLobbyListButton basıldığında LobbyScrollView (activeMatchesPanel) 
        /// ve refreshLobbyButton elemanlarını aynı anda görünür/gizli yapar.
        /// </summary>
        public void ToggleLobbyListPanel()
        {
            if (activeMatchesPanel == null) return;

            // Mevcut durumu alıp tersine çeviriyoruz (Toggle)
            bool isCurrentlyActive = activeMatchesPanel.activeSelf;
            bool newState = !isCurrentlyActive;

            SetLobbyListVisibility(newState);

            // Eğer görünür yapıldıysa aktif maçları çek
            if (newState)
            {
                OnRefreshLobbyClicked();
            }
        }

        /// <summary>
        /// Liste paneli ve Yenile butonunun görünürlüğünü tek noktadan yönetir.
        /// </summary>
        private void SetLobbyListVisibility(bool isVisible)
        {
            if (activeMatchesPanel != null)
                activeMatchesPanel.SetActive(isVisible);

            if (refreshLobbyButton != null)
                refreshLobbyButton.gameObject.SetActive(isVisible);
        }

        private async void OnCreateMatchClicked()
        {
            string pId = GameManager.Instance.LocalPlayerId;
            if (string.IsNullOrEmpty(pId))
            {
                SetStatus("Kullanıcı kimliği oluşturulamadı!", isError: true);
                return;
            }

            SetStatus("Maç oluşturuluyor...");

            try
            {
                GameManager.Instance.LocalFaction = GetSelectedFactionCode();
                string matchId = await FirestoreGameManager.Instance.CreateMatch(pId);

                if (matchIdDisplay != null)
                    matchIdDisplay.text = $"Maç Kodu: {matchId}";

                SetStatus($"Maç Kuruldu [{matchId}]. Rakip bekleniyor...");
            }
            catch (Exception e)
            {
                Debug.LogError($"CreateMatch failed: {e}");
                SetStatus("Maç oluşturulamadı.", isError: true);
            }
        }

        public void OnJoinWithInputClicked()
        {
            if (matchIdInput == null || string.IsNullOrEmpty(matchIdInput.text))
            {
                SetStatus("Lütfen geçerli bir Maç Kodu girin!", isError: true);
                return;
            }

            JoinSelectedMatch(matchIdInput.text.Trim());
        }

        public void JoinSelectedMatch(string matchId)
        {
            SetStatus($"{matchId} maçına bağlanılıyor...");
            GameManager.Instance.LocalFaction = GetSelectedFactionCode();
            FirestoreGameManager.Instance.JoinMatch(matchId);
        }

        public async void OnRefreshLobbyClicked()
        {
            SetStatus("Aktif maçlar yükleniyor...");

            // Yenilemeye basıldığında görünürlüklerinin açık olduğundan emin ol
            SetLobbyListVisibility(true);

            if (matchItemContainer != null)
            {
                foreach (Transform child in matchItemContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            List<GameState> waitingMatches = await FirestoreGameManager.Instance.GetWaitingMatches();

            if (waitingMatches.Count == 0)
            {
                SetStatus("Şu anda bekleyen aktif maç bulunamadı.");
                return;
            }

            foreach (var match in waitingMatches)
            {
                if (matchItemPrefab != null && matchItemContainer != null)
                {
                    GameObject itemObj = Instantiate(matchItemPrefab, matchItemContainer);
                    LobbyMatchItem item = itemObj.GetComponent<LobbyMatchItem>();
                    if (item != null)
                    {
                        item.Setup(match, (selectedMatchId) =>
                        {
                            JoinSelectedMatch(selectedMatchId);
                        });
                    }
                }
            }

            SetStatus($"{waitingMatches.Count} adet aktif maç bulundu.");
        }

        private string GetSelectedFactionCode()
        {
            return GameManager.Instance.LocalFaction ?? "Northern";
        }

        private void SetStatus(string msg, bool isError = false)
        {
            if (statusText != null)
            {
                statusText.text = msg;
                statusText.color = isError ? Color.red : Color.white;
            }
        }
    }
}