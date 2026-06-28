using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Subway;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 역 호버 시 목적지역 오른쪽에 예상 소요 시간·비용 툴팁을 표시한다.
    /// SubwayMapRenderer가 있는 씬 오브젝트에 같이 붙이거나 별도 오브젝트에 배치.
    /// </summary>
    public class RouteInfoTooltip : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private SubwayMapRenderer mapRenderer;

        [Header("툴팁 스타일")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector2  tooltipSize    = new Vector2(160f, 72f);
        [SerializeField] private Vector2  stationOffset  = new Vector2(28f, 0f);
        [SerializeField] private Color    bgColor        = new Color(0.08f, 0.08f, 0.08f, 0.88f);
        [SerializeField] private Color    textColor      = Color.white;
        [SerializeField] private Color    subTextColor   = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private float    fontSize       = 14f;
        [SerializeField] private float    subFontSize    = 12f;
        [SerializeField] private int      sortOrder      = 14;

        Canvas        _canvas;
        RectTransform _panel;
        TMP_Text      _timeLabel;
        TMP_Text      _transferLabel;
        TMP_Text      _costLabel;

        void Awake()
        {
            BuildUI();
        }

        void Start()
        {
            if (mapRenderer == null)
                mapRenderer = GameCore.Instance?.MapRenderer;

            StationView.StationHovered    += OnHovered;
            StationView.StationHoverExited += OnExited;
        }

        void OnDestroy()
        {
            StationView.StationHovered    -= OnHovered;
            StationView.StationHoverExited -= OnExited;
        }

        // ── UI 생성 ──────────────────────────────────────────────────────

        void BuildUI()
        {
            var canvasGO = new GameObject("[RouteInfoTooltip]");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortOrder;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // 패널 배경
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);

            var bg  = panelGO.AddComponent<Image>();
            bg.color = bgColor;
            bg.raycastTarget = false;

            _panel = panelGO.GetComponent<RectTransform>();
            _panel.sizeDelta = tooltipSize;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 0.5f);
            _panel.pivot     = new Vector2(0f, 0.5f);

            // 시간
            _timeLabel = MakeLabel(panelGO.transform, "TimeLabel",
                new Vector2(10f, -10f), new Vector2(tooltipSize.x - 10f, fontSize + 4f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), fontSize, textColor);

            // 환승 횟수
            _transferLabel = MakeLabel(panelGO.transform, "TransferLabel",
                new Vector2(10f, -10f - (fontSize + 6f)), new Vector2(tooltipSize.x - 10f, subFontSize + 4f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), subFontSize, subTextColor);

            // 비용
            _costLabel = MakeLabel(panelGO.transform, "CostLabel",
                new Vector2(10f, -10f - (fontSize + 6f) - (subFontSize + 4f)), new Vector2(tooltipSize.x - 10f, subFontSize + 4f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), subFontSize, subTextColor);

            panelGO.SetActive(false);
        }

        TMP_Text MakeLabel(Transform parent, string name, Vector2 anchoredPos, Vector2 size,
                           Vector2 anchorMin, Vector2 anchorMax, float fs, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize     = fs;
            tmp.color        = color;
            tmp.alignment    = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        // ── 이벤트 ──────────────────────────────────────────────────────

        void OnHovered(string stationId)
        {
            var core = GameCore.Instance;
            var player = core?.Player;

            // 플레이어 위치 없거나 자기 역 호버 시 숨김
            if (player == null || string.IsNullOrEmpty(player.CurrentStationId)) { Hide(); return; }
            if (stationId == player.CurrentStationId)                            { Hide(); return; }

            var graph = core?.Graph?.Graph;
            if (graph == null) { Hide(); return; }

            var path = graph.ShortestPath(player.CurrentStationId, stationId);
            if (path == null || path.Count < 2) { Hide(); return; }

            int steps     = path.Count - 1;
            int transfers = CountTransfers(path, graph, player.CurrentLineId);

            var gts = core?.GameTime;
            var ms  = core?.Money;

            int minutes = steps     * (gts != null ? gts.minutesPerMove  : 5)
                        + transfers * (gts != null ? gts.minutesTransfer : 5);
            int cost    = transfers * (ms  != null ? ms.transferCost     : 300);

            _timeLabel.text     = $"약 {FormatMinutes(minutes)} 소요";
            _transferLabel.text = transfers == 0 ? "직행" : $"환승 {transfers}회";
            _costLabel.text     = cost == 0
                ? "추가 비용 없음"
                : $"추가 ₩{cost:#,0}";

            PositionNear(stationId);
            _panel.gameObject.SetActive(true);
        }

        void OnExited() => Hide();

        void Hide()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        // ── 계산 ────────────────────────────────────────────────────────

        static int CountTransfers(List<string> path, MapGraph graph, string startLineId)
        {
            int transfers = 0;
            string prevLine = startLineId; // 플레이어가 현재 탑승 중인 노선 기준 시작
            for (int i = 0; i < path.Count - 1; i++)
            {
                string line = graph.GetConnectingLineId(path[i], path[i + 1]);
                if (line != null && line != prevLine) transfers++;
                if (line != null) prevLine = line;
            }
            return transfers;
        }

        static string FormatMinutes(int minutes)
        {
            if (minutes <= 0) return "0분";
            int h = minutes / 60, m = minutes % 60;
            if (h == 0) return $"{m}분";
            return m == 0 ? $"{h}시간" : $"{h}시간 {m}분";
        }

        // ── 위치 계산 ────────────────────────────────────────────────────

        void PositionNear(string stationId)
        {
            if (mapRenderer == null) return;
            var mapContainer = mapRenderer.MapContainer;
            if (mapContainer == null) return;

            var localPos = mapRenderer.GetStationUIPos(stationId);
            if (!localPos.HasValue) return;

            // mapContainer 로컬 좌표 → 월드 좌표
            Vector3 worldPos = mapContainer.TransformPoint(
                new Vector3(localPos.Value.x, localPos.Value.y, 0f));

            // 월드 → 화면 좌표 (ScreenSpaceOverlay 에서는 camera=null)
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPos);

            // 화면 → 캔버스 로컬 좌표
            var canvasRT = _canvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenPos, null, out Vector2 canvasLocal);

            _panel.anchoredPosition = canvasLocal + stationOffset;
        }
    }
}
