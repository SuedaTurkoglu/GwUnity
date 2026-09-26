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

            p1ScoreText.text = $"P1 Total: {state.p1TotalStrength}";
            p2ScoreText.text = $"P2 Total: {state.p2TotalStrength}";

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
            bool isLocalPlayerP1 = Core.GameManager.Instance.LocalPlayerId == state.player1Id;
            bool localPassed = isLocalPlayerP1 ? state.p1Passed : state.p2Passed;
            bool opponentPassed = isLocalPlayerP1 ? state.p2Passed : state.p1Passed;

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

            UpdateRowUI(p1MeleeContainer, state.p1Melee, p1GraveyardTransform);
            UpdateRowUI(p1RangedContainer, state.p1Ranged, p1GraveyardTransform);
            UpdateRowUI(p1SiegeContainer, state.p1Siege, p1GraveyardTransform);

            UpdateRowUI(p2MeleeContainer, state.p2Melee, p2GraveyardTransform);
            UpdateRowUI(p2RangedContainer, state.p2Ranged, p2GraveyardTransform);
            UpdateRowUI(p2SiegeContainer, state.p2Siege, p2GraveyardTransform);

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

        private void UpdateRowUI(Transform container, List<string> cardIds, RectTransform graveyardTarget)
        {
            var existingViews = new Dictionary<string, CardView>();
            foreach (Transform child in container)
            {
                var cv = child.GetComponent<CardView>();
                if (cv != null && !string.IsNullOrEmpty(cv.CardId))
                    existingViews[cv.CardId] = cv;
            }

            var newIdSet = new HashSet<string>(cardIds);

            // Artık listede olmayan kartlar: anında silmek yerine mezara UÇUR
            foreach (var kvp in existingViews)
            {
                if (!newIdSet.Contains(kvp.Key))
                {
                    RectTransform rect = kvp.Value.GetComponent<RectTransform>();
                    if (rect == null) { Destroy(kvp.Value.gameObject); continue; }

                    if (graveyardTarget == null)
                    {
                        // Hedef yoksa eski davranış: doğrudan sil
                        Destroy(kvp.Value.gameObject);
                        continue;
                    }

                    Transform flightParent = animationLayer != null ? (Transform)animationLayer : transform;
                    rect.SetParent(flightParent, true); // Canvas altındaki uçuş katmanına al
                    CardAnimationManager.Instance.AnimateToGraveyard(rect, graveyardTarget);
                }
            }

            for (int i = 0; i < cardIds.Count; i++)
            {
                string id = cardIds[i];
                var cardData = CardManager.Instance.GetCardById(id);
                if (cardData == null) { Debug.LogWarning($"Card id bulunamadı: {id}"); continue; }

                if (existingViews.TryGetValue(id, out var view))
                {
                    view.transform.SetSiblingIndex(i);
                    view.Setup(cardData);
                }
                else
                {
                    GameObject cardObj = Instantiate(cardPrefab, container);
                    cardObj.transform.SetSiblingIndex(i);
                    var cardView = cardObj.GetComponent<CardView>();
                    if (cardView != null) cardView.Setup(cardData);
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

        public void OnRowClicked(string rowType)
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

            bool isValidRow = cardData.row == "Any" ||
                            cardData.row == "Agile" ||
                            cardData.row == rowType ||
                            cardData.Type == CardType.Special ||
                            cardData.Type == CardType.Weather;

            if (!isValidRow)
            {
                ShowFeedback($"'{cardData.name}' sadece {cardData.row} sırasına oynanabilir.");
                return; // Seçim iptal olmuyor, kullanıcı doğru sıraya tıklayabilir
            }

            FirestoreGameManager.Instance.PushMove(_selectedCardId, rowType);

            bool isPlayer1 = Core.GameManager.Instance.LocalPlayerId == Core.GameManager.Instance.CurrentState.player1Id;
            RectTransform targetContainer = GetTargetRowContainer(rowType, isPlayer1);

            CardAnimationManager.Instance.PlayCardMoveAnimation(
                _selectedCardView.GetComponent<RectTransform>(),
                targetContainer
            );

            // Kart oynandı, outline'ı kapat
            if (_selectedCardView != null)
            {
                _selectedCardView.SetSelected(false);
                _selectedCardView = null;
            }

            _selectedCardId = null;
        }

        private RectTransform GetTargetRowContainer(string rowType, bool isPlayer1)
        {
            Transform melee = isPlayer1 ? p1MeleeContainer : p2MeleeContainer;
            Transform ranged = isPlayer1 ? p1RangedContainer : p2RangedContainer;
            Transform siege = isPlayer1 ? p1SiegeContainer : p2SiegeContainer;

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
    }
}