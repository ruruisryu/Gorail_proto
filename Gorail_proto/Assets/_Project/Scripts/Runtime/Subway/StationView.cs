using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.Subway
{
    /// <summary>
    /// 역 한 개를 나타내는 프리팹(StationNode) 루트에 붙는 컴포넌트.
    ///
    /// 시각 요소(라벨·점 템플릿·히트 영역)는 모두 <b>프리팹 안에</b> 존재하며,
    /// 이 컴포넌트는 그것들을 데이터(StationData·노선색)에 맞게 <b>설정(Configure)</b>만 한다.
    /// → 공통 모양 변경은 프리팹 1곳 수정으로 전체 역에 반영되고,
    ///   개별 역의 미세 조정(위치·라벨 비켜놓기 등)은 인스턴스에서 한다.
    ///
    /// 점 개수·색은 본질적으로 데이터(어느 노선이 지나는가)라서,
    /// 환승역은 DotTemplate를 노선 수만큼 복제해 인스턴스에 생성한다.
    /// </summary>
    public class StationView : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>
        /// 역이 클릭됐을 때 그 stationId를 알린다(렌더링 레이어는 발신만, 게임플레이가 구독).
        /// 이동 입력(②)을 위해 StationClickRouter가 이 이벤트를 받아 TurnResolver로 넘긴다.
        /// </summary>
        public static event System.Action<string> StationClicked;

        // 어떤 역인지 / 지나는 노선 색 — 데이터 주도(인스턴스 고유)
        [HideInInspector] public StationData stationData;
        [HideInInspector] public List<Color> lineColors = new List<Color>();

        [Header("프리팹 내부 참조")]
        [Tooltip("루트의 클릭/드래그용 잡이판 Image")]
        [SerializeField] private Image hitArea;
        [Tooltip("점 1개 템플릿(비활성). 루트=흰 배경원, 자식[0]=색상원")]
        [SerializeField] private RectTransform dotTemplate;
        [Tooltip("역 이름 라벨")]
        [SerializeField] private TextMeshProUGUI label;

        [Header("폰트")]
        [SerializeField] private TMP_FontAsset fontTransfer; // 환승역 — Korail B SDF
        [SerializeField] private TMP_FontAsset fontNormal;   // 일반역 — Korail M SDF

        [Header("레이아웃")]
        [Tooltip("점 지름")]
        [SerializeField] private float dotSize = 12f;
        [Tooltip("환승역 점 간격 = dotSize × 이 값")]
        [SerializeField] private float dotSpacingRatio = 0.85f;
        [Tooltip("히트 영역이 점보다 얼마나 더 큰지(여백)")]
        [SerializeField] private float hitPadding = 8f;

        // Configure가 생성한 점들 (재구성 시 제거 대상)
        private readonly List<GameObject> _spawnedDots = new List<GameObject>();

        // baked 인스턴스(이미 씬에 생성돼 Configure가 다시 안 불리는 역)에도 D3가 적용되도록
        // 매 활성화 시 라벨을 최상위로 보장한다.
        void Awake() => EnsureLabelTopmost();

        /// <summary>
        /// [D3] 역명 라벨이 어떤 경우에도 동그라미·선 위에 렌더되도록 overrideSorting 캔버스를 부여.
        /// 자식 순서(라벨이 점보다 먼저 그려지는 문제)와 무관하게 항상 최상위.
        /// </summary>
        void EnsureLabelTopmost()
        {
            if (label == null) return;
            var lc = label.GetComponent<Canvas>();
            if (lc == null) lc = label.gameObject.AddComponent<Canvas>();
            lc.overrideSorting = true;
            lc.sortingOrder = 100;
        }

        /// <summary>역 데이터·노선색에 맞게 점·라벨·히트 영역을 설정한다.</summary>
        public void Configure(StationData data, List<Color> colors)
        {
            stationData = data;
            lineColors  = colors != null ? new List<Color>(colors) : new List<Color>();

            int   n        = Mathf.Max(1, lineColors.Count);
            bool  transfer = lineColors.Count > 1;
            float spacing  = dotSize * dotSpacingRatio;
            float totalW   = dotSize + (n - 1) * spacing;
            float startX   = -(totalW * 0.5f) + dotSize * 0.5f;

            // 이전에 만든 점 제거
            foreach (var d in _spawnedDots) DestroySafe(d);
            _spawnedDots.Clear();

            // 점 생성(템플릿 복제)
            if (dotTemplate != null)
            {
                for (int i = 0; i < n; i++)
                {
                    var dot = Instantiate(dotTemplate.gameObject, dotTemplate.parent);
                    dot.name = "Dot_" + i;
                    dot.SetActive(true);
                    var drt = dot.GetComponent<RectTransform>();
                    drt.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                    if (drt.childCount > 0)
                    {
                        var fill = drt.GetChild(0).GetComponent<Image>(); // 색상원(자식)
                        if (fill != null) fill.color = lineColors[Mathf.Min(i, lineColors.Count - 1)];
                    }
                    _spawnedDots.Add(dot);
                }
            }

            // 라벨
            if (label != null)
            {
                label.text = data != null ? data.displayName : "";
                label.font = transfer ? fontTransfer : fontNormal;
            }
            EnsureLabelTopmost();

            // 히트 영역 — 점 묶음을 덮는 크기. 저작 중(역 선택)·런타임(이동 입력) 모두 클릭 가능.
            if (hitArea != null)
            {
                hitArea.rectTransform.sizeDelta = new Vector2(totalW + hitPadding, dotSize + hitPadding);
                hitArea.raycastTarget = true;
            }
        }

        /// <summary>역 클릭 → stationId를 이벤트로 알림(②이동 입력). 빈 stationId는 무시.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (stationData != null && !string.IsNullOrEmpty(stationData.stationId))
                StationClicked?.Invoke(stationData.stationId);
        }

        void DestroySafe(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
