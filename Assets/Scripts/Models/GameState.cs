using System.Collections.Generic;
using Firebase.Firestore;

namespace Gwent.Models
{
    // [FirestoreData] + [FirestoreProperty]: Firebase Firestore SDK'nın bu sınıfı
    // otomatik olarak Firestore dökümanına yazıp okuyabilmesi (SetAsync / ConvertTo<GameState>)
    // için gereklidir. Bunlar olmadan (de)serialization çalışma zamanında sorun çıkarabilir.
    [FirestoreData]
    public class GameState
    {
        [FirestoreProperty] public string matchId { get; set; }
        [FirestoreProperty] public string player1Id { get; set; }
        [FirestoreProperty] public string player2Id { get; set; }
        [FirestoreProperty] public string player1Faction { get; set; } // "Northern", "Nilfgaard", "ScoiaTael", "Monsters", "Skellige"
        [FirestoreProperty] public string player2Faction { get; set; }
        [FirestoreProperty] public string currentTurnPlayerId { get; set; }
        [FirestoreProperty] public GameStatus status { get; set; } = GameStatus.Waiting;

        // Eller
        [FirestoreProperty] public List<string> p1Hand { get; set; } = new List<string>();
        [FirestoreProperty] public List<string> p2Hand { get; set; } = new List<string>();

        // Sahalar
        [FirestoreProperty] public List<string> p1Melee { get; set; } = new List<string>();
        [FirestoreProperty] public List<string> p1Ranged { get; set; } = new List<string>();
        [FirestoreProperty] public List<string> p1Siege { get; set; } = new List<string>();

        [FirestoreProperty] public List<string> p2Melee { get; set; } = new List<string>();
        [FirestoreProperty] public List<string> p2Ranged { get; set; } = new List<string>();
        [FirestoreProperty] public List<string> p2Siege { get; set; } = new List<string>();

        // Mezarlık (round sonunda sahadaki kartlar buraya taşınır; ileride Medic yeteneği için lazım)
        [FirestoreProperty] public List<string> p1Graveyard { get; set; } = new List<string>();
        [FirestoreProperty] public List<string> p2Graveyard { get; set; } = new List<string>();

        [FirestoreProperty] public int p1TotalStrength { get; set; }
        [FirestoreProperty] public int p2TotalStrength { get; set; }

        // Pas durumu (round bitişini belirlemek için)
        [FirestoreProperty] public bool p1Passed { get; set; }
        [FirestoreProperty] public bool p2Passed { get; set; }

        // Can / round takibi (Gwent: 2 round kaybedince oyunu kaybedersin)
        [FirestoreProperty] public int p1Lives { get; set; } = 2;
        [FirestoreProperty] public int p2Lives { get; set; } = 2;
        [FirestoreProperty] public int p1RoundsWon { get; set; }
        [FirestoreProperty] public int p2RoundsWon { get; set; }
        [FirestoreProperty] public int currentRound { get; set; } = 1;

        [FirestoreProperty] public string lastMoveCardId { get; set; }
        [FirestoreProperty] public string lastMovePlayerId { get; set; }

        // Maç bitince kazananın id'si buraya yazılır
        [FirestoreProperty] public string winnerId { get; set; }

        // YENİ: Gelişmiş Yetenek Takibi (AbilityManager için)
        [FirestoreProperty] public bool weatherMelee { get; set; } = false;
        [FirestoreProperty] public bool weatherRanged { get; set; } = false;
        [FirestoreProperty] public bool weatherSiege { get; set; } = false;

        [FirestoreProperty] public bool p1HornMelee { get; set; } = false;
        [FirestoreProperty] public bool p1HornRanged { get; set; } = false;
        [FirestoreProperty] public bool p1HornSiege { get; set; } = false;

        [FirestoreProperty] public bool p2HornMelee { get; set; } = false;
        [FirestoreProperty] public bool p2HornRanged { get; set; } = false;
        [FirestoreProperty] public bool p2HornSiege { get; set; } = false;

        [FirestoreProperty] public bool p1LeaderAbilityUsed { get; set; } = false;
        [FirestoreProperty] public bool p2LeaderAbilityUsed { get; set; } = false;

        // Lider ve Deste Takibi (YENİ)
        [FirestoreProperty] public string p1LeaderId { get; set; }
        [FirestoreProperty] public string p2LeaderId { get; set; }
        [FirestoreProperty] public List<string> p1Deck { get; set; } = new List<string>();
        [FirestoreProperty] public List<string> p2Deck { get; set; } = new List<string>();
    }

    public enum GameStatus
    {
        Waiting,
        Playing,
        Finished
    }
}
