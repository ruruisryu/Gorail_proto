using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Game.Subway;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 역 호버 시 목적지역 오른쪽에 예상 도착 시간·소요·비용 툴팁을 표시한다.
    /// panel / 텍스트는 에디터에서 직접 연결한다.
    /// </summary>
    public class RouteInfoTooltip : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private SubwayMapRenderer mapRenderer;

        [Header("UI (에디터 연결)")]
        [SerializeField] private GameObject    panel;           // 툴팁 루트 (Show/Hide 대상)
        [SerializeField] private TMP_Text      arrivalTimeTxt;  // "HH:MM"  도착 예상 시각
        [SerializeField] private TMP_Text      durationTxt;     // "+Xm"    소요 시간
        [SerializeField] private TMP_Text      costTxt;         // "XXXX₩"  예상 비용

        [Header("위치")]
        [SerializeField] private RectTransform panelRT;         // panel의 RectTransform
        [SerializeField] private Canvas        overlayCanvas;   // ScreenSpaceOverlay 캔버스
        [SerializeField] private Vector2       stationOffset    = new Vector2(28f, 0f);
        [SerializeField] private Vector2       defaultPanelSize = new Vector2(160f, 72f);
        [SerializeField] private float         longDurationExtraWidth = 30f;

        void Start()
        {
            if (mapRenderer == null)
                mapRenderer = GameCore.Instance?.MapRenderer;

            if (panel != null) panel.SetActive(false);

            StationView.StationHovered     += OnHovered;
            StationView.StationHoverExited += OnExited;
        }

        void OnDestroy()
        {
            StationView.StationHovered     -= OnHovered;
            StationView.StationHoverExited -= OnExited;
        }

        // ── 이벤트 ──────────────────────────────────────────────────────

        void OnHovered(string stationId)
        {
            var core   = GameCore.Instance;
            var player = core?.Player;

            if (player == null || string.IsNullOrEmpty(player.CurrentStationId)) { Hide(); return; }
            if (stationId == player.CurrentStationId)                             { Hide(); return; }

            var graph = core?.Graph?.Graph;
            if (graph == null) { Hide(); return; }

            var path = graph.ShortestPath(player.CurrentStationId, stationId);
            if (path == null || path.Count < 2) { Hide(); return; }

            int steps     = path.Count - 1;
            int transfers = CountTransfers(path, graph, player.CurrentLineId);

            var gts = core?.GameTime;
            var ms  = core?.Money;

            int travelMin = steps     * (gts != null ? gts.minutesPerMove  : 5)
                          + transfers * (gts != null ? gts.minutesTransfer : 5);
            int cost      = transfers * (ms  != null ? ms.transferCost     : 300);

            // 도착 예상 시각
            if (arrivalTimeTxt != null)
            {
                int arrHour = 0, arrMin = 0;
                if (gts != null)
                {
                    int total = gts.Hour * 60 + gts.Minute + travelMin;
                    arrHour = (total / 60) % 24;
                    arrMin  = total % 60;
                }
                arrivalTimeTxt.text = $"{arrHour:00}:{arrMin:00}";
            }

            // 소요 시간
            if (durationTxt != null)
                durationTxt.text = $"+{FormatDuration(travelMin)}";

            // 비용
            if (costTxt != null)
                costTxt.text = $"{cost:#,0}원";

            if (panelRT != null)
            {
                float extraW = travelMin >= 60 ? longDurationExtraWidth : 0f;
                panelRT.sizeDelta = defaultPanelSize + new Vector2(extraW, 0f);
            }

            PositionNear(stationId);
            if (panel != null) panel.SetActive(true);
        }

        void OnExited() => Hide();

        void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        // ── 계산 ────────────────────────────────────────────────────────

        static int CountTransfers(List<string> path, MapGraph graph, string startLineId)
        {
            int transfers = 0;
            string prevLine = startLineId;
            for (int i = 0; i < path.Count - 1; i++)
            {
                string line = graph.GetConnectingLineId(path[i], path[i + 1]);
                if (line != null && line != prevLine) transfers++;
                if (line != null) prevLine = line;
            }
            return transfers;
        }

        static string FormatDuration(int minutes)
        {
            if (minutes <= 0) return "0분";
            int h = minutes / 60, m = minutes % 60;
            if (h == 0) return $"{m}분";
            return m == 0 ? $"{h}시간" : $"{h}시간 {m}분";
        }

        // ── 위치 계산 ────────────────────────────────────────────────────

        void PositionNear(string stationId)
        {
            if (panelRT == null || mapRenderer == null) return;
            var mapContainer = mapRenderer.MapContainer;
            if (mapContainer == null) return;

            var localPos = mapRenderer.GetStationUIPos(stationId);
            if (!localPos.HasValue) return;

            Vector3 worldPos  = mapContainer.TransformPoint(new Vector3(localPos.Value.x, localPos.Value.y, 0f));
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);

            var canvasRT = overlayCanvas != null
                ? overlayCanvas.GetComponent<RectTransform>()
                : panelRT.root.GetComponent<RectTransform>();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenPos, null, out Vector2 canvasLocal);

            panelRT.anchoredPosition = canvasLocal + stationOffset;
        }
    }
}
