using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.Subway
{
    /// <summary>
    /// 역 아이콘 루트에 붙는 컴포넌트. 씬에서 직접 배치한 Image 레이어들의 색·표시를 제어한다.
    ///
    /// 레이어 구성 (씬에서 배치):
    ///   baseImage        — 흰색 기본 역 스프라이트 (항상 표시)
    ///   inactiveOverlay  — 비활성 오버레이 스프라이트 (비활성 시 활성)
    ///   lineRings        — 노선 색 링 (환승역·특별역. lineId별로 하나씩)
    ///   brushIcon        — 붓 아이콘 (지상 진입 가능 특별역 전용)
    ///   label            — 역명 라벨 (선택)
    /// </summary>
    public class StationView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        // ── 전역 이벤트 ────────────────────────────────────────────────────
        public static event System.Action<string> StationClicked;
        public static event System.Action<string> StationHovered;
        public static event System.Action         StationHoverExited;

        // ── 데이터 ─────────────────────────────────────────────────────────
        [HideInInspector] public StationData stationData;

        // ── 씬에서 배치하는 레이어들 ───────────────────────────────────────
        [Header("레이어 (씬에서 직접 배치·연결)")]
        [SerializeField] private Image inactiveOverlay;
        [SerializeField] private List<LineRing> lineRings = new List<LineRing>();
        [SerializeField] private Image brushIcon;
        [SerializeField] private TextMeshProUGUI label;

        // ── 공개 API ───────────────────────────────────────────────────────

        /// <summary>역명 라벨 텍스트를 설정한다.</summary>
        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        /// <summary>
        /// 비활성 오버레이 표시 여부.
        /// 이 역을 지나는 모든 노선이 비활성이고 현재 노선도 아닐 때 true.
        /// </summary>
        public void SetInactiveOverlayVisible(bool visible)
        {
            if (inactiveOverlay != null) inactiveOverlay.gameObject.SetActive(visible);
        }

        /// <summary>특정 노선 링의 색을 설정한다.</summary>
        public void SetLineRingColor(string lineId, Color color)
        {
            foreach (var lr in lineRings)
                if (lr.lineId == lineId && lr.ring != null)
                    lr.ring.color = color;
        }

        /// <summary>lineId → color 함수로 모든 링을 한 번에 업데이트한다.</summary>
        public void ApplyLineRingColors(System.Func<string, Color> colorForLine)
        {
            foreach (var lr in lineRings)
                if (lr.ring != null)
                    lr.ring.color = colorForLine(lr.lineId);
        }

        /// <summary>이 뷰가 가진 lineId 목록 (colorRing 기준).</summary>
        public IReadOnlyList<string> LineIds
        {
            get
            {
                var ids = new List<string>(lineRings.Count);
                foreach (var lr in lineRings) ids.Add(lr.lineId);
                return ids;
            }
        }
        

        // ── 포인터 이벤트 ──────────────────────────────────────────────────
        public void OnPointerClick(PointerEventData _)
        {
            if (stationData != null && !string.IsNullOrEmpty(stationData.stationId))
                StationClicked?.Invoke(stationData.stationId);
        }

        public void OnPointerEnter(PointerEventData _)
        {
            if (stationData != null && !string.IsNullOrEmpty(stationData.stationId))
                StationHovered?.Invoke(stationData.stationId);
        }

        public void OnPointerExit(PointerEventData _) => StationHoverExited?.Invoke();
    }

    [System.Serializable]
    public struct LineRing
    {
        public string lineId;
        public Image  ring;
    }
}
