using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using TMPro;

namespace Gwent.UI
{
    public class CardAnimationManager : MonoBehaviour
    {
        public static CardAnimationManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // Android için 60 FPS kilit açma
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            DOTween.SetTweensCapacity(500, 50);
        }

        /// <summary>
        /// 1. SÜZÜLEREK SAHAYA/ELE GİTME ANIMASYONU
        /// Kart başlangıç noktasından hedef sıraya yumuşak bir kavisle ve hafif eğimle süzülür.
        /// </summary>
        public void PlayCardMoveAnimation(RectTransform cardRect, RectTransform targetContainer, float duration = 0.5f, Action onComplete = null)
        {
            if (cardRect == null || targetContainer == null) return;

            Vector3 startWorldPos = cardRect.position;

            // Kartı yeni ebeveynine taşı (worldPositionStays:true -> anlık sıçrama olmaz)
            cardRect.SetParent(targetContainer, true);

            // KRİTİK: ignoreLayout HENÜZ true YAPILMADAN, rebuild'i zorla ve hedefi
            // BUNDAN SONRA oku. Aksi halde Layout Group bu kartı hesaba katmaz.
            LayoutRebuilder.ForceRebuildLayoutImmediate(targetContainer);
            Vector3 targetWorldPos = cardRect.position;

            // Şimdi görsel olarak başlangıç noktasına geri çek
            cardRect.position = startWorldPos;

            LayoutElement layoutElement = cardRect.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = cardRect.gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true; // animasyon sırasında Layout Group elini çeksin

            Sequence glideSeq = DOTween.Sequence();
            glideSeq.Join(cardRect.DOMove(targetWorldPos, duration).SetEase(Ease.OutCubic));
            glideSeq.Join(cardRect.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(-6f, 6f)), duration * 0.5f)
                .SetLoops(2, LoopType.Yoyo));

            glideSeq.OnComplete(() =>
            {
                cardRect.rotation = Quaternion.identity;
                cardRect.position = targetWorldPos; // tam oturduğundan emin ol
                layoutElement.ignoreLayout = false;
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// 2. MEZARLIĞA UÇMA ANIMASYONU (Raund Sonu veya Scorch)
        /// Kart küçülüp sağ alt/üst köşedeki mezarlığa doğru dönerek uçup yok olur.
        /// </summary>
        public void AnimateToGraveyard(RectTransform cardRect, RectTransform graveyardTransform, Action onComplete = null)
        {
            if (cardRect == null || graveyardTransform == null)
            {
                Destroy(cardRect?.gameObject);
                return;
            }

            LayoutElement layoutElement = cardRect.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = cardRect.gameObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true; // artık her zaman garanti altına alınıyor

            Sequence flySeq = DOTween.Sequence();
            flySeq.Join(cardRect.DOMove(graveyardTransform.position, 0.45f).SetEase(Ease.InQuad));
            flySeq.Join(cardRect.DOScale(Vector3.one * 0.2f, 0.45f).SetEase(Ease.InBack));
            flySeq.Join(cardRect.DORotate(new Vector3(0, 0, 180f), 0.45f, RotateMode.FastBeyond360));

            flySeq.OnComplete(() =>
            {
                onComplete?.Invoke();
                Destroy(cardRect.gameObject);
            });
        }

        /// <summary>
        /// Ekran Sarsıntısı (Yakma / Scorch vb. için)
        /// </summary>
        public void PlayCameraShake(float duration = 0.3f, float strength = 6f)
        {
            if (Camera.main != null)
            {
                Camera.main.transform.DOShakePosition(duration, strength);
            }

            #if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
            #endif
        }

        /// <summary>
        /// Pas geçildiğinde ekranda beliren dinamik pop-up uyarısı.
        /// </summary>
        public void PlayPassNotification(TextMeshProUGUI notificationText, string message)
        {
            if (notificationText == null) return;

            notificationText.text = message;
            notificationText.transform.DOKill();
            notificationText.transform.localScale = Vector3.zero;
            notificationText.gameObject.SetActive(true);

            Sequence passSeq = DOTween.Sequence();
            passSeq.Append(notificationText.transform.DOScale(Vector3.one * 1.2f, 0.25f).SetEase(Ease.OutBack));
            passSeq.Append(notificationText.transform.DOScale(Vector3.one, 0.15f));
        }
    }
}