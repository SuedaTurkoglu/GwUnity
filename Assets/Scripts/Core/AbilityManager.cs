using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gwent.Models;

namespace Gwent.Core
{
    public static class AbilityManager
    {
        /// <summary>
        /// Gwent Resmi Hesaplama Sırası:
        /// 1. Base / Weather (Kahramanlar hariç 1 olur)
        /// 2. TightBond (Aynı isimli kart sayısı ile çarpılır)
        /// 3. Morale Boost (+1 bonus)
        /// 4. Commander's Horn (x2 bonus)
        /// </summary>
        public static List<int> ComputeRowPowers(List<string> cardIds, bool weatherActive, bool hornActive)
        {
            if (cardIds == null) return new List<int>();

            var cards = cardIds.Select(id => CardManager.Instance.GetCardById(id)).ToList();
            int n = cards.Count;
            var power = new int[n];

            // 1. ADIM: Taban Güç & Weather (Hava Efekti)
            for (int i = 0; i < n; i++)
            {
                if (cards[i] == null) continue;

                if (weatherActive && cards[i].ability != "Hero")
                    power[i] = 1;
                else
                    power[i] = cards[i].strength;
            }

            // 2. ADIM: TightBond (Sıkı Bağ)
            var tightBondGroups = cards
                .Select((c, i) => new { c, i })
                .Where(x => x.c != null && x.c.ability == "TightBond")
                .GroupBy(x => x.c.name);

            foreach (var group in tightBondGroups)
            {
                int count = group.Count();
                foreach (var item in group)
                {
                    power[item.i] *= count; // Kaç adet varsa o kadarla çarpılır (2 tane ise x2, 3 tane ise x3)
                }
            }

            // 3. ADIM: Morale Boost (Moral Artışı)
            var moraleIndices = cards
                .Select((c, i) => new { c, i })
                .Where(x => x.c != null && x.c.ability == "MoraleBoost" || x.c?.ability == "Morale")
                .Select(x => x.i)
                .ToList();

            if (moraleIndices.Count > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (cards[i] == null || cards[i].ability == "Hero") continue;
                    // Kendisi hariç sahada kaç moral kartı varsa o kadar +1 alır
                    int bonus = moraleIndices.Count(mi => mi != i);
                    power[i] += bonus;
                }
            }

            // 4. ADIM: Commander's Horn (Komutan Borusu)
            // Hem sıradaki Horn efekti hem de Dandelion gibi kart bazlı Horn kontrol edilir
            for (int i = 0; i < n; i++)
            {
                if (cards[i] == null || cards[i].ability == "Hero") continue;

                bool cardIsHorn = cards[i].ability == "CommandersHorn" || cards[i].ability == "Horn";
                if (hornActive || cardIsHorn)
                {
                    power[i] *= 2;
                }
            }

            return power.ToList();
        }

        public static int ComputeRowTotal(List<string> cardIds, bool weatherActive, bool hornActive)
        {
            return ComputeRowPowers(cardIds, weatherActive, hornActive).Sum();
        }

        public static void ResolveOnPlayAbility(GameState state, CardData playedCard, bool isPlayer1, string rowType)
        {
            if (playedCard == null) return;

            switch (playedCard.ability)
            {
                case "Horn":
                case "CommandersHorn":
                    SetHornFlag(state, isPlayer1, rowType, true);
                    break;

                case "Scorch":
                    // Genel Yakma: Tüm sahadaki en güçlü kart(ları) siler
                    ScorchGlobalStrongest(state);
                    break;

                case "Medic":
                    // İstek üzerine: Desteden random kart çekip sahaya koyar
                    PlayRandomFromDeck(state, isPlayer1);
                    break;

                case "Decoy":
                    // İstek üzerine: Sahadan random kartı ele geri döndürür
                    ReturnRandomCardToHand(state, isPlayer1);
                    break;

                case "Spy":
                    // Ajan: Desteden 2 kart çektirir (Ajan kartının rakip sahaya konması GameLoop/Controller'da işlenmelidir)
                    DrawCardsFromDeck(state, isPlayer1, 2);
                    break;

                case "Muster":
                    // Sürü: Eldeki ve destedeki aynı isimli kartları sahaya çeker
                    MusterSameCards(state, playedCard, isPlayer1);
                    break;

                case "ClearWeather":
                case "WeatherClear":
                    ResolveWeatherCard(state, playedCard);
                    break;

                default:
                    if (playedCard.cardType == CardType.Weather || playedCard.ability == "Weather")
                    {
                        ResolveWeatherCard(state, playedCard);
                    }
                    break;
            }
        }

