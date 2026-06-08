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
    public class StationView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>
        /// 역이 클릭됐을 때 그 stationId를 알린다(렌더링 레이어는 발신만, 게임플레이가 구독).
        /// 이동 입력(②)을 위해 StationClickRouter가 이 이벤트를 받아 TurnResolver로 넘긴다.
        /// </summary>
        public static event System.Action<string> StationClicked;

        /// <summary>역에 마우스를 올렸을 때(stationId). 적 이동 프리뷰(D10) 등에 사용.</summary>
        public static event System.Action<string> StationHovered;
        /// <summary>역에서 마우스가 벗어났을 때.</summary>
        public static event System.Action StationHoverExited;

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
        void Awake()
        {
            // _spawnedDots는 [SerializeField]가 아니므로 씬 로드 시 비어 있다.
            // Build Map으로 에디터에서 구워진 baked 인스턴스의 경우 dotTemplate 형제 중
            // "Dot_"로 시작하는 오브젝트를 다시 수집해 SetDotColors가 동작하게 한다.
            _spawnedDots.Clear();
            if (dotTemplate != null)
            {
                var parent = dotTemplate.parent;
                if (parent != null)
                    foreach (Transform child in parent)
                        if (child.name.StartsWith("Dot_"))
                            _spawnedDots.Add(child.gameObject);
            }
            EnsureLabelTopmost();
            RefreshSpecialMarker();
        }

        /// <summary>[임시] baked 인스턴스(Configure 재호출 안 됨)에도 특별역 별 마커를 적용.</summary>
        void RefreshSpecialMarker()
        {
            if (stationData == null || !stationData.AllowsOutside || dotTemplate == null) return;
            var parent = dotTemplate.parent;
            if (parent == null) return;
            foreach (Transform ch in parent)
            {
                if (!ch.name.StartsWith("Dot_")) continue;
                var rootImg = ch.GetComponent<Image>();
                if (rootImg != null) rootImg.sprite = StarSprite();
                if (ch.childCount > 0)
                {
                    var f = ch.GetChild(0).GetComponent<Image>();
                    if (f != null) f.sprite = StarSprite();
                }
            }
        }

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
            bool  special  = data != null && data.AllowsOutside; // [임시] 특별역(랜드마크/상점)은 별 마커
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
                    if (special)
                    {
                        var rootImg = dot.GetComponent<Image>(); // 흰 배경원 → 별
                        if (rootImg != null) rootImg.sprite = StarSprite();
                    }
                    if (drt.childCount > 0)
                    {
                        var fill = drt.GetChild(0).GetComponent<Image>(); // 색상원(자식)
                        if (fill != null)
                        {
                            fill.color = lineColors[Mathf.Min(i, lineColors.Count - 1)];
                            if (special) fill.sprite = StarSprite(); // [임시] 별 마커
                        }
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (stationData != null && !string.IsNullOrEmpty(stationData.stationId))
                StationHovered?.Invoke(stationData.stationId);
        }

        public void OnPointerExit(PointerEventData eventData) => StationHoverExited?.Invoke();

        /// <summary>[D1] 이미 생성된 점들의 색만 갈아끼운다(활성=고유색/비활성=회색 재색칠용).</summary>
        public void SetDotColors(System.Collections.Generic.IReadOnlyList<Color> colors)
        {
            if (colors == null || colors.Count == 0) return;
            for (int i = 0; i < _spawnedDots.Count; i++)
            {
                var drt = _spawnedDots[i].GetComponent<RectTransform>();
                if (drt != null && drt.childCount > 0)
                {
                    var fill = drt.GetChild(0).GetComponent<Image>();
                    if (fill != null) fill.color = colors[Mathf.Min(i, colors.Count - 1)];
                }
            }
        }

        // [임시] 특별역 별 마커용 스프라이트(절차적 5각 별, 1회 생성·공유).
        private static Sprite _starSprite;
        static Sprite StarSprite()
        {
            if (_starSprite != null) return _starSprite;
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px  = new Color[size * size];
            var c   = new Vector2(size * 0.5f, size * 0.5f);
            float outer = size * 0.48f, inner = outer * 0.42f;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r   = (i % 2 == 0) ? outer : inner;
                float ang = Mathf.Deg2Rad * (-90f + i * 36f);
                pts[i] = c + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = PointInPoly(new Vector2(x + 0.5f, y + 0.5f), pts) ? Color.white : Color.clear;
            tex.SetPixels(px);
            tex.Apply();
            _starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
            return _starSprite;
        }

        static bool PointInPoly(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            return inside;
        }

        void DestroySafe(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
