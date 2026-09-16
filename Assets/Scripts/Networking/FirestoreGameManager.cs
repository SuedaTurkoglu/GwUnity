using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Gwent.Models;
using System.Collections.Generic;
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

        // Yeni: Maç Oluşturma Fonksiyonu
        public async Task<string> CreateMatch(string p1Id)
        {
            GameState newState = new GameState
            {
                matchId = System.Guid.NewGuid().ToString(),
                player1Id = p1Id,
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

                    // Eğer maç bekliyorsa ve biz 2. oyuncuysak, kendimizi ekleyelim
                    if (state.status == GameStatus.Waiting && state.player2Id == null)
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

            // Deal cards when the second player joins
            DealCards(state);

            state.status = GameStatus.Playing;

            await _matchRef.SetAsync(state);
            Debug.Log("Joined as Player 2. Cards Dealt. Game Started!");
        }

        private void DealCards(GameState state)
        {
            var allCards = Core.CardManager.Instance.GetAllCards();

            // Simple deal: Give 5 random cards to each player
            state.p1Hand = ShuffleAndPick(allCards, 5);
            state.p2Hand = ShuffleAndPick(allCards, 5);
        }

        private List<string> ShuffleAndPick(List<Gwent.Models.CardData> cards, int count)
        {
            List<string> ids = new List<string>();
            List<Gwent.Models.CardData> pool = new List<Gwent.Models.CardData>(cards);

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

            var snapshot = await _matchRef.GetSnapshotAsync();
            GameState state = snapshot.ConvertTo<GameState>();

            string playerId = Core.GameManager.Instance.LocalPlayerId;

            if (playerId == state.player1Id)
            {
                AddCardToRow(state.p1Melee, state.p1Ranged, state.p1Siege, cardId, rowType);
            }
            else
            {
                AddCardToRow(state.p2Melee, state.p2Ranged, state.p2Siege, cardId, rowType);
            }

            state.lastMoveCardId = cardId;
            state.lastMovePlayerId = playerId;
            state.currentTurnPlayerId = (state.currentTurnPlayerId == state.player1Id) ? state.player2Id : state.player1Id;

            await _matchRef.SetAsync(state);
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

        void OnDestroy()
        {
            _snapshotListener?.Stop();
        }
    }
}
