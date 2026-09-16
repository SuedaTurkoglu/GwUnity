using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gwent.Models
{
    [Serializable]
    public class GameState
    {
        public string matchId;
        public string player1Id;
        public string player2Id;
        public string currentTurnPlayerId;
        public GameStatus status = GameStatus.Waiting;

        // Hands
        public List<string> p1Hand = new List<string>();
        public List<string> p2Hand = new List<string>();

        // Boards: PlayerID -> RowType -> List of CardIDs
        public List<string> p1Melee = new List<string>();
        public List<string> p1Ranged = new List<string>();
        public List<string> p1Siege = new List<string>();

        public List<string> p2Melee = new List<string>();
        public List<string> p2Ranged = new List<string>();
        public List<string> p2Siege = new List<string>();

        public int p1TotalStrength;
        public int p2TotalStrength;

        public string lastMoveCardId;
        public string lastMovePlayerId;
    }

    public enum GameStatus
    {
        Waiting,
        Playing,
        Finished
    }
}