        public static void ResolveWeatherCard(GameState state, CardData weatherCard)
        {
            if (weatherCard == null) return;

            if (weatherCard.name.Contains("Dondurucu Soğuk") || weatherCard.id.Contains("frost")) state.weatherMelee = true;
            else if (weatherCard.name.Contains("Yoğun Sis") || weatherCard.id.Contains("fog")) state.weatherRanged = true;
            else if (weatherCard.name.Contains("Sağanak Yağmur") || weatherCard.id.Contains("rain")) state.weatherSiege = true;
            else if (weatherCard.name.Contains("Temiz Hava") || weatherCard.ability == "ClearWeather" || weatherCard.ability == "WeatherClear")
            {
                state.weatherMelee = false;
                state.weatherRanged = false;
                state.weatherSiege = false;
            }
        }

        public static void ClearRoundEffects(GameState state)
        {
            state.weatherMelee = false;
            state.weatherRanged = false;
            state.weatherSiege = false;
            state.p1HornMelee = false;
            state.p1HornRanged = false;
            state.p1HornSiege = false;
            state.p2HornMelee = false;
            state.p2HornRanged = false;
            state.p2HornSiege = false;
        }

        private static void SetHornFlag(GameState state, bool isPlayer1, string row, bool value)
        {
            if (isPlayer1)
            {
                if (row == "Melee") state.p1HornMelee = value;
                else if (row == "Ranged") state.p1HornRanged = value;
                else if (row == "Siege") state.p1HornSiege = value;
            }
            else
            {
                if (row == "Melee") state.p2HornMelee = value;
                else if (row == "Ranged") state.p2HornRanged = value;
                else if (row == "Siege") state.p2HornSiege = value;
            }
        }

        /// <summary>
        /// Orijinal Scorch (Yakma) Kuralı: Sahadaki (P1 + P2) En Yüksek Güce Sahip (Hero Olmayan) Kart(ları) Yakar.
        /// </summary>
        private static void ScorchGlobalStrongest(GameState state)
        {
            List<(List<string> row, string id, int power, List<string> graveyard)> allCards = new List<(List<string>, string, int, List<string>)>();

            void CollectRow(List<string> row, bool weather, bool horn, List<string> graveyard)
            {
                if (row == null) return;
                var powers = ComputeRowPowers(row, weather, horn);
                for (int i = 0; i < row.Count; i++)
                {
                    var card = CardManager.Instance.GetCardById(row[i]);
                    if (card != null && card.ability != "Hero")
                    {
                        allCards.Add((row, row[i], powers[i], graveyard));
                    }
                }
            }

            CollectRow(state.p1Melee, state.weatherMelee, state.p1HornMelee, state.p1Graveyard);
            CollectRow(state.p1Ranged, state.weatherRanged, state.p1HornRanged, state.p1Graveyard);
            CollectRow(state.p1Siege, state.weatherSiege, state.p1HornSiege, state.p1Graveyard);

            CollectRow(state.p2Melee, state.weatherMelee, state.p2HornMelee, state.p2Graveyard);
            CollectRow(state.p2Ranged, state.weatherRanged, state.p2HornRanged, state.p2Graveyard);
            CollectRow(state.p2Siege, state.weatherSiege, state.p2HornSiege, state.p2Graveyard);

            if (allCards.Count == 0) return;

            int maxPower = allCards.Max(x => x.power);
            var toDestroy = allCards.Where(x => x.power == maxPower).ToList();

            foreach (var item in toDestroy)
            {
                item.row.Remove(item.id);
                item.graveyard.Add(item.id);
            }
        }

