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

            await SetupAndDealCards(state);

            state.status = GameStatus.Playing;

            await _matchRef.SetAsync(state);
            Debug.Log("Joined as Player 2. Custom Decks Loaded. Cards Dealt. Game Started!");
        }

        private async Task SetupAndDealCards(GameState state)
        {
            // Oyuncu 1'in destesini yükle
            var p1Profile = await UserProfileManager.Instance.LoadDeck(state.player1Id);
            List<string> p1Deck = (p1Profile.deck != null && p1Profile.deck.Count > 0)
                                  ? p1Profile.deck
                                  : GenerateRandomDeck(state.player1Faction);
            state.p1Hand = ShuffleAndPick(p1Deck, 10);

            // Oyuncu 2'nin destesini yükle
            var p2Profile = await UserProfileManager.Instance.LoadDeck(state.player2Id);
            List<string> p2Deck = (p2Profile.deck != null && p2Profile.deck.Count > 0)
                                  ? p2Profile.deck
                                  : GenerateRandomDeck(state.player2Faction);
            state.p2Hand = ShuffleAndPick(p2Deck, 10);

            Debug.Log($"Cards dealt. P1: {p1Deck.Count}, P2: {p2Deck.Count}");
        }

        private List<string> GenerateRandomDeck(string faction)
        {
            var allCards = Core.CardManager.Instance.GetAllCards();
            var pool = allCards.Where(c => c.faction == faction || c.faction == "Neutral").ToList();
            return pool.Select(c => c.id).OrderBy(x => UnityEngine.Random.value).Take(30).ToList();
        }

        private List<string> ShuffleAndPick(List<string> cards, int count)
        {
            List<string> ids = new List<string>();
            List<string> pool = new List<string>(cards);

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                ids.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return ids;
        }

        public async Task PushMove(string cardId, string rowType)
        {
            if (_matchRef == null) return;

            var cardData = Core.CardManager.Instance.GetCardById(cardId);
            if (cardData == null) return;
            if (cardData.row != "Any" && cardData.row != rowType) return;

            var snapshot = await _matchRef.GetSnapshotAsync();
            GameState state = snapshot.ConvertTo<GameState>();

            string playerId = Core.GameManager.Instance.LocalPlayerId;
            bool isPlayer1 = playerId == state.player1Id;

            Core.AbilityManager.ResolveOnPlayAbility(state, cardData, isPlayer1, rowType);

            bool isPermanentUnit = cardData.cardType == CardType.Unit || cardData.cardType == CardType.Hero;
            if (cardData.ability == "Scorch" || cardData.ability == "Medic" || cardData.ability == "Horn" || cardData.ability == "Decoy")
                isPermanentUnit = false;

            if (isPermanentUnit)
            {
                if (isPlayer1) AddCardToRow(state.p1Melee, state.p1Ranged, state.p1Siege, cardId, rowType);
                else AddCardToRow(state.p2Melee, state.p2Ranged, state.p2Siege, cardId, rowType);
            }

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

            if (isPlayer1) state.p1Passed = true; else state.p2Passed = true;

            if (!(isPlayer1 ? state.p2Passed : state.p1Passed))
                state.currentTurnPlayerId = isPlayer1 ? state.player2Id : state.player1Id;

            if (state.p1Passed && state.p2Passed) ResolveRound(state);

            await _matchRef.SetAsync(state);
        }

        private void ResolveRound(GameState state)
        {
            if (state.status == GameStatus.Finished) return;

            int p1Power = Core.AbilityManager.ComputeRowTotal(state.p1Melee, state.weatherMelee, state.p1HornMelee) +
                         Core.AbilityManager.ComputeRowTotal(state.p1Ranged, state.weatherRanged, state.p1HornRanged) +
                         Core.AbilityManager.ComputeRowTotal(state.p1Siege, state.weatherSiege, state.p1HornSiege);

            int p2Power = Core.AbilityManager.ComputeRowTotal(state.p2Melee, state.weatherMelee, state.p2HornMelee) +
                         Core.AbilityManager.ComputeRowTotal(state.p2Ranged, state.weatherRanged, state.p2HornRanged) +
                         Core.AbilityManager.ComputeRowTotal(state.p2Siege, state.weatherSiege, state.p2HornSiege);

            if (p1Power > p2Power) { state.p1RoundsWon++; state.p2Lives--; }
            else if (p2Power > p1Power) { state.p2RoundsWon++; state.p1Lives--; }
            else { state.p1Lives--; state.p2Lives--; }

            if (state.p1Lives <= 0 || state.p2Lives <= 0)
            {
                state.status = GameStatus.Finished;
                state.winnerId = state.p1Lives <= 0 ? state.player2Id : state.player1Id;
                return;
            }

            state.p1Graveyard.AddRange(state.p1Melee);
            state.p1Graveyard.AddRange(state.p1Ranged);
            state.p1Graveyard.AddRange(state.p1Siege);
            state.p2Graveyard.AddRange(state.p2Melee);
            state.p2Graveyard.AddRange(state.p2Ranged);
            state.p2Graveyard.AddRange(state.p2Siege);

            state.p1Melee.Clear(); state.p1Ranged.Clear(); state.p1Siege.Clear();
            state.p2Melee.Clear(); state.p2Ranged.Clear(); state.p2Siege.Clear();

            state.p1Passed = false; state.p2Passed = false; state.currentRound++;

            if (p1Power > p2Power) state.currentTurnPlayerId = state.player2Id;
            else state.currentTurnPlayerId = state.player1Id;
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
