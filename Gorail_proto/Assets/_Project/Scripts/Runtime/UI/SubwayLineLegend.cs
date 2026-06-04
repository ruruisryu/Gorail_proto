using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Subway;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// [D4] 노선 범례 — 노선별 색 스와치 + 이름을 표시하고, 활성 노선은 고유색·비활성은 회색으로 칠한다.
    /// 노선 활성 상태를 한눈에 보여주는 대시보드. SubwayNetworkData에서 자동 생성한다.
    ///
    /// 줌 영향 없음: 이 오브젝트를 zoom 대상(MapContent)이 아닌 PopupPanel 바로 아래에 두면
    /// 확대/축소와 무관하게 고정 크기로 유지된다.
    /// </summary>
    public class SubwayLineLegend : MonoBehaviour
    {
        [SerializeField] private SubwayNetworkData networkData;
        [SerializeField] private Player            player;
        [SerializeField] private TMP_FontAsset     font;

        [Header("레이아웃")]
        [SerializeField] private Color inactiveColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        [SerializeField] private Color panelColor    = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private float rowHeight  = 30f;
        [SerializeField] private float swatchSize = 18f;
        [SerializeField] private float panelWidth = 160f;

        private readonly List<(string lineId, Color color, Image swatch)> _rows = new();
        private bool _built;

        // 팝업이 닫힌 채 시작하므로 Start가 아니라 OnEnable에서 빌드(첫 활성화 = 팝업 열릴 때).
        void OnEnable()
        {
            if (!_built) { Build(); _built = true; }
            if (player != null) player.ActiveLinesChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (player != null) player.ActiveLinesChanged -= Refresh;
        }

        void Build()
        {
            if (networkData == null || networkData.lines == null) return;

            var selfRT = GetComponent<RectTransform>();
            if (selfRT == null) selfRT = gameObject.AddComponent<RectTransform>();
            // 부모(PopupPanel) 좌상단 고정
            selfRT.anchorMin = selfRT.anchorMax = new Vector2(0f, 1f);
            selfRT.pivot = new Vector2(0f, 1f);
            selfRT.anchoredPosition = new Vector2(12f, -12f);

            var bg = GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = panelColor;
            bg.raycastTarget = false;

            int n = networkData.lines.Count;
            selfRT.sizeDelta = new Vector2(panelWidth, n * rowHeight + 12f);

            for (int i = 0; i < n; i++)
            {
                var line = networkData.lines[i];
                if (line == null) continue;

                var row = new GameObject("Row_" + line.lineId, typeof(RectTransform));
                var rrt = row.GetComponent<RectTransform>();
                rrt.SetParent(transform, false);
                rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 1f);
                rrt.pivot = new Vector2(0f, 1f);
                rrt.anchoredPosition = new Vector2(8f, -6f - i * rowHeight);
                rrt.sizeDelta = new Vector2(panelWidth - 16f, rowHeight);

                var sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
                var swrt = sw.GetComponent<RectTransform>();
                swrt.SetParent(rrt, false);
                swrt.anchorMin = swrt.anchorMax = new Vector2(0f, 0.5f);
                swrt.pivot = new Vector2(0f, 0.5f);
                swrt.anchoredPosition = Vector2.zero;
                swrt.sizeDelta = Vector2.one * swatchSize;
                var swImg = sw.GetComponent<Image>();
                swImg.color = line.lineColor;
                swImg.raycastTarget = false;

                var lab = new GameObject("Label", typeof(RectTransform));
                var lrt = lab.GetComponent<RectTransform>();
                lrt.SetParent(rrt, false);
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.offsetMin = new Vector2(swatchSize + 8f, 0f);
                lrt.offsetMax = Vector2.zero;
                var tmp = lab.AddComponent<TextMeshProUGUI>();
                tmp.text = string.IsNullOrEmpty(line.displayName) ? line.lineId : line.displayName;
                tmp.fontSize = 16f;
                tmp.color = Color.black;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.raycastTarget = false;
                if (font != null) tmp.font = font;

                _rows.Add((line.lineId, line.lineColor, swImg));
            }
        }

        public void Refresh()
        {
            var active = player != null
                ? new HashSet<string>(player.ActiveLines)
                : new HashSet<string>();
            foreach (var r in _rows)
                if (r.swatch != null)
                    r.swatch.color = active.Contains(r.lineId) ? r.color : inactiveColor;
        }
    }
}
