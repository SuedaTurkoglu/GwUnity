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
        Weather,
        Leader
    }

    [Serializable]
    public class CardData
    {
        public string id;
        public string name;
        public int strength;
        public string faction; // "Northern", "Nilfgaard", "ScoiaTael", "Monsters", "Skellige", "Neutral"
        public string row; // "Melee", "Ranged", "Siege", "Agile", "Any"
        public string ability; // "None", "Hero", "Spy", "Medic", "Muster", "TightBond", "MoraleBoost", "Scorch", "Decoy", "CommandersHorn", "ClearWeather"
        public string description;
        public string imagePath;

        // --- GWENT İÇİN EKLENEN ALANLAR ---
        
        public string cardType = "Unit";

        // Kartın Agile (Esnek) olup olmadığını kontrol eden yardımcı mülk
        public bool IsAgile => row == "Agile";

        // Oyuncunun destesine ekleyebileceği maksimum kopya sayısı (Örn: 3x Sefil Piyade)
        public int maxCopies = 1;

        public CardType Type
        {
            get
            {
                if (string.IsNullOrEmpty(cardType))
                {
                    if (id.Contains("_l") || ability == "LeaderAbility") return CardType.Leader;
                    if (ability == "Hero") return CardType.Hero;
                    if (ability == "Weather" || ability == "WeatherClear" || ability == "ClearWeather") return CardType.Weather;
                    if (ability == "Decoy" || ability == "Scorch" || (ability == "CommandersHorn" && strength == 0) || row == "Any") return CardType.Special;
                    return CardType.Unit;
                }

                if (Enum.TryParse<CardType>(cardType, true, out var result))
                    return result;

                return CardType.Unit;
            }
            set
            {
                cardType = value.ToString();
            }
        }
    }

    [Serializable]
    public class CardDatabase
    {
        public List<CardData> cards = new List<CardData>();
    }
}