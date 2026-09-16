using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Gwent.Models;

namespace Gwent.Core
{
    public class CardManager : MonoBehaviour
    {
        public static CardManager Instance { get; private set; }

        private Dictionary<string, CardData> _cardCache = new Dictionary<string, CardData>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadCards();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LoadCards()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "cards.json");

            // Note: On Android, StreamingAssets requires UnityWebRequest.
            // For now, we'll implement a simple read for editor/PC.
            if (File.Exists(filePath))
            {
                string jsonContent = File.ReadAllText(filePath);
                CardDatabase db = JsonUtility.FromJson<CardDatabase>(jsonContent);

                foreach (var card in db.cards)
                {
                    _cardCache[card.id] = card;
                }
                Debug.Log($"Loaded {_cardCache.Count} cards successfully.");
            }
            else
            {
                Debug.LogError($"Card database not found at {filePath}");
            }
        }

        public CardData GetCardById(string id)
        {
            if (_cardCache.TryGetValue(id, out CardData card))
            {
                return card;
            }
            return null;
        }

        public List<CardData> GetAllCards()
        {
            return new List<CardData>(_cardCache.Values);
        }
    }
}
