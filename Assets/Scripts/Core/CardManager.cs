using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Gwent.Models;

namespace Gwent.Core
{
    public class CardManager : MonoBehaviour
    {
        public static CardManager Instance { get; private set; }

        public bool IsLoaded { get; private set; }
        public event Action OnCardsLoaded;

        private Dictionary<string, CardData> _cardCache = new Dictionary<string, CardData>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                StartCoroutine(LoadCardsCoroutine());
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator LoadCardsCoroutine()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "cards.json");

            // Android'de streamingAssetsPath zaten "jar:file://...!/assets/" formatında gelir,
            // UnityWebRequest ile doğrudan kullanılabilir. Diğer platformlarda (Editor/PC/Mac)
            // yerel dosya için "file://" öneki gerekir.
#if !UNITY_ANDROID
            if (!filePath.Contains("://"))
            {
                filePath = "file://" + filePath;
            }
#endif

            using (UnityWebRequest request = UnityWebRequest.Get(filePath))
            {
                yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                bool failed = request.result != UnityWebRequest.Result.Success;
#else
                bool failed = request.isNetworkError || request.isHttpError;
#endif
                if (failed)
                {
                    Debug.LogError($"Card database not found or couldn't be read: {request.error} (path: {filePath})");
                    yield break;
                }

                string jsonContent = request.downloadHandler.text;
                CardDatabase db = JsonUtility.FromJson<CardDatabase>(jsonContent);

                if (db == null || db.cards == null)
                {
                    Debug.LogError("cards.json parse edilemedi veya boş. JSON kök objesinin {\"cards\": [...]} formatında olduğundan emin ol.");
                    yield break;
                }

                foreach (var card in db.cards)
                {
                    _cardCache[card.id] = card;
                }

                IsLoaded = true;
                Debug.Log($"Loaded {_cardCache.Count} cards successfully.");
                OnCardsLoaded?.Invoke();
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

        // Statik: CardView ve CardDetailPopup gibi farklı yerlerden aynı mantıkla
        // sprite yüklemek için kullanılır, kod tekrarını önler.
        public static Sprite LoadCardSprite(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return null;

            string cleanPath = imagePath;
            int dot = cleanPath.LastIndexOf('.');
            if (dot >= 0) cleanPath = cleanPath.Substring(0, dot);

            Sprite sprite = Resources.Load<Sprite>(cleanPath);
            if (sprite == null)
            {
                Debug.LogWarning($"Kart görseli bulunamadı: Resources/{cleanPath} (orijinal yol: {imagePath}). " +
                                  "Dosyanın Resources klasöründe olduğundan ve Texture Type'ının 'Sprite (2D and UI)' olduğundan emin ol.");
            }
            return sprite;
        }
    }
}