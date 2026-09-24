using UnityEngine;
using DG.Tweening;
using System;

namespace Gwent.UI
{
    public class CardAnimationManager : MonoBehaviour
    {
        public static CardAnimationManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Kartı bir başlangıç noktasından (Örn: Deste) hedef noktaya (Örn: El veya Sıra) kavisli bir şekilde uçurur.
        /// </summary>
        public void PlayCardMoveAnimation(RectTransform cardRect, Vector3 startWorldPos, Vector3 targetWorldPos, float duration = 0.5f, Action onComplete = null)
        {
            if (cardRect == null) return;

            // Kartın başlangıç pozisyonunu ayarla
            cardRect.position = startWorldPos;
            cardRect.localScale = Vector3.zero; // Küçük başlayıp büyüyecek

            Sequence moveSeq = DOTween.Sequence();

            // 1. Ölçeklenme ve Pozisyon Yolu
            moveSeq.Join(cardRect.DOScale(Vector3.one, duration * 0.5f).SetEase(Ease.OutBack));
            moveSeq.Join(cardRect.DOMove(targetWorldPos, duration).SetEase(Ease.OutCubic));
            
            // 2. Kartın uçarken hafif dönme efekti (Gwent havası katmak için)
            moveSeq.Join(cardRect.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(-5f, 5f)), duration * 0.5f)
                   .SetLoops(2, LoopType.Yoyo));

            moveSeq.OnComplete(() =>
            {
                cardRect.rotation = Quaternion.identity; // Rotasyonu sıfırla
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Scorch (Yakma) efekti çalıştığında tüm ekranı hafifçe sarsar.
        /// </summary>
        public void PlayCameraShake(float duration = 0.4f, float strength = 8f)
        {
            if (Camera.main != null)
            {
                Camera.main.transform.DOShakePosition(duration, strength);
            }
        }
    }
}