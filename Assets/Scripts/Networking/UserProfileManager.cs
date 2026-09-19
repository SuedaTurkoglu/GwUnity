using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using Gwent.Models;

namespace Gwent.Networking
{
    public class UserProfileManager : MonoBehaviour
    {
        public static UserProfileManager Instance { get; private set; }
        private FirebaseFirestore _db;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeFirebase();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeFirebase()
        {
            try
            {
                _db = FirebaseFirestore.DefaultInstance;
                Debug.Log("UserProfileManager: Firebase Firestore initialized.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"UserProfileManager: Firebase initialization failed: {e.Message}");
            }
        }

        private async Task EnsureDbInitialized()
        {
            if (_db != null) return;

            Debug.Log("UserProfileManager: DB not initialized, retrying...");
            _db = FirebaseFirestore.DefaultInstance;
            await Task.Yield(); // Bir frame bekle
        }

        public async Task SaveDeck(string userId, string leaderId, List<string> deckIds)
        {
            if (string.IsNullOrEmpty(userId))
            {
                Debug.LogError("SaveDeck failed: userId is null or empty!");
                return;
            }

            await EnsureDbInitialized();

            if (_db == null)
            {
                Debug.LogError("SaveDeck failed: Firestore DB is still null!");
                return;
            }

            try
            {
                var userRef = _db.Collection("users").Document(userId);

                Dictionary<string, object> userData = new Dictionary<string, object>
                {
                    { "leaderId", leaderId },
                    { "deck", deckIds }
                };

                await userRef.SetAsync(userData, SetOptions.MergeAll);
                Debug.Log($"Deste başarıyla kaydedildi: {userId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Firestore Save Error: {e.Message}");
            }
        }

        public async Task<(string leaderId, List<string> deck)> LoadDeck(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return (null, new List<string>());

            await EnsureDbInitialized();

            if (_db == null) return (null, new List<string>());

            try
            {
                var doc = await _db.Collection("users").Document(userId).GetSnapshotAsync();
                if (doc.Exists)
                {
                    string leaderId = doc.GetValue<string>("leaderId");
                    var deckObj = doc.GetValue<List<object>>("deck");
                    List<string> deck = deckObj?.ConvertAll(x => x.ToString()) ?? new List<string>();

                    return (leaderId, deck);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Firestore Load Error: {e.Message}");
            }

            return (null, new List<string>());
        }
    }
}