using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SubwayMapZoom : MonoBehaviour,
        IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform zoomTarget;
        [Tooltip("[D2] 줌 변경 시 역 점·선 크기 고정 보정을 받을 렌더러.")]
        [SerializeField] private Game.Subway.SubwayMapRenderer mapRenderer;

        [Header("Zoom Settings")]
        [Tooltip("스크롤 한 칸당 배율 변화량 (1~20 권장). 높을수록 한 번에 크게 확대/축소됩니다.")]
        [SerializeField][Range(1, 20)] private int zoomSensitivity = 5;
        [SerializeField] private float minZoom = 0.4f;
        [SerializeField] private float maxZoom = 4f;
        [Tooltip("이 배율 이상으로 확대하면 역 동그라미·선 굵기가 화면상 더 커지지 않고 고정됩니다. " +
                 "(예: 1 = 1배율부터 고정 / 2 = 2배율까지는 같이 커지다 그 이후 고정)")]
        [SerializeField] private float sizeLockThreshold = 1f;

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
            // [D11] 재오픈 시 이전 배율 유지(리셋 안 함) + 플레이어 역을 중앙으로
            ApplyZoom(currentZoom);
            CenterOnPlayer();
        }

        /// <summary>[D11] 플레이어가 위치한 역이 뷰 중앙에 오도록 맵을 이동.</summary>
        public void CenterOnPlayer()
        {
            if (zoomTarget == null || mapRenderer == null) return;
            var core = Game.Core.GameCore.Instance;
            string id = core != null && core.Player != null ? core.Player.CurrentStationId : null;
            if (string.IsNullOrEmpty(id)) return;
            var pos = mapRenderer.GetStationUIPos(id);
            if (!pos.HasValue) return;
            zoomTarget.anchoredPosition = -pos.Value * currentZoom; // 역 → (0,0) 중앙
            ClampPosition();
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
            if (mapRenderer != null)
                mapRenderer.ApplyZoomCompensation(currentZoom, sizeLockThreshold); // [D2] 점·선 크기 고정
            ClampPosition();
        }

        // 드래그/중앙정렬 범위 = 콘텐츠 절반 크기 × 줌 + 여유(panMargin). 맵 전체를 훑고 임의 역을 중앙에 둘 수 있음.
        void ClampPosition()
        {
            if (zoomTarget == null) return;

            var size = zoomTarget.rect.size;
            float maxX = size.x * 0.5f * currentZoom + panMargin;
            float maxY = size.y * 0.5f * currentZoom + panMargin;

            var pos = zoomTarget.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, -maxX, maxX);
            pos.y = Mathf.Clamp(pos.y, -maxY, maxY);
            zoomTarget.anchoredPosition = pos;
        }
    }
}
