using System.Collections;
using TMPro;
using UnityEngine;
using Game.Core;
using Game.Gameplay;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 잘못된 역 클릭 시 uGUI 토스트 알림.
    /// Scene 설정: CanvasGroup이 붙은 패널 오브젝트에 이 컴포넌트를 추가하고,
    /// 자식 TextMeshProUGUI를 messageText에 연결한다.
    /// </summary>
    public class MoveNotificationHud : MonoBehaviour
    {
        [SerializeField] private CanvasGroup          canvasGroup;
        [SerializeField] private TextMeshProUGUI       messageText;
        [SerializeField] private MoveRejectionMessages messages;
        [SerializeField] private float displaySeconds = 2.5f;
        [SerializeField] private float fadeSeconds    = 0.3f;

        private Coroutine _routine;

        void Start()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            var tr = GameCore.Instance?.TurnResolver;
            if (tr != null)
                tr.MoveRejected += OnMoveRejected;
            else
                Debug.LogWarning("[MoveNotificationHud] TurnResolver를 찾지 못했습니다.");
        }

        void OnDestroy()
        {
            var tr = GameCore.Instance?.TurnResolver;
            if (tr != null) tr.MoveRejected -= OnMoveRejected;
        }

        void OnMoveRejected(MoveRejectedReason reason)
        {
            if (messageText != null && messages != null)
                messageText.text = reason switch
                {
                    MoveRejectedReason.WrongDirection => messages.wrongDirection,
                    MoveRejectedReason.InactiveLine   => messages.inactiveLine,
                    _                                 => messages.wrongLine,
                };
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(messageText.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowThenFade());
        }

        IEnumerator ShowThenFade()
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(displaySeconds);

            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                if (canvasGroup != null) canvasGroup.alpha = 1f - t / fadeSeconds;
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }
    }
}