        /// <summary>
        /// Medic Yeteneği (Geçici): Desteden rastgele bir birim kartını çekip sahaya yerleştirir.
        /// </summary>
        private static void PlayRandomFromDeck(GameState state, bool isPlayer1)
        {
            var deck = isPlayer1 ? state.p1Deck : state.p2Deck;
            if (deck == null || deck.Count == 0) return;

            var validCards = deck
                .Select(id => CardManager.Instance.GetCardById(id))
                .Where(c => c != null && c.ability != "Hero" && c.cardType != CardType.Special)
                .ToList();

            if (validCards.Count == 0) return;

            var chosen = validCards[Random.Range(0, validCards.Count)];
            deck.Remove(chosen.id);

            var melee = isPlayer1 ? state.p1Melee : state.p2Melee;
            var ranged = isPlayer1 ? state.p1Ranged : state.p2Ranged;
            var siege = isPlayer1 ? state.p1Siege : state.p2Siege;

            switch (chosen.row)
            {
                case "Melee": melee.Add(chosen.id); break;
                case "Ranged": ranged.Add(chosen.id); break;
                case "Siege": siege.Add(chosen.id); break;
                default: melee.Add(chosen.id); break;
            }
        }

        /// <summary>
        /// Decoy Yeteneği (Geçici): Sahadaki Hero olmayan rastgele bir kartı ele geri döndürür.
        /// </summary>
        private static void ReturnRandomCardToHand(GameState state, bool isPlayer1)
        {
            var melee = isPlayer1 ? state.p1Melee : state.p2Melee;
            var ranged = isPlayer1 ? state.p1Ranged : state.p2Ranged;
            var siege = isPlayer1 ? state.p1Siege : state.p2Siege;
            var hand = isPlayer1 ? state.p1Hand : state.p2Hand;

            List<(List<string> row, string id)> boardCards = new List<(List<string>, string)>();

            void AddRow(List<string> row)
            {
                if (row == null) return;
                foreach (var id in row)
                {
                    var card = CardManager.Instance.GetCardById(id);
                    if (card != null && card.ability != "Hero" && card.ability != "Decoy")
                        boardCards.Add((row, id));
                }
            }

            AddRow(melee);
            AddRow(ranged);
            AddRow(siege);

            if (boardCards.Count == 0) return;

            var selected = boardCards[Random.Range(0, boardCards.Count)];
            selected.row.Remove(selected.id);
            hand.Add(selected.id);
        }

        /// <summary>
        /// Spy Mekaniği: Desteden belirtilen sayıda kart çeker.
        /// </summary>
        private static void DrawCardsFromDeck(GameState state, bool isPlayer1, int count)
        {
            var deck = isPlayer1 ? state.p1Deck : state.p2Deck;
            var hand = isPlayer1 ? state.p1Hand : state.p2Hand;

            for (int i = 0; i < count; i++)
            {
                if (deck != null && deck.Count > 0)
                {
                    string cardId = deck[0];
                    deck.RemoveAt(0);
                    hand.Add(cardId);
                }
            }
        }

        /// <summary>
        /// Muster Mekaniği: Elde ve destede aynı isme sahip tüm kartları bulup sahaya dizer.
        /// </summary>
        private static void MusterSameCards(GameState state, CardData playedCard, bool isPlayer1)
        {
            var deck = isPlayer1 ? state.p1Deck : state.p2Deck;
            var hand = isPlayer1 ? state.p1Hand : state.p2Hand;
            var targetRow = isPlayer1 ? 
                (playedCard.row == "Melee" ? state.p1Melee : playedCard.row == "Ranged" ? state.p1Ranged : state.p1Siege) :
                (playedCard.row == "Melee" ? state.p2Melee : playedCard.row == "Ranged" ? state.p2Ranged : state.p2Siege);

            // Destedekileri çağır
            var fromDeck = deck.Where(id => CardManager.Instance.GetCardById(id)?.name == playedCard.name).ToList();
            foreach (var id in fromDeck)
            {
                deck.Remove(id);
                targetRow.Add(id);
            }

            // Eldekileri çağır
            var fromHand = hand.Where(id => CardManager.Instance.GetCardById(id)?.name == playedCard.name).ToList();
            foreach (var id in fromHand)
            {
                hand.Remove(id);
                targetRow.Add(id);
            }
        }
    }
}