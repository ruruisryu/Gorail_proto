using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 전환 연출용 전체화면 검은 페이드 싱글톤.
    /// 인스펙터에서 전체화면을 덮는 검은 Image를 overlay 슬롯에 연결한다.
    ///
    /// 사용 예:
    /// ScreenFader.Instance.Fade(onBlack: () => LoadNextScene(), fadeIn: 0.4f, fadeOut: 0.4f);
    ///
    /// 순서: 투명 → (fadeIn초) → 완전 검은 → onBlack 콜백 → (fadeOut초) → 투명
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private Image overlay;
        [SerializeField] private float defaultFadeIn  = 0.5f;
        [SerializeField] private float defaultBlackOn  = 0.5f;
        [SerializeField] private float defaultFadeOut = 0.5f;

        private Tween _tween;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (overlay != null)
            {
                overlay.color = new Color(0f, 0f, 0f, 0f);
                overlay.raycastTarget = false;
            }
        }

        /// <summary>
        /// 페이드 인 → onBlack 콜백 → 페이드 아웃 순으로 실행한다.
        /// </summary>
        /// <param name="onBlack">화면이 완전히 검어졌을 때 호출될 콜백. null 가능.</param>
        /// <param name="fadeIn">검게 되는 데 걸리는 시간(초). 0 이하면 기본값 사용.</param>
        /// <param name="fadeOut">밝아지는 데 걸리는 시간(초). 0 이하면 기본값 사용.</param>
        /// <param name="blackOn">검은 화면을 유지하는 시간(초). 0 이하면 기본값 사용.</param>
        /// <param name="onComplete">페이드 아웃이 끝나 화면이 완전히 밝아졌을 때 호출될 콜백. null 가능.</param>
        public void Fade(Action onBlack = null, float fadeIn = -1f, float fadeOut = -1f, float blackOn = -1f, Action onComplete = null)
        {
            float tIn  = fadeIn  > 0f ? fadeIn  : defaultFadeIn;
            float tOut = fadeOut > 0f ? fadeOut : defaultFadeOut;
            float tOn  = blackOn > 0f ? blackOn : defaultBlackOn;

            _tween?.Kill();
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.raycastTarget = true;

            _tween = DOTween.Sequence()
                .Append(overlay.DOFade(1f, tIn).SetEase(Ease.Linear))
                .AppendCallback(() => onBlack?.Invoke())
                .AppendInterval(tOn)
                .Append(overlay.DOFade(0f, tOut).SetEase(Ease.Linear))
                .OnComplete(() =>
                {
                    overlay.raycastTarget = false;
                    onComplete?.Invoke();
                })
                .SetUpdate(true);
        }

        /// <summary>즉시 검게 만든다. 이후 FadeOut()으로 밝힌다.</summary>
        public void FadeInImmediate()
        {
            if (overlay == null) return;
            _tween?.Kill();
            overlay.color = Color.black;
            overlay.raycastTarget = true;
        }

        /// <summary>현재 검은 화면에서 서서히 밝힌다.</summary>
        public void FadeOut(float duration = -1f, Action onComplete = null)
        {
            if (overlay == null) { onComplete?.Invoke(); return; }

            float t = duration > 0f ? duration : defaultFadeOut;
            _tween?.Kill();
            _tween = overlay.DOFade(0f, t)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    overlay.raycastTarget = false;
                    onComplete?.Invoke();
                })
                .SetUpdate(true);
        }
    }
}
