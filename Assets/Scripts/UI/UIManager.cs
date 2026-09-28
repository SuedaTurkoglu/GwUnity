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
        public Transform myLeaderContainer;
        public Transform opponentLeaderContainer;
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


        [Header("Graveyard References")]
        public RectTransform p1GraveyardTransform; // P1 Mezarlık UI Objesi
        public RectTransform p2GraveyardTransform; // P2 Mezarlık UI Objes

        private int _lastRound = 1;

        [Header("Pass Feedback References")]
        public GameObject p1PassedBadge; // Player 1 Pas Rozeti / Yazısı
        public GameObject p2PassedBadge; // Player 2 Pas Rozeti / Yazısı
        public TextMeshProUGUI turnNotificationText;

        void Awake()
        {

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

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

            string localPlayerId = Core.GameManager.Instance.LocalPlayerId;
            bool isLocalP1 = (localPlayerId == state.player1Id);
            int myTotal = isLocalP1 ? state.p1TotalStrength : state.p2TotalStrength;
            int oppTotal = isLocalP1 ? state.p2TotalStrength : state.p1TotalStrength;

            // Alt taraf (Yerel Oyuncu) Verileri
            List<string> myMelee = isLocalP1 ? state.p1Melee : state.p2Melee;
            List<string> myRanged = isLocalP1 ? state.p1Ranged : state.p2Ranged;
            List<string> mySiege = isLocalP1 ? state.p1Siege : state.p2Siege;
            int myTotalStrength = isLocalP1 ? state.p1TotalStrength : state.p2TotalStrength;

            // Üst taraf (Rakip Oyuncu) Verileri
            List<string> oppMelee = isLocalP1 ? state.p2Melee : state.p1Melee;
            List<string> oppRanged = isLocalP1 ? state.p2Ranged : state.p1Ranged;
            List<string> oppSiege = isLocalP1 ? state.p2Siege : state.p1Siege;
            int oppTotalStrength = isLocalP1 ? state.p2TotalStrength : state.p1TotalStrength;

            p1ScoreText.text = $"Sen: {myTotalStrength}";
            p2ScoreText.text = $"Rakip: {oppTotalStrength}";

            if (roundInfoText != null)
            {
                roundInfoText.text = $"Round {state.currentRound}  |  Sen: {GetLocalLives(state)} can  Rakip: {GetOpponentLives(state)} can";
            }

            if (state.currentRound > _lastRound)
            {
                _lastRound = state.currentRound;

                // --- 2, 3, 4 & 5. ADIMLAR: Kartları Mezarlığa Uçur ---
                AnimateBoardToGraveyard();
            }

            // --- PAS DURUMU GERİ BİLDİRİMİ (FEEDBACK) ---
            bool localPassed = isLocalP1 ? state.p1Passed : state.p2Passed;
            bool opponentPassed = isLocalP1 ? state.p2Passed : state.p1Passed;

            // Pas Rozetlerini Aktif/Pasif Yap
            if (p1PassedBadge != null) p1PassedBadge.SetActive(localPassed);
            if (p2PassedBadge != null) p2PassedBadge.SetActive(opponentPassed);

            // Bildirim Metni Güncelleme
            if (turnNotificationText != null)
            {
                if (opponentPassed && !localPassed)
                {
                    turnNotificationText.text = "Rakip Pas Geçti! Raund bitene kadar hamle sırası sizde.";
                    turnNotificationText.color = Color.yellow;
                }
                else if (localPassed && !opponentPassed)
                {
                    turnNotificationText.text = "Pas geçtiniz. Rakibin hamle yapması bekleniyor...";
                    turnNotificationText.color = Color.gray;
                }
                else if (localPassed && opponentPassed)
                {
                    turnNotificationText.text = "İki taraf da pas geçti. Raund sonlandırılıyor...";
                    turnNotificationText.color = Color.red;
                }
                else
                {
                    // İki taraf da henüz pas geçmediyse sıra kimde?
                    bool isMyTurn = state.currentTurnPlayerId == Core.GameManager.Instance.LocalPlayerId;
                    turnNotificationText.text = isMyTurn ? "Sizin Sıranız" : "Rakibin Sırası";
                    turnNotificationText.color = isMyTurn ? Color.green : Color.white;
                }
            }

            UpdateRowUI(p1MeleeContainer, myMelee, p1GraveyardTransform);
            UpdateRowUI(p1RangedContainer, myRanged, p1GraveyardTransform);
            UpdateRowUI(p1SiegeContainer, mySiege, p1GraveyardTransform);

            UpdateRowUI(p2MeleeContainer, oppMelee, p2GraveyardTransform);
            UpdateRowUI(p2RangedContainer, oppRanged, p2GraveyardTransform);
            UpdateRowUI(p2SiegeContainer, oppSiege, p2GraveyardTransform);

            UpdateLocalHand(state);
            UpdateLeaderUI(state);
            UpdateDeckCount(state);
        }

        [Header("Animation")]
        public RectTransform animationLayer; // Canvas altında, tam ekran kaplayan boş obje

        private void AnimateBoardToGraveyard()
        {
            List<Transform> p1Cards = GetCardsFromContainers(p1MeleeContainer, p1RangedContainer, p1SiegeContainer);
            List<Transform> p2Cards = GetCardsFromContainers(p2MeleeContainer, p2RangedContainer, p2SiegeContainer);

            Transform flightParent = animationLayer != null ? animationLayer : transform;
            if (animationLayer == null)
                Debug.LogWarning("UIManager: 'Animation Layer' atanmamış! Kartlar Canvas dışına çıkıp görünmez olabilir.");

            foreach (var cardTransform in p1Cards)
            {
                RectTransform rect = cardTransform.GetComponent<RectTransform>();
                if (rect != null && p1GraveyardTransform != null)
                {
                    cardTransform.SetParent(flightParent, true); // artık Canvas altında
                    CardAnimationManager.Instance.AnimateToGraveyard(rect, p1GraveyardTransform);
                }
                else if (p1GraveyardTransform == null)
                {
                    Debug.LogWarning("UIManager: 'P1 Graveyard Transform' atanmamış, kart animasyonsuz siliniyor.");
                    Destroy(cardTransform.gameObject);
                }
            }

            foreach (var cardTransform in p2Cards)
            {
                RectTransform rect = cardTransform.GetComponent<RectTransform>();
                if (rect != null && p2GraveyardTransform != null)
                {
                    cardTransform.SetParent(flightParent, true);
                    CardAnimationManager.Instance.AnimateToGraveyard(rect, p2GraveyardTransform);
                }
                else if (p2GraveyardTransform == null)
                {
                    Debug.LogWarning("UIManager: 'P2 Graveyard Transform' atanmamış, kart animasyonsuz siliniyor.");
                    Destroy(cardTransform.gameObject);
                }
            }
        }

        /// <summary>
        /// Verilen sıralardaki (Container) aktif Child kart objelerini liste olarak getirir.
        /// </summary>
        private List<Transform> GetCardsFromContainers(params Transform[] containers)
        {
            List<Transform> cards = new List<Transform>();
            foreach (var container in containers)
            {
                if (container == null) continue;
                foreach (Transform child in container)
                {
                    cards.Add(child);
                }
            }
            return cards;
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

        private void UpdateRowUI(Transform container, List<string> containerCardIds, RectTransform graveyardTarget)
        {
            // Konteynırdaki mevcut tüm CardView nesnelerini topla
            List<CardView> existingViews = new List<CardView>();
            foreach (Transform child in container)
            {
                var cv = child.GetComponent<CardView>();
                if (cv != null)
                    existingViews.Add(cv);
            }

            // Sahada olup da yeni veride (containerCardIds) olmayan kartlar tespit et (Scorch vb.)
            List<string> remainingTargetIds = new List<string>(containerCardIds);
            List<CardView> viewsToRemove = new List<CardView>();
            List<CardView> viewsToKeep = new List<CardView>();

            foreach (var cv in existingViews)
            {
                if (remainingTargetIds.Contains(cv.CardId))
                {
                    // Kart hala sırada duruyor
                    viewsToKeep.Add(cv);
                    remainingTargetIds.Remove(cv.CardId);
                }
                else
                {
                    // Kart sahadan silinmiş! (Scorch / Yakma veya Özel Yetenek ile imha)
                    viewsToRemove.Add(cv);
                }
            }

            // Silinen kart(lar)ı Mezarlığa Uçur (AnimateToGraveyard)
            foreach (var removedView in viewsToRemove)
            {
                if (removedView != null)
                {
                    RectTransform rect = removedView.GetComponent<RectTransform>();
                    if (rect != null && graveyardTarget != null)
                    {
                        CardAnimationManager.Instance.PlayVanish(rect);
                    }
                    else
                    {
                        Destroy(removedView.gameObject);
                    }
                }
            }

            // Kalan kartları güncelle ve eksik olanları (yeni oynanan / Muster ile gelen) sahaya oluştur
            for (int i = 0; i < containerCardIds.Count; i++)
            {
                string id = containerCardIds[i];
                var cardData = CardManager.Instance.GetCardById(id);
                if (cardData == null) { Debug.LogWarning($"Card id bulunamadı: {id}"); continue; }

                if (i < viewsToKeep.Count)
                {
                    // Zaten var olan kartın görselini ve sırasını koru
                    viewsToKeep[i].transform.SetSiblingIndex(i);
                    viewsToKeep[i].Setup(cardData);
                }
                else
                {
                    // Yeni eklenen kart (P2 hamlesi veya Muster ile gelenler)
                    GameObject cardObj = Instantiate(cardPrefab, container);
                    cardObj.transform.SetSiblingIndex(i);
                    var newCardView = cardObj.GetComponent<CardView>();
                    if (newCardView != null)
                    {
                        newCardView.Setup(cardData);
                    }
                }
            }
        }

        public void UpdateHand(List<string> handCardIds)
        {
            var existingViews = new Dictionary<string, CardView>();
            foreach (Transform child in handContainer)
            {
                var cv = child.GetComponent<CardView>();
                if (cv != null && !string.IsNullOrEmpty(cv.CardId))
                    existingViews[cv.CardId] = cv;
            }

            var newIdSet = new HashSet<string>(handCardIds);

            foreach (var kvp in existingViews)
                if (!newIdSet.Contains(kvp.Key))
                    Destroy(kvp.Value.gameObject);

            for (int i = 0; i < handCardIds.Count; i++)
            {
                string id = handCardIds[i];
                var cardData = CardManager.Instance.GetCardById(id);
                if (cardData == null) continue;

                if (existingViews.TryGetValue(id, out var view))
                {
                    view.transform.SetSiblingIndex(i);
                    view.Setup(cardData);
                    continue; 
                }

                GameObject cardObj = Instantiate(cardPrefab, handContainer);
                cardObj.transform.SetSiblingIndex(i);
                var newCardView = cardObj.GetComponent<CardView>();
                if (newCardView != null) newCardView.Setup(cardData);

                Button btn = cardObj.GetComponent<Button>();
                if (btn != null)
                {
                    string capturedId = id;
                    CardView capturedView = newCardView;
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

        public void ResetRoundTracking()
        {
            _lastRound = 1;
        }

        public async void OnRowClicked(string rowType)
        {
            if (string.IsNullOrEmpty(_selectedCardId)) return;

            if (!Core.GameManager.Instance.CanPlayCard())
            {
                ShowFeedback("Sıra sende değil, kart oynayamazsın.");
                return;
            }

            // kart sadece kendi tanımlı satırında (row) oynanabilir.
            var cardData = CardManager.Instance.GetCardById(_selectedCardId);
            if (cardData == null)
            {
                ShowFeedback("Kart verisi bulunamadı!");
                return;
            }


            bool isWeatherCard = cardData.Type == CardType.Weather || 
                                cardData.ability == "Weather" || 
                                cardData.ability == "ClearWeather" || 
                                cardData.ability == "WeatherClear";
            bool isValidRow = false;

            if (isWeatherCard)
            {
                // Hava ve Temiz Hava kartları birim sırasına girmez, Weather efekti olarak işlenir
                isValidRow = true;
                rowType = "Weather"; 
            }
            else if (cardData.row == "Agile")
            {
                isValidRow = (rowType == "Melee" || rowType == "Close Combat" || rowType == "Ranged");
            }
            else if (cardData.Type == CardType.Weather || cardData.Type == CardType.Special || cardData.row == "Any")
            {
                isValidRow = true;
            }
            // Standart Birlik Kartları: Kartın kendi tanımında yazan sıraya denk gelmeli
            else
            {
                isValidRow = (cardData.row == rowType || 
                            (cardData.row == "Close Combat" && rowType == "Melee") ||
                            (cardData.row == "Melee" && rowType == "Close Combat"));
            }

            if (!isValidRow)
            {
                if (cardData.row == "Agile")
                {
                    ShowFeedback($"'{cardData.name}' (Agile) sadece Yakın Dövüş veya Menzilli sırasına oynanabilir!");
                }
                else
                {
                    ShowFeedback($"'{cardData.name}' sadece {cardData.row} sırasına oynanabilir.");
                }
                return;
            }

            bool isPlayer1 = Core.GameManager.Instance.LocalPlayerId == Core.GameManager.Instance.CurrentState.player1Id;

            bool isSpy = cardData.ability == "Spy";
            bool targetContainerIsPlayer1 = isSpy ? !isPlayer1 : isPlayer1;

            RectTransform targetContainer = GetTargetRowContainer(rowType, targetContainerIsPlayer1);

            // Oynanan ilk ana kart için görsel süzülme animasyonunu oynat
            if (_selectedCardView != null)
            {
                CardAnimationManager.Instance.PlayCardMoveAnimation(
                    _selectedCardView.GetComponent<RectTransform>(),
                    targetContainer
                );

                _selectedCardView.SetSelected(false);
                _selectedCardView = null;
            }

            string playedId = _selectedCardId;
            _selectedCardId = null;

            await FirestoreGameManager.Instance.PushMove(playedId, rowType);
        }

        private RectTransform GetTargetRowContainer(string rowType, bool targetIsLocalBoard)
        {
            // targetIsLocalBoard == true ise ALT konteynerler (p1), false ise ÜST konteynerler (p2)
            Transform melee = targetIsLocalBoard ? p1MeleeContainer : p2MeleeContainer;
            Transform ranged = targetIsLocalBoard ? p1RangedContainer : p2RangedContainer;
            Transform siege = targetIsLocalBoard ? p1SiegeContainer : p2SiegeContainer;

            switch (rowType)
            {
                case "Melee": case "Close Combat": return melee.GetComponent<RectTransform>();
                case "Ranged": return ranged.GetComponent<RectTransform>();
                case "Siege": return siege.GetComponent<RectTransform>();
                default: return melee.GetComponent<RectTransform>();
            }
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
            bool isPlayer1 = (localId == state.player1Id);

            string myLeaderId = isPlayer1 ? state.p1LeaderId : state.p2LeaderId;
            string oppLeaderId = isPlayer1 ? state.p2LeaderId : state.p1LeaderId;

            bool myAbilityUsed = isPlayer1 ? state.p1LeaderAbilityUsed : state.p2LeaderAbilityUsed;
            bool oppAbilityUsed = isPlayer1 ? state.p2LeaderAbilityUsed : state.p1LeaderAbilityUsed;

            // --- BENİM LİDERİM ---
            if (myLeaderContainer != null && !string.IsNullOrEmpty(myLeaderId))
            {
                // NOT: Her update'te Destroy etmek yerine kart zaten oluşturulmuşsa sadece durumunu güncellemek daha performanslıdır.
                foreach (Transform child in myLeaderContainer) Destroy(child.gameObject);

                var card = CardManager.Instance.GetCardById(myLeaderId);
                if (card != null)
                {
                    GameObject leaderObj = Instantiate(cardPrefab, myLeaderContainer);
                    var cardView = leaderObj.GetComponent<CardView>();
                    if (cardView != null) cardView.Setup(card);

                    Button btn = leaderObj.GetComponent<Button>();
                    if (btn != null)
                    {
                        // Lider kullanıldıysa butonu tıklanamaz (non-interactable) yap
                        btn.interactable = !myAbilityUsed;

                        btn.onClick.RemoveAllListeners(); // Çifte tıklama dinleyicilerini önle
                        btn.onClick.AddListener(() => {
                            if (!myAbilityUsed)
                            {
                                FirestoreGameManager.Instance.PushLeaderAbility();
                            }
                        });
                    }

                    // Kartın görsel olarak pasif olduğunu hissettirmek için alfa/karartma ekle
                    var canvasGroup = leaderObj.GetComponent<CanvasGroup>();
                    if (canvasGroup == null) canvasGroup = leaderObj.AddComponent<CanvasGroup>();
                    canvasGroup.alpha = myAbilityUsed ? 0.5f : 1.0f; // Kullanıldıysa şeffaflaştır
                }
            }

            // --- RAKİP LİDER ---
            if (opponentLeaderContainer != null && !string.IsNullOrEmpty(oppLeaderId))
            {
                foreach (Transform child in opponentLeaderContainer) Destroy(child.gameObject);

                var card = CardManager.Instance.GetCardById(oppLeaderId);
                if (card != null)
                {
                    GameObject leaderObj = Instantiate(cardPrefab, opponentLeaderContainer);
                    var cardView = leaderObj.GetComponent<CardView>();
                    if (cardView != null) cardView.Setup(card);

                    Button btn = leaderObj.GetComponent<Button>();
                    if (btn != null) btn.interactable = false; // Rakip lider zaten tıklanamaz olmalı

                    var canvasGroup = leaderObj.GetComponent<CanvasGroup>();
                    if (canvasGroup == null) canvasGroup = leaderObj.AddComponent<CanvasGroup>();
                    canvasGroup.alpha = oppAbilityUsed ? 0.5f : 1.0f;
                }
            }

            // --- OVERLAY GÖSTERİMLERİ ---
            if (myLeaderUsedOverlay != null)
            {
                myLeaderUsedOverlay.SetActive(myAbilityUsed);
                myLeaderUsedOverlay.transform.SetAsLastSibling(); // Overlay'in kartın önünde/üstünde kalmasını sağla
            }

            if (opponentLeaderUsedOverlay != null)
            {
                opponentLeaderUsedOverlay.SetActive(oppAbilityUsed);
                opponentLeaderUsedOverlay.transform.SetAsLastSibling();
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

        /// <summary>
        /// Yeni maça başlarken veya lobiye dönerken ekrandaki el ve tahta kartlarını sıfırlar.
        /// </summary>
        public void ClearHandAndBoardUI()
        {
            // 1. Eldeki kart objelerini temizle
            if (handContainer != null)
            {
                foreach (Transform child in handContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            // 2. Tahtadaki tüm sıraları temizle
            ClearContainer(p1MeleeContainer);
            ClearContainer(p1RangedContainer);
            ClearContainer(p1SiegeContainer);

            ClearContainer(p2MeleeContainer);
            ClearContainer(p2RangedContainer);
            ClearContainer(p2SiegeContainer);

            // 3. Seçili kart referanslarını sıfırla
            if (_selectedCardView != null)
            {
                _selectedCardView.SetSelected(false);
                _selectedCardView = null;
            }
            _selectedCardId = null;
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }
    }
}