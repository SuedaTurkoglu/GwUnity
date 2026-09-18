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
        public string row; // "Melee", "Ranged", "Siege", "Any"
        public string ability; // "None", "Hero", "Spy", "Medic", "Muster", "TightBond", "Morale", "Scorch", "Decoy", "Horn"
        public string description;
        public string imagePath;
    }

    [Serializable]
    public class CardDatabase
    {
        public List<CardData> cards;
    }
}
