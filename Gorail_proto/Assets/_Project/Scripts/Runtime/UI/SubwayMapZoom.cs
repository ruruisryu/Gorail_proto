using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SubwayMapZoom : MonoBehaviour,
        IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform zoomTarget;

        [Header("Zoom Settings")]
        [Tooltip("스크롤 한 칸당 배율 변화량 (1~20 권장). 높을수록 한 번에 크게 확대/축소됩니다.")]
        [SerializeField][Range(1, 20)] private int zoomSensitivity = 5;
        [SerializeField] private float minZoom = 0.4f;
        [SerializeField] private float maxZoom = 4f;

        [Header("Pan Settings")]
        [Tooltip("우클릭 드래그로 지도를 이동합니다.")]
        [SerializeField] private bool enablePan = true;
        [Tooltip("map 끝 너머로 드래그할 수 있는 여유(px). 0이면 끝까지만, 50 이하 권장.")]
        [SerializeField][Range(0f, 200f)] private float panMargin = 30f;

        private float currentZoom = 1f;
        private RectTransform _viewportRT;
        private RectTransform ViewportRT => _viewportRT != null ? _viewportRT : (_viewportRT = GetComponent<RectTransform>());

        void OnEnable()
        {
            ApplyZoom(minZoom);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (zoomTarget == null) return;

            float delta = eventData.scrollDelta.y;
            float step  = zoomSensitivity * 0.01f;
            ApplyZoom(currentZoom + delta * step);
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (!enablePan || zoomTarget == null) return;
            if (eventData.button != PointerEventData.InputButton.Right) return;

            zoomTarget.anchoredPosition += eventData.delta;
            ClampPosition();
        }

        public void OnEndDrag(PointerEventData eventData) { }

        public void ResetZoom()
        {
            if (zoomTarget != null)
                zoomTarget.anchoredPosition = Vector2.zero;
            ApplyZoom(minZoom);
        }

        void ApplyZoom(float zoom)
        {
            currentZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            if (zoomTarget != null)
                zoomTarget.localScale = Vector3.one * currentZoom;
            ClampPosition();
        }

        // 드래그 범위 = panMargin × 줌 배율 (비례 증가)
        void ClampPosition()
        {
            if (zoomTarget == null) return;

            float maxX = panMargin * currentZoom;
            float maxY = panMargin * currentZoom;

            var pos = zoomTarget.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, -maxX, maxX);
            pos.y = Mathf.Clamp(pos.y, -maxY, maxY);
            zoomTarget.anchoredPosition = pos;
        }
    }
}
