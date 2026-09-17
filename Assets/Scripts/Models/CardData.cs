using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gwent.Models
{
    [Serializable]
    public class CardData
    {
        public string id;
        public string name;
        public int strength;
        public string faction; // "Northern", "Nilfgaard", "ScoiaTael", "Monsters", "Skellige", "Neutral"
        public string row; // Melee, Ranged, Siege
        public string ability; // None, BoostFaction, DrawCard, etc.
        public string description;
        public string imagePath;
    }

    [Serializable]
    public class CardDatabase
    {
        public List<CardData> cards;
    }
}
