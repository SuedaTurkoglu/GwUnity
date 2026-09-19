using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Gwent.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gwent.Networking
{
    public class FirestoreGameManager : MonoBehaviour
    {
        public static FirestoreGameManager Instance { get; private set; }

        private FirebaseFirestore _db;
        private DocumentReference _matchRef;
        private Firebase.Firestore.ListenerRegistration _snapshotListener;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeFirebase();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private async void InitializeFirebase()
        {
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus == DependencyStatus.Available)
            {
                _db = FirebaseFirestore.DefaultInstance;
                Debug.Log("Firebase initialized successfully.");
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        }

        public async Task<string> CreateMatch(string p1Id)
        {
            GameState newState = new GameState
            {
                matchId = System.Guid.NewGuid().ToString(),
                player1Id = p1Id,
                player1Faction = Core.GameManager.Instance.LocalFaction,
                currentTurnPlayerId = p1Id,
                status = GameStatus.Waiting
            };

            _matchRef = _db.Collection("matches").Document(newState.matchId);
            await _matchRef.SetAsync(newState);

            JoinMatch(newState.matchId);
            return newState.matchId;
        }

        public void JoinMatch(string matchId)
        {
            _matchRef = _db.Collection("matches").Document(matchId);

            _snapshotListener = _matchRef.Listen(snapshot =>
            {
                if (snapshot.Exists)
                {
                    GameState state = snapshot.ConvertTo<GameState>();
                    Core.GameManager.Instance.UpdateGameState(state);

                    // ÖNEMLİ DÜZELTME: sadece gerçekten player1 OLMAYAN client
                    // kendini 2. oyuncu olarak eklemeli. Bu satır olmadan,
                    // maçı oluşturan kişi kendi listener'ında bu koşulu anında
                    // karşılayıp kendi kendine 2. oyuncu oluyordu.
                    if (state.status == GameStatus.Waiting
                        && state.player2Id == null
                        && Core.GameManager.Instance.LocalPlayerId != state.player1Id)
                    {
                        SetAsPlayer2();
                    }
                }
            });
        }

        private async void SetAsPlayer2()
        {
            var snapshot = await _matchRef.GetSnapshotAsync();
            GameState state = snapshot.ConvertTo<GameState>();

            state.player2Id = Core.GameManager.Instance.LocalPlayerId;
            state.player2Faction = Core.GameManager.Instance.LocalFaction;

            DealCards(state);

            state.status = GameStatus.Playing;

            await _matchRef.SetAsync(state);
            Debug.Log("Joined as Player 2. Cards Dealt. Game Started!");
        }

        private void DealCards(GameState state)
        {
            var allCards = Core.CardManager.Instance.GetAllCards();

            // YENİ: her oyuncunun havuzu kendi fraksiyonu + Neutral kartlardan oluşuyor
            var p1Pool = allCards.Where(c => c.faction == state.player1Faction || c.faction == "Neutral").ToList();
            var p2Pool = allCards.Where(c => c.faction == state.player2Faction || c.faction == "Neutral").ToList();

            if (p1Pool.Count == 0)
                Debug.LogWarning($"P1 için '{state.player1Faction}' fraksiyonunda hiç kart bulunamadı. cards.json'daki 'faction' değerlerini kontrol et.");
            if (p2Pool.Count == 0)
                Debug.LogWarning($"P2 için '{state.player2Faction}' fraksiyonunda hiç kart bulunamadı. cards.json'daki 'faction' değerlerini kontrol et.");

            state.p1Hand = ShuffleAndPick(p1Pool, 5);
            state.p2Hand = ShuffleAndPick(p2Pool, 5);
        }

        private List<string> ShuffleAndPick(List<CardData> cards, int count)
        {
            List<string> ids = new List<string>();
            List<CardData> pool = new List<CardData>(cards);

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                ids.Add(pool[index].id);
                pool.RemoveAt(index);
            }
            return ids;
        }

        public async Task PushMove(string cardId, string rowType)
        {
            if (_matchRef == null) return;

            // YENİ: kartın kendi satırıyla hedef satır uyuşuyor mu kontrol et
            var cardData = Core.CardManager.Instance.GetCardById(cardId);
            if (cardData == null)
            {
                Debug.LogWarning($"PushMove: kart verisi bulunamadı: {cardId}");
                return;
            }
            if (cardData.row != "Any" && cardData.row != rowType)
            {
                Debug.LogWarning($"PushMove: '{cardData.name}' {cardData.row} sırasına ait, {rowType} sırasına oynanamaz.");
                return;
            }

            var snapshot = await _matchRef.GetSnapshotAsync();
            GameState state = snapshot.ConvertTo<GameState>();

            string playerId = Core.GameManager.Instance.LocalPlayerId;
            bool isPlayer1 = playerId == state.player1Id;

            // --- YETENEK İŞLEME (Ability Processing) ---
            // Kart sahaya inmeden önce veya indiği an yeteneğini çalıştır
            Core.AbilityManager.ResolveOnPlayAbility(state, cardData, isPlayer1, rowType);

            // Kartı sahaya ekle (Sadece Unit ve Hero kartları sahada kalır)
            // Özel yetenekli kartlar (Scorch, Medic, Horn vb.) etkisini gösterip gider.
            bool isPermanentUnit = cardData.cardType == CardType.Unit || cardData.cardType == CardType.Hero;

            // Ekstra kontrol: JSON'da cardType eksik olsa bile ability'sine bakarak engelle
            if (cardData.ability == "Scorch" || cardData.ability == "Medic" || cardData.ability == "Horn" || cardData.ability == "Decoy")
            {
                isPermanentUnit = false;
            }

            if (isPermanentUnit)
            {
                if (isPlayer1)
                    AddCardToRow(state.p1Melee, state.p1Ranged, state.p1Siege, cardId, rowType);
                else
                    AddCardToRow(state.p2Melee, state.p2Ranged, state.p2Siege, cardId, rowType);
            }

            // DÜZELTME: kartı elden çıkar, yoksa aynı kart tekrar oynanabilir
            var hand = isPlayer1 ? state.p1Hand : state.p2Hand;
            hand.Remove(cardId);

            state.lastMoveCardId = cardId;
            state.lastMovePlayerId = playerId;
            state.currentTurnPlayerId = (state.currentTurnPlayerId == state.player1Id) ? state.player2Id : state.player1Id;

            await _matchRef.SetAsync(state);
        }

        public async Task PushPass()
        {
            if (_matchRef == null) return;

            var snapshot = await _matchRef.GetSnapshotAsync();
            GameState state = snapshot.ConvertTo<GameState>();

            string playerId = Core.GameManager.Instance.LocalPlayerId;
            bool isPlayer1 = playerId == state.player1Id;

            if (isPlayer1) state.p1Passed = true;
            else state.p2Passed = true;

            // Sıra: rakip pas geçmediyse ona geç, o da geçtiyse (round bitiyor) sırayı takmıyoruz
            if (!(isPlayer1 ? state.p2Passed : state.p1Passed))
            {
                state.currentTurnPlayerId = isPlayer1 ? state.player2Id : state.player1Id;
            }

            if (state.p1Passed && state.p2Passed)
            {
                ResolveRound(state);
            }

            await _matchRef.SetAsync(state);
        }

        private void ResolveRound(GameState state)
        {
            if (state.status == GameStatus.Finished) return;

            int p1Power = SumRowStrength(state.p1Melee) + SumRowStrength(state.p1Ranged) + SumRowStrength(state.p1Siege);
            int p2Power = SumRowStrength(state.p2Melee) + SumRowStrength(state.p2Ranged) + SumRowStrength(state.p2Siege);

            if (p1Power > p2Power) { state.p1RoundsWon++; state.p2Lives--; }
            else if (p2Power > p1Power) { state.p2RoundsWon++; state.p1Lives--; }
            else { state.p1Lives--; state.p2Lives--; } // berabere: ikisi de can kaybeder

            if (state.p1Lives <= 0 || state.p2Lives <= 0)
            {
                state.status = GameStatus.Finished;
                state.winnerId = state.p1Lives <= 0 ? state.player2Id : state.player1Id;
                return;
            }

            // Yeni round: sahadaki kartlar mezarlığa gider, eller korunur
            state.p1Graveyard.AddRange(state.p1Melee);
            state.p1Graveyard.AddRange(state.p1Ranged);
            state.p1Graveyard.AddRange(state.p1Siege);
            state.p2Graveyard.AddRange(state.p2Melee);
            state.p2Graveyard.AddRange(state.p2Ranged);
            state.p2Graveyard.AddRange(state.p2Siege);

            state.p1Melee.Clear(); state.p1Ranged.Clear(); state.p1Siege.Clear();
            state.p2Melee.Clear(); state.p2Ranged.Clear(); state.p2Siege.Clear();

            state.p1Passed = false;
            state.p2Passed = false;
            state.currentRound++;

            // Bir önceki round'u kaybeden oyuncu yeni round'a başlar (Gwent kuralı).
            // Berabere durumda player1 başlar (basit tutuyoruz).
            if (p1Power > p2Power) state.currentTurnPlayerId = state.player2Id;
            else state.currentTurnPlayerId = state.player1Id;
        }

        private int SumRowStrength(List<string> cardIds)
        {
            int sum = 0;
            foreach (var id in cardIds)
            {
                var card = Core.CardManager.Instance.GetCardById(id);
                if (card != null) sum += card.strength;
            }
            return sum;
        }

        private void AddCardToRow(List<string> melee, List<string> ranged, List<string> siege, string cardId, string rowType)
        {
            switch (rowType)
            {
                case "Melee": melee.Add(cardId); break;
                case "Ranged": ranged.Add(cardId); break;
                case "Siege": siege.Add(cardId); break;
            }
        }

        // YENİ: oyun bitince veya lobiye dönerken bu maçı dinlemeyi bırak
        public void LeaveMatch()
        {
            _snapshotListener?.Stop();
            _snapshotListener = null;
            _matchRef = null;
        }

        void OnDestroy()
        {
            _snapshotListener?.Stop();
        }
    }
}