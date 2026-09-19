using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gwent.Models
{
    public enum CardType
    {
        Unit,
        Hero,
        Special,
        Weather
    }

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

        // YENİ: Gwent Kuralları için tip belirleyici
        public CardType cardType = CardType.Unit;
    }

    [Serializable]
    public class CardDatabase
    {
        public List<CardData> cards;
    }
}
