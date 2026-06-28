using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 대상을 위아래로 부드럽게 둥둥 띄우는 연출(사인파). 로고/타이틀 등에 사용.
    /// RectTransform이면 anchoredPosition, 아니면 localPosition을 기준으로 움직인다.
    /// 기준 위치는 Awake에서 캡처 — 시작 위치를 중심으로 ±amplitude 만큼 진동.
    /// </summary>
    public class FloatBob : MonoBehaviour
    {
        [Tooltip("위아래 진폭(픽셀 또는 유닛). 작게 둥둥 띄우려면 8~15 정도.")]
        [SerializeField] private float amplitude = 12f;
        [Tooltip("흔들림 속도(클수록 빠르게).")]
        [SerializeField] private float speed = 1.5f;
        [Tooltip("시작 위상(라디안). 여러 개를 엇갈리게 띄우고 싶을 때만 사용.")]
        [SerializeField] private float phaseOffset = 0f;

        RectTransform _rt;
        Vector2 _baseAnchored;
        Vector3 _baseLocal;
        float _t;

        void Awake()
        {
            _rt = transform as RectTransform;
            if (_rt != null) _baseAnchored = _rt.anchoredPosition;
            else             _baseLocal    = transform.localPosition;
        }

        void OnEnable()  => _t = phaseOffset;

        void OnDisable()
        {
            // 꺼질 때 기준 위치로 복원(오프셋 상태로 멈추지 않게)
            if (_rt != null) _rt.anchoredPosition = _baseAnchored;
            else             transform.localPosition = _baseLocal;
        }

        void Update()
        {
            _t += Time.deltaTime * speed;
            float y = Mathf.Sin(_t) * amplitude;
            if (_rt != null) _rt.anchoredPosition = _baseAnchored + new Vector2(0f, y);
            else             transform.localPosition = _baseLocal + new Vector3(0f, y, 0f);
        }

        /// <summary>런타임에 위치를 다시 잡았을 때 기준점 갱신.</summary>
        public void Rebase()
        {
            if (_rt != null) _baseAnchored = _rt.anchoredPosition;
            else             _baseLocal    = transform.localPosition;
        }
    }
}