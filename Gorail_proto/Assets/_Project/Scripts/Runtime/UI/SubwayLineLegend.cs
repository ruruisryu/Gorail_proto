using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Subway;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// [D4] 노선 범례 — 노선별 스와치 + 이름을 표시하고, 활성 노선은 고유색·비활성은 회색으로 칠한다.
    /// 패널과 스와치 프리팹은 인스펙터에서 직접 연결한다.
    /// </summary>
    public class SubwayLineLegend : MonoBehaviour
    {
        [SerializeField] private SubwayNetworkData networkData;
        [SerializeField] private TMP_FontAsset     font;

        [Header("패널")]
        [SerializeField] private Transform rowContainer; // 행을 담을 부모 Transform

        [Header("스와치 프리팹")]
        [SerializeField] private GameObject swatchPrefab; // Image + TMP_Text(호선번호) 포함

        [Header("색")]
        [SerializeField] private Color inactiveColor = new Color(0.62f, 0.62f, 0.62f, 1f);

        Player player => Game.Core.GameCore.Instance?.Player;

        private readonly List<(string lineId, Color color, Image swatch)> _rows = new();
        private bool _built;

        void Start()
        {
            if (!_built) { Build(); _built = true; }
            if (player != null) player.ActiveLinesChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (player != null) player.ActiveLinesChanged -= Refresh;
        }

        void Build()
        {
            if (networkData == null || networkData.lines == null) return;
            if (rowContainer == null) { Debug.LogWarning("[SubwayLineLegend] rowContainer가 연결되지 않았습니다."); return; }
            if (swatchPrefab == null) { Debug.LogWarning("[SubwayLineLegend] swatchPrefab이 연결되지 않았습니다."); return; }

            foreach (var line in networkData.lines)
            {
                if (line == null) continue;

                var go = Instantiate(swatchPrefab, rowContainer);
                go.name = "Swatch_" + line.lineId;

                // Image — 스와치 색
                var img = go.GetComponent<Image>();
                if (img != null)
                {
                    img.color = line.lineColor;
                    img.raycastTarget = false;
                }

                // TMP_Text — 호선 번호 (자식에서 찾기)
                var tmp = go.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null)
                {
                    var numMatch = System.Text.RegularExpressions.Regex.Match(
                        string.IsNullOrEmpty(line.displayName) ? line.lineId : line.displayName, @"\d+");
                    tmp.text = numMatch.Success ? numMatch.Value : line.lineId;
                    tmp.raycastTarget = false;
                    if (font != null) tmp.font = font;
                }

                // TMP_Text — 호선명 라벨 (스와치 GO의 자식으로 추가)
                var labelGO = new GameObject("LineName", typeof(RectTransform));
                labelGO.transform.SetParent(go.transform, false);
                var labelRT = labelGO.GetComponent<RectTransform>();
                labelRT.anchorMin = labelRT.anchorMax = new Vector2(1f, 0.5f);
                labelRT.pivot     = new Vector2(0f, 0.5f);
                labelRT.anchoredPosition = new Vector2(6f, 0f);
                labelRT.sizeDelta = new Vector2(90f, 20f);
                var labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
                labelTmp.text = string.IsNullOrEmpty(line.displayName) ? line.lineId : line.displayName;
                labelTmp.fontSize = 14f;
                labelTmp.color = Color.black;
                labelTmp.raycastTarget = false;
                if (font != null) labelTmp.font = font;

                _rows.Add((line.lineId, line.lineColor, img));
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
