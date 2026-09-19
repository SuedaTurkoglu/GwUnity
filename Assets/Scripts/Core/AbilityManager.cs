using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Gwent.Models;

namespace Gwent.Core
{
    public static class AbilityManager
    {
        public static List<int> ComputeRowPowers(List<string> cardIds, bool weatherActive, bool hornActive)
        {
            if (cardIds == null) return new List<int>();
            
            var cards = cardIds.Select(id => CardManager.Instance.GetCardById(id)).ToList();
            int n = cards.Count;
            var power = new int[n];

            for (int i = 0; i < n; i++)
                power[i] = cards[i]?.strength ?? 0;

            if (weatherActive)
            {
                for (int i = 0; i < n; i++)
                    if (cards[i] != null && cards[i].ability != "Hero")
                        power[i] = 1;
            }

            var tightBondGroups = cards
                .Select((c, i) => new { c, i })
                .Where(x => x.c != null && x.c.ability == "TightBond")
                .GroupBy(x => x.c.name);

            foreach (var group in tightBondGroups)
            {
                int count = group.Count();
                if (count > 1)
                {
                    foreach (var item in group)
                        power[item.i] *= count;
                }
            }

            var moraleIndices = cards
                .Select((c, i) => new { c, i })
                .Where(x => x.c != null && x.c.ability == "Morale")
                .Select(x => x.i)
                .ToList();

            if (moraleIndices.Count > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (cards[i] == null || cards[i].ability == "Hero") continue;
                    int bonus = moraleIndices.Count(mi => mi != i);
                    power[i] += bonus;
                }
            }

            if (hornActive)
            {
                for (int i = 0; i < n; i++)
                    if (cards[i] != null && cards[i].ability != "Hero")
                        power[i] *= 2;
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
                    SetHornFlag(state, isPlayer1, rowType, true);
                    break;

                case "Scorch":
                    ScorchOpponentStrongest(state, isPlayer1);
                    break;

                case "Medic":
                    ReviveFromGraveyard(state, isPlayer1);
                    break;

                default:
                    break;
            }
        }

        public static void ResolveWeatherCard(GameState state, CardData weatherCard)
        {
            if (weatherCard == null) return;

            if (weatherCard.name.Contains("Dondurucu Soğuk")) state.weatherMelee = true;
            else if (weatherCard.name.Contains("Yoğun Sis")) state.weatherRanged = true;
            else if (weatherCard.name.Contains("Sağanak Yağmur")) state.weatherSiege = true;
            else if (weatherCard.name.Contains("Temiz Hava"))
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

        private static void ScorchOpponentStrongest(GameState state, bool isPlayer1)
        {
            var oppMelee = isPlayer1 ? state.p2Melee : state.p1Melee;
            var oppRanged = isPlayer1 ? state.p2Ranged : state.p1Ranged;
            var oppSiege = isPlayer1 ? state.p2Siege : state.p1Siege;
            var oppGraveyard = isPlayer1 ? state.p2Graveyard : state.p1Graveyard;

            bool oppWeatherMelee = state.weatherMelee;
            bool oppWeatherRanged = state.weatherRanged;
            bool oppWeatherSiege = state.weatherSiege;
            bool oppHornMelee = isPlayer1 ? state.p2HornMelee : state.p1HornMelee;
            bool oppHornRanged = isPlayer1 ? state.p2HornRanged : state.p1HornRanged;
            bool oppHornSiege = isPlayer1 ? state.p2HornSiege : state.p1HornSiege;

            string bestId = null;
            int bestPower = -1;
            List<string> bestRow = null;

            void Scan(List<string> row, bool weather, bool horn)
            {
                if (row == null) return;
                var powers = ComputeRowPowers(row, weather, horn);
                for (int i = 0; i < row.Count; i++)
                {
                    var card = CardManager.Instance.GetCardById(row[i]);
                    if (card == null || card.ability == "Hero") continue;
                    if (powers[i] > bestPower)
                    {
                        bestPower = powers[i];
                        bestId = row[i];
                        bestRow = row;
                    }
                }
            }

            Scan(oppMelee, oppWeatherMelee, oppHornMelee);
            Scan(oppRanged, oppWeatherRanged, oppHornRanged);
            Scan(oppSiege, oppWeatherSiege, oppHornSiege);

            if (bestId != null && bestRow != null)
            {
                bestRow.Remove(bestId);
                oppGraveyard.Add(bestId);
            }
        }

        private static void ReviveFromGraveyard(GameState state, bool isPlayer1)
        {
            var graveyard = isPlayer1 ? state.p1Graveyard : state.p2Graveyard;
            if (graveyard == null || graveyard.Count == 0) return;

            var candidates = graveyard
                .Select(id => CardManager.Instance.GetCardById(id))
                .Where(c => c != null && c.ability != "Hero" && c.cardType != CardType.Special)
                .ToList();

            if (candidates.Count == 0) return;

            var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            graveyard.Remove(chosen.id);

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
    }
}
