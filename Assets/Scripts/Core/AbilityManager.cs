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
        public static List<int> ComputeRowPowers(List<string> cardIds, bool weatherActive, bool hornActive, GameState state = null)
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
                    // Eğer Kral Bran'in pasifi aktifse, kartlar 1'e düşmek yerine güçlerinin yarısını (yukarı yuvarlanarak) korur.
                    if (state != null && state.isBranPassiveActive)
                        power[i] = Mathf.CeilToInt(cards[i].strength / 2f);
                    else
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

            if (playedCard.Type == CardType.Leader || playedCard.ability == "LeaderAbility")
            {
                ResolveLeaderAbility(state, playedCard, isPlayer1);
                return;
            }

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
                    if (playedCard.Type == CardType.Weather || playedCard.ability == "Weather")
                    {
                        ResolveWeatherCard(state, playedCard);
                    }
                    break;
            }
        }

        /// <summary>
        /// JSON verisetindeki Lider Kartı ID'lerine göre özel Lider yeteneklerini çalıştırır.
        /// </summary>
        private static void ResolveLeaderAbility(GameState state, CardData leaderCard, bool isPlayer1)
        {
            if (leaderCard == null) return;

            switch (leaderCard.id)
            {
                // --- KUZEY KRALLIKLARI (Northern Realms) ---
                case "nr_l1": // Foltest 1: Yoğun Sis oynar
                    state.weatherRanged = true;
                    break;

                case "nr_l2": // Foltest 2: Tüm hava etkilerini temizler
                    ClearAllWeather(state);
                    break;

                case "nr_l3": // Foltest 3: Kuşatma sırasına Komutanın Borusu basar
                    SetHornFlag(state, isPlayer1, "Siege", true);
                    break;

                case "nr_l4": // Foltest 4: Rakip Kuşatma toplamı >= 10 ise en güçlüsünü yakar
                    ScorchRowIfThreshold(state, !isPlayer1, "Siege", 10);
                    break;


                // --- NILFGAARD ---
                case "nilf_l1": // Emhyr 1: Rakibin elindeki 3 karta bakar (UI/Controller tarafında yönlendirilebilir)
                    Debug.Log($"[Lider Yeteneği] {(isPlayer1 ? "Player 1" : "Player 2")} rakibin elindeki kartları inceliyor.");
                    break;

                case "nilf_l2": // Emhyr 2: Rakibin liderini iptal eder (GameLoop içinde kontrol edilir)
                    SetOpponentLeaderDisabled(state, !isPlayer1);
                    break;

                case "nilf_l3": // Emhyr 3: Rakip mezarlığından 1 kartı ele çeker
                    ReviveFromOpponentGraveyardToHand(state, isPlayer1);
                    break;

                case "nilf_l4": // Emhyr 4: Tüm hava etkilerini temizler
                    ClearAllWeather(state);
                    break;


                // --- CANAVARLAR (Monsters) ---
                case "mon_l1": // Eredin 1: Yakın Dövüş sırasına Komutanın Borusu basar
                    SetHornFlag(state, isPlayer1, "Melee", true);
                    break;

                case "mon_l2": // Eredin 2: Mezarlıktan 1 kartı ele döndürür
                    ReviveFromGraveyardToHand(state, isPlayer1);
                    break;

                case "mon_l3": // Eredin 3: Dondurucu Soğuk oynar
                    state.weatherMelee = true;
                    break;

                case "mon_l4": // Eredin 4: 2 kart atıp desteden 1 kart çeker
                    DiscardAndDraw(state, isPlayer1, 2, 1);
                    break;


                // --- SCOIA'TAEL ---
                case "sco_l1": // Francesca 1: Dondurucu Soğuk oynar
                    state.weatherMelee = true;
                    break;

                case "sco_l2": // Francesca 2: Fazladan 1 kart çeker
                    DrawCardsFromDeck(state, isPlayer1, 1);
                    break;

                case "sco_l3": // Francesca 3: Menzilli sırasına Komutanın Borusu basar
                    SetHornFlag(state, isPlayer1, "Ranged", true);
                    break;

                case "sco_l4": // Francesca 4: Rakip Yakın Dövüş toplamı >= 10 ise en güçlüsünü yakar
                    ScorchRowIfThreshold(state, !isPlayer1, "Melee", 10);
                    break;


                // --- SKELLIGE ---
                case "ske_l1": // Kral Bran: Kötü havalar yarı güç düşürür (Ayrı mantıkla veya passive flag ile işlenebilir)
                    state.isBranPassiveActive = true;
                    break;

                case "ske_l2": // Crach an Craite: İki oyuncunun mezarlığını destelerine geri karıştırır
                    ReshuffleGraveyardsToDecks(state);
                    break;

                default:
                    Debug.LogWarning($"Tanımsız Lider Kartı Yeteneği ID: {leaderCard.id}");
                    break;
            }
        }

        #region Lider Yetenek Yardımcı Metotları

        private static void ClearAllWeather(GameState state)
        {
            state.weatherMelee = false;
            state.weatherRanged = false;
            state.weatherSiege = false;
        }

        private static void SetOpponentLeaderDisabled(GameState state, bool isPlayer1Target)
        {
            if (isPlayer1Target)
                state.p1LeaderAbilityUsed = true; // Rakip Lider kullanım hakkı kilitlenir
            else
                state.p2LeaderAbilityUsed = true;
        }

        #endregion

        /// <summary>
        /// Belirtilen rakip sırada toplam güç threshold değerini (örn: 10) geçiyorsa en güçlü kartı yakar.
        /// </summary>
        private static void ScorchRowIfThreshold(GameState state, bool targetIsPlayer1, string rowType, int threshold)
        {
            var targetRow = targetIsPlayer1 ? 
                (rowType == "Melee" ? state.p1Melee : rowType == "Ranged" ? state.p1Ranged : state.p1Siege) :
                (rowType == "Melee" ? state.p2Melee : rowType == "Ranged" ? state.p2Ranged : state.p2Siege);

            bool weather = rowType == "Melee" ? state.weatherMelee : rowType == "Ranged" ? state.weatherRanged : state.weatherSiege;
            bool horn = targetIsPlayer1 ? 
                (rowType == "Melee" ? state.p1HornMelee : rowType == "Ranged" ? state.p1HornRanged : state.p1HornSiege) :
                (rowType == "Melee" ? state.p2HornMelee : rowType == "Ranged" ? state.p2HornRanged : state.p2HornSiege);

            if (ComputeRowTotal(targetRow, weather, horn) >= threshold)
            {
                var powers = ComputeRowPowers(targetRow, weather, horn);
                int maxPower = -1;
                string strongestId = null;

                for (int i = 0; i < targetRow.Count; i++)
                {
                    var card = CardManager.Instance.GetCardById(targetRow[i]);
                    if (card != null && card.ability != "Hero" && powers[i] > maxPower)
                    {
                        maxPower = powers[i];
                        strongestId = targetRow[i];
                    }
                }

                if (strongestId != null)
                {
                    targetRow.Remove(strongestId);
                    var graveyard = targetIsPlayer1 ? state.p1Graveyard : state.p2Graveyard;
                    graveyard.Add(strongestId);
                }
            }
        }

        private static void ReviveFromGraveyardToHand(GameState state, bool isPlayer1)
        {
            var graveyard = isPlayer1 ? state.p1Graveyard : state.p2Graveyard;
            var hand = isPlayer1 ? state.p1Hand : state.p2Hand;

            if (graveyard != null && graveyard.Count > 0)
            {
                string cardId = graveyard[UnityEngine.Random.Range(0, graveyard.Count)];
                graveyard.Remove(cardId);
                hand.Add(cardId);
            }
        }

        private static void ReviveFromOpponentGraveyardToHand(GameState state, bool isPlayer1)
        {
            var oppGraveyard = isPlayer1 ? state.p2Graveyard : state.p1Graveyard;
            var hand = isPlayer1 ? state.p1Hand : state.p2Hand;

            if (oppGraveyard != null && oppGraveyard.Count > 0)
            {
                string cardId = oppGraveyard[UnityEngine.Random.Range(0, oppGraveyard.Count)];
                oppGraveyard.Remove(cardId);
                hand.Add(cardId);
            }
        }

        private static void DiscardAndDraw(GameState state, bool isPlayer1, int discardCount, int drawCount)
        {
            var hand = isPlayer1 ? state.p1Hand : state.p2Hand;
            var graveyard = isPlayer1 ? state.p1Graveyard : state.p2Graveyard;

            for (int i = 0; i < discardCount; i++)
            {
                if (hand.Count > 0)
                {
                    string cardId = hand[0];
                    hand.RemoveAt(0);
                    graveyard.Add(cardId);
                }
            }

            DrawCardsFromDeck(state, isPlayer1, drawCount);
        }

        private static void ReshuffleGraveyardsToDecks(GameState state)
        {
            if (state.p1Graveyard != null)
            {
                state.p1Deck.AddRange(state.p1Graveyard);
                state.p1Graveyard.Clear();
            }
            if (state.p2Graveyard != null)
            {
                state.p2Deck.AddRange(state.p2Graveyard);
                state.p2Graveyard.Clear();
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
                .Where(c => c != null && c.ability != "Hero" && c.Type != CardType.Special)
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