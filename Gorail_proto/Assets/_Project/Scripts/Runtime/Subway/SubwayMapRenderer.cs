using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Subway
{
    /// <summary>
    /// 서울 지하철 노선도를 uGUI 위에 렌더링한다.
    ///
    /// ── 에디터 워크플로 ──
    ///   ① Build Map     : 역 GameObject + 선을 전부 새로 생성한다. 기존 배치 초기화.
    ///   ② (역 이동)     : 기획자가 Hierarchy에서 역 GameObject를 드래그해 위치 조정.
    ///   ③ Update Lines  : 현재 역 위치 기준으로 선만 다시 그린다.
    ///   ④ Save Positions: 조정된 역 위치를 StationData.mapPosition에 저장한다.
    /// </summary>
    public class SubwayMapRenderer : MonoBehaviour
    {
        // ── 참조 ────────────────────────────────────────────────────────
        [SerializeField] private SubwayNetworkData  networkData;
        [SerializeField] private PlayerLocationData playerLocation;
        [SerializeField] private EnemyLocationData  enemyLocations;
        [SerializeField] private RectTransform      mapContainer;
        [SerializeField] private StationView        stationNodePrefab;

        // ── 레이아웃 ─────────────────────────────────────────────────────
        private const float RefWidth  = 860f;
        private const float RefHeight = 550f;

        [Header("Layout")]
        [Tooltip("역 간 간격 배율. 1 = 원본, 0.5 = 절반으로 압축.")]
        [SerializeField][Range(0.1f, 1f)] private float stationSpread = 0.4f;

        [Tooltip("[D1] 비활성(아직 안 탄) 노선의 선·역 점 색(회색). 활성화되면 각 노선 고유색으로.")]
        [SerializeField] private Color inactiveLineColor = new Color(0.62f, 0.62f, 0.62f, 1f);

        private const float LineThickness = 6f;
        private const float PlayerSize    = 18f;
        private const float EnemySize     = 18f;

        private static readonly Color PlayerColor     = new Color(0.22f, 0.92f, 0.42f);
        private static readonly Color PlayerRingColor = new Color(0.22f, 0.92f, 0.42f, 0.30f);
        private static readonly Color EnemyColor      = new Color(0.95f, 0.18f, 0.18f);
        private static readonly Color EnemyRingColor  = new Color(0.95f, 0.18f, 0.18f, 0.30f);

        // ── 내부 ─────────────────────────────────────────────────────────
        private Sprite _circle;
        private float _zoomComp = 1f; // [D2] 현재 줌 역보정 배율(1 = 보정 없음)
        private HashSet<string> _activeLines; // [D1] null = 전부 고유색(에디터/초기), 아니면 이 집합만 고유색·나머지 회색

        /// <summary>[D1] 활성 집합 기준 노선 색 — 모든 선 그리기가 이걸 통해 칠해 어떤 재그리기에도 활성색 유지.</summary>
        Color LineColorFor(LineData line) =>
            _activeLines == null || _activeLines.Contains(line.lineId) ? line.lineColor : inactiveLineColor;

        /// <summary>[D1] lineId가 현재 활성(고유색)인지 여부. _activeLines==null이면 전부 활성으로 간주.</summary>
        bool IsActiveLineId(string lineId) =>
            _activeLines == null || _activeLines.Contains(lineId);
        private const string LinesTag    = "[Lines]";
        private const string StationsTag = "[Stations]";
        private const string PreviewTag  = "[Preview]"; // [D10] 적 이동 프리뷰 고스트
        private const string FxTag       = "[Fx]";      // [H6] 연출 오버레이(깜빡임·강조 등, ChaseFx 소유)
        private const string PlayerTag   = "[Player]";   // [H6] 영속 플레이어 마커(역간 부드러운 글라이드)

        // [H6] 플레이어 마커 글라이드 — 재생성 대신 영속 컨테이너를 목표 위치로 보간(플레이 모드 한정).
        private const float PlayerGlideSharpness = 14f;  // 클수록 빠르게 따라붙음(프레임 독립)
        private RectTransform _playerMarker;
        private Vector2 _playerTarget;

        public Vector2 StationBoundsMin { get; private set; }
        public Vector2 StationBoundsMax { get; private set; }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 런타임
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        void Awake()
        {
            _circle = MakeCircleSprite(128);
            // SO 위치 + 현재 stationSpread 기준으로 역을 재배치하고 선·마커를 갱신한다.
            ApplyLayout();
        }

#if UNITY_EDITOR
        // 에디터에서 stationSpread 슬라이더 등 값이 바뀌면 즉시 재배치한다.
        // OnValidate 중에는 GameObject 생성/파괴가 금지되므로 delayCall로 미룬다.
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || mapContainer == null) return;
                if (FindContainer(StationsTag) == null) return; // 아직 Build 전이면 건너뜀
                ApplyLayout();
            };
        }
#endif

        /// <summary>
        /// 역 위치의 단일 소스(StationData.mapPosition) + 현재 stationSpread 기준으로
        /// 모든 역 GameObject를 다시 배치한 뒤, 그 위치로 선과 마커를 갱신한다.
        /// stationSpread 슬라이더·런타임 시작이 모두 이 한 경로를 탄다.
        /// </summary>
        public void ApplyLayout()
        {
            if (networkData == null || mapContainer == null) return;
            if (_circle == null) _circle = MakeCircleSprite(128);

            var stationsRT = FindContainer(StationsTag);
            if (stationsRT == null) { BuildMap(); return; } // 아직 역이 없으면 최초 생성

            // SO(mapPosition) → 현재 spread 적용한 UI 좌표로 모든 역을 재배치
            foreach (var view in stationsRT.GetComponentsInChildren<StationView>())
            {
                if (view.stationData == null) continue;
                var rt = view.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = UI(view.stationData.mapPosition);
            }

            UpdateLines();    // 재배치된 위치 기준으로 선 갱신
            RefreshMarkers(); // 플레이어·적 마커 갱신
        }

        /// <summary>플레이어·적 마커를 현재 역 위치 기준으로 다시 그린다.</summary>
        public void RefreshMarkers()
        {
            // Lines, Stations 컨테이너가 아닌 직접 자식(마커)만 제거
            for (int i = mapContainer.childCount - 1; i >= 0; i--)
            {
                var child = mapContainer.GetChild(i);
                if (child.name == LinesTag || child.name == StationsTag || child.name == PreviewTag || child.name == FxTag || child.name == PlayerTag) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            if (enemyLocations != null)
            {
                int idx = 0;
                foreach (var id in enemyLocations.enemyStationIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    var pos = GetStationUIPos(id);
                    if (pos.HasValue) DrawEnemy(pos.Value, idx++);
                }
            }

            if (playerLocation != null && !string.IsNullOrEmpty(playerLocation.currentStationId))
            {
                var pos = GetStationUIPos(playerLocation.currentStationId);
                if (pos.HasValue) DrawPlayer(pos.Value);
            }

            ApplyCompToMarkers(); // [D2] 새로 그린 마커에도 현재 줌 보정 반영
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [D2] 줌 크기 고정 — SubwayMapZoom이 배율 변경 시 호출
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 줌(부모 localScale)으로 커지는 역 점·선·마커를 역보정해, 임계 배율 이상에서 화면상 크기를 고정한다.
        /// 역(역명 라벨 포함)·마커는 균등 역보정, 선은 굵기 축만 역보정(길이는 줌 따라 벌어짐).
        /// lockThreshold(고정 시작 배율)는 SubwayMapZoom이 인스펙터 값으로 전달한다.
        /// </summary>
        public void ApplyZoomCompensation(float zoom, float lockThreshold)
        {
            _zoomComp = zoom > lockThreshold && zoom > 0f ? lockThreshold / zoom : 1f;

            var stationsRT = FindContainer(StationsTag);
            if (stationsRT != null)
                foreach (var view in stationsRT.GetComponentsInChildren<StationView>(true))
                    view.transform.localScale = Vector3.one * _zoomComp;

            var linesRT = FindContainer(LinesTag);
            if (linesRT != null)
                for (int i = 0; i < linesRT.childCount; i++)
                    linesRT.GetChild(i).localScale = new Vector3(1f, _zoomComp, 1f); // 굵기만 고정

            ApplyCompToMarkers();
        }

        /// <summary>마커(컨테이너가 아닌 직접 자식)에 현재 줌 보정 배율을 적용한다.</summary>
        void ApplyCompToMarkers()
        {
            for (int i = 0; i < mapContainer.childCount; i++)
            {
                var child = mapContainer.GetChild(i);
                if (child.name == LinesTag || child.name == StationsTag || child.name == PreviewTag || child.name == FxTag || child.name == PlayerTag) continue;
                child.localScale = Vector3.one * _zoomComp;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [D10] 적 이동 프리뷰 — 호버 시 추격자 예측 위치를 고스트로
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static readonly Color RouteColor = new Color(1f, 0.85f, 0.10f, 0.95f); // 플레이어 이동 경로(노랑)
        private static readonly Color DestRingColor = new Color(1f, 0.85f, 0.10f, 0.45f);
        private static readonly Color DestColor     = new Color(1f, 0.85f, 0.10f, 0.95f);
        private static readonly Color EnemyRouteColor = new Color(0.95f, 0.18f, 0.18f, 0.85f); // 적 이동 경로(빨강)

        /// <summary>현재 지도에 표시 중인 적 마커의 역 ID들(프리뷰가 같은 적을 기준으로 시뮬하도록).</summary>
        public IReadOnlyList<string> DisplayedEnemyStations =>
            enemyLocations != null ? enemyLocations.enemyStationIds : null;

        /// <summary>
        /// [D10] 호버 이동 프리뷰: 플레이어 이동 <b>경로</b>(노랑)+<b>도착역</b>과 함께,
        /// 각 적의 <b>이동 경로</b>(빨강 루트)+<b>예측 도착 위치</b>(고스트)를 그린다.
        /// 적이 없어도(미스폰) 플레이어 경로·도착역은 항상 보인다.
        /// </summary>
        public void ShowChasePreview(IList<string> playerPath, IList<IReadOnlyList<string>> enemyPaths)
        {
            ClearChasePreview();
            bool hasPlayer = playerPath != null && playerPath.Count >= 2;
            bool hasEnemy  = enemyPaths != null && enemyPaths.Count > 0;
            if (!hasPlayer && !hasEnemy) return;
            var prev = CreateContainer(PreviewTag, mapContainer.childCount); // 최상위

            // ① 적 이동 경로(빨강 루트) + 예측 도착 고스트 — 플레이어 경로 아래에 깔리도록 먼저 그림
            if (hasEnemy)
                foreach (var ep in enemyPaths)
                {
                    if (ep == null || ep.Count == 0) continue;
                    for (int i = 0; i < ep.Count - 1; i++)
                    {
                        var a = GetStationUIPos(ep[i]);
                        var b = GetStationUIPos(ep[i + 1]);
                        if (!a.HasValue || !b.HasValue) continue;
                        SegmentDirect(a.Value, b.Value, EnemyRouteColor, LineThickness + 1f, prev).name = "EnemyRouteSeg";
                    }
                    var end = GetStationUIPos(ep[ep.Count - 1]);
                    if (end.HasValue)
                    {
                        Circ("PreviewRing", prev, end.Value, EnemySize + 16f, new Color(0.95f, 0.18f, 0.18f, 0.22f));
                        Circ("Preview",     prev, end.Value, EnemySize + 4f,  new Color(0.95f, 0.18f, 0.18f, 0.70f));
                    }
                }

            // ② 플레이어 이동 경로(노랑) + 도착역 — 적 경로 위에 그림
            if (hasPlayer)
            {
                for (int i = 0; i < playerPath.Count - 1; i++)
                {
                    var a = GetStationUIPos(playerPath[i]);
                    var b = GetStationUIPos(playerPath[i + 1]);
                    if (!a.HasValue || !b.HasValue) continue;
                    SegmentDirect(a.Value, b.Value, RouteColor, LineThickness + 5f, prev).name = "RouteSeg";
                }
                var dest = GetStationUIPos(playerPath[playerPath.Count - 1]);
                if (dest.HasValue)
                {
                    Circ("DestRing", prev, dest.Value, PlayerSize + 16f, DestRingColor);
                    Circ("Dest",     prev, dest.Value, PlayerSize + 2f,  DestColor);
                }
            }

            // 줌 보정: 마커는 균등, 경로선은 굵기축만(길이는 줌 따라 벌어짐)
            for (int i = 0; i < prev.childCount; i++)
            {
                var ch = prev.GetChild(i);
                ch.localScale = (ch.name == "RouteSeg" || ch.name == "EnemyRouteSeg")
                    ? new Vector3(1f, _zoomComp, 1f)
                    : Vector3.one * _zoomComp;
            }
        }

        public void ClearChasePreview() => DestroyContainer(PreviewTag);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [H6] 연출 오버레이 레이어 — ChaseFx가 깜빡임·강조 마커를 여기에 그린다.
        //      RefreshMarkers/줌보정이 건드리지 않는 영속 컨테이너.
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>현재 줌 역보정 배율(연출 마커도 화면상 크기를 맞추는 데 사용).</summary>
        public float ZoomComp => _zoomComp;

        /// <summary>연출 마커용 공유 원형 스프라이트.</summary>
        public Sprite CircleSprite => _circle != null ? _circle : (_circle = MakeCircleSprite(128));

        /// <summary>연출 오버레이 컨테이너([Fx])를 얻거나 만든다(최상위, 마커 갱신에 안 지워짐).</summary>
        public RectTransform GetOrCreateFxLayer()
        {
            var rt = FindContainer(FxTag);
            if (rt == null) rt = CreateContainer(FxTag, mapContainer.childCount);
            else rt.SetSiblingIndex(mapContainer.childCount - 1); // 항상 최상위 유지
            return rt;
        }

        /// <summary>[Fx] 레이어에 원형 연출 마커 1개를 만들어 반환(색·크기 지정, 줌 보정 적용).</summary>
        public Image CreateFxCircle(RectTransform fxLayer, Vector2 anchoredPos, float size, Color color)
        {
            var go = Circ("Fx", fxLayer, anchoredPos, size, color);
            go.transform.localScale = Vector3.one * _zoomComp;
            return go.GetComponent<Image>();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [D1] 활성 노선 색 / 비활성 회색
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 활성 노선은 고유색, 비활성(아직 안 탄) 노선은 회색으로 선·역 점을 다시 칠한다(§5-3 활성 상태 시각화).
        /// 활성 노선 변경 시 외부(MapActivationView)에서 호출한다.
        /// </summary>
        public void ApplyActiveLineColors(IEnumerable<string> activeLines)
        {
            if (networkData == null || mapContainer == null) return;
            var active = new HashSet<string>(activeLines ?? Enumerable.Empty<string>());

            // 활성 집합을 저장 → 이후 모든 선 그리기(UpdateLines/BuildMap)가 LineColorFor로 이 색을 쓴다.
            _activeLines = active;
            UpdateLines(); // 활성=고유색/비활성=회색으로 선 다시 그림

            // 역 점: 그 점(색)을 쓰는 노선 중 하나라도 활성이면 고유색, 아니면 회색
            var stationsRT = FindContainer(StationsTag);
            if (stationsRT == null) return;
            var views = new Dictionary<string, StationView>();
            foreach (var v in stationsRT.GetComponentsInChildren<StationView>(true))
                if (v.stationData != null) views[v.stationData.stationId] = v;

            foreach (var kv in CollectStationColorInfo())
            {
                if (!views.TryGetValue(kv.Key, out var view)) continue;
                var colors = kv.Value
                    .Select(ci => ci.lineIds.Any(active.Contains) ? ci.color : inactiveLineColor)
                    .ToList();
                view.SetDotColors(colors);
            }
        }

        /// <summary>역별 distinct 색(Configure와 같은 순서) + 각 색을 쓰는 lineId 목록.</summary>
        Dictionary<string, List<(Color color, List<string> lineIds)>> CollectStationColorInfo()
        {
            var result = new Dictionary<string, List<(Color color, List<string> lineIds)>>();
            foreach (var line in networkData.lines)
            {
                if (line == null) continue;
                foreach (var stn in line.stations)
                {
                    if (stn == null) continue;
                    if (!result.TryGetValue(stn.stationId, out var list))
                        result[stn.stationId] = list = new List<(Color color, List<string> lineIds)>();
                    int idx = list.FindIndex(e => e.color == line.lineColor);
                    if (idx < 0) list.Add((line.lineColor, new List<string> { line.lineId }));
                    else if (!list[idx].lineIds.Contains(line.lineId)) list[idx].lineIds.Add(line.lineId);
                }
            }
            return result;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 에디터 진입점 (Inspector 버튼에서 호출)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>역 GameObject + 선을 전부 새로 만든다. 기존 배치가 초기화된다.</summary>
        public void BuildMap()
        {
            if (networkData == null || mapContainer == null) return;
            _circle = MakeCircleSprite(128);

            // mapContainer 전체 클리어 (이전 Render() 잔재 포함)
            for (int i = mapContainer.childCount - 1; i >= 0; i--)
            {
                var child = mapContainer.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            var linesRT    = CreateContainer(LinesTag,    siblingIndex: 0);
            var stationsRT = CreateContainer(StationsTag, siblingIndex: 1);

            var stationColors = CollectStationColors();

            // 선 그리기 (StationData.mapPosition 사용) — 비활성 먼저, 활성 나중
            foreach (var line in networkData.lines)
                if (line != null && !IsActiveLineId(line.lineId)) DrawLineSegmentsFromData(line, linesRT);
            foreach (var line in networkData.lines)
                if (line != null &&  IsActiveLineId(line.lineId)) DrawLineSegmentsFromData(line, linesRT);

            // 역 GameObject 생성
            var drawn = new HashSet<string>();
            foreach (var line in networkData.lines)
                foreach (var stn in line.stations)
                {
                    if (stn == null || drawn.Contains(stn.stationId)) continue;
                    drawn.Add(stn.stationId);
                    CreateStationGO(stn, stationColors[stn.stationId], stationsRT);
                }

            Debug.Log($"[SubwayMapRenderer] Build Map 완료 — 역:{drawn.Count}개");
        }

        /// <summary>역 위치는 유지하고, 현재 StationView 위치 기준으로 선만 다시 그린다.</summary>
        public void UpdateLines()
        {
            if (networkData == null || mapContainer == null) return;
            _circle = MakeCircleSprite(128);

            // Stations가 없으면 BuildMap으로 초기 생성
            if (FindContainer(StationsTag) == null) { BuildMap(); return; }

            DestroyContainer(LinesTag);
            var linesRT = CreateContainer(LinesTag, siblingIndex: 0);

            // StationView → anchoredPosition 딕셔너리
            var posMap = BuildPosMap();

            // [D1] 비활성 노선을 먼저, 활성 노선을 나중에 그린다.
            // uGUI는 나중에 그린 오브젝트가 위에 렌더되므로, 겹치는 구간에서
            // 활성 노선 색이 비활성 회색 위에 반드시 올라오게 된다.
            void DrawLineSegments(LineData line)
            {
                var s   = line.stations;
                var col = LineColorFor(line);
                for (int i = 0; i < s.Count - 1; i++)
                {
                    if (s[i] == null || s[i + 1] == null) continue;
                    if (!posMap.TryGetValue(s[i].stationId,     out var from)) continue;
                    if (!posMap.TryGetValue(s[i + 1].stationId, out var to))   continue;
                    SegmentDirect(from, to, col, LineThickness, linesRT);
                }
                if (line.isCircular && s.Count > 1 && s[0] != null && s[s.Count - 1] != null)
                {
                    if (posMap.TryGetValue(s[s.Count - 1].stationId, out var from) &&
                        posMap.TryGetValue(s[0].stationId,            out var to))
                        SegmentDirect(from, to, col, LineThickness, linesRT);
                }
            }

            foreach (var line in networkData.lines)
                if (line != null && !IsActiveLineId(line.lineId)) DrawLineSegments(line); // 비활성 먼저
            foreach (var line in networkData.lines)
                if (line != null &&  IsActiveLineId(line.lineId)) DrawLineSegments(line); // 활성 나중(위)
            // [D2] 새로 그린 선 굵기에 현재 줌 보정 재적용
            for (int i = 0; i < linesRT.childCount; i++)
                linesRT.GetChild(i).localScale = new Vector3(1f, _zoomComp, 1f);
        }

        /// <summary>현재 StationView 위치를 StationData.mapPosition에 저장한다.</summary>
        public void SavePositions()
        {
            var stationsRT = FindContainer(StationsTag);
            if (stationsRT == null)
            {
                Debug.LogWarning("[SubwayMapRenderer] Build Map을 먼저 실행하세요.");
                return;
            }

            Rect  r  = mapContainer.rect;
            float sx = r.width  / RefWidth;
            float sy = r.height / RefHeight;
            float cx = RefWidth  * 0.5f;
            float cy = RefHeight * 0.5f;

            int count = 0;
            foreach (var view in stationsRT.GetComponentsInChildren<StationView>())
            {
                var rt = view.GetComponent<RectTransform>();
                if (rt == null || view.stationData == null) continue;

                // UI 좌표 → mapPosition 역변환
                Vector2 ui     = rt.anchoredPosition;
                float scaledX  = (ui.x + r.width  * 0.5f) / sx;
                float scaledY  = (r.height * 0.5f  - ui.y) / sy;
                float mapX     = (scaledX - cx) / stationSpread + cx;
                float mapY     = (scaledY - cy) / stationSpread + cy;

                view.stationData.mapPosition = new Vector2(mapX, mapY);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(view.stationData);
#endif
                count++;
            }
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            Debug.Log($"[SubwayMapRenderer] {count}개 역 위치를 SO에 저장했습니다.");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 내부 헬퍼
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        Dictionary<string, List<Color>> CollectStationColors()
        {
            var result = new Dictionary<string, List<Color>>();
            foreach (var line in networkData.lines)
                foreach (var stn in line.stations)
                {
                    if (stn == null) continue;
                    if (!result.ContainsKey(stn.stationId))
                        result[stn.stationId] = new List<Color>();
                    if (!result[stn.stationId].Contains(line.lineColor))
                        result[stn.stationId].Add(line.lineColor);
                }
            return result;
        }

        Dictionary<string, Vector2> BuildPosMap()
        {
            var posMap = new Dictionary<string, Vector2>();
            var stationsRT = FindContainer(StationsTag);
            if (stationsRT == null) return posMap;
            foreach (var view in stationsRT.GetComponentsInChildren<StationView>())
            {
                var rt = view.GetComponent<RectTransform>();
                if (rt != null && view.stationData != null)
                    posMap[view.stationData.stationId] = rt.anchoredPosition;
            }
            return posMap;
        }

        void CreateStationGO(StationData stn, List<Color> lineColors, RectTransform parent)
        {
            if (stationNodePrefab == null)
            {
                Debug.LogError("[SubwayMapRenderer] stationNodePrefab가 비어 있습니다. StationNode 프리팹을 할당하세요.");
                return;
            }

            Vector2 uiPos = UI(stn.mapPosition);

            // 프리팹 인스턴스화
            // 에디터 Edit 모드: PrefabUtility.InstantiatePrefab → 프리팹 연결 유지
            // Play 모드 / 빌드: Instantiate
            StationView view;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var go2 = UnityEditor.PrefabUtility.InstantiatePrefab(
                    stationNodePrefab.gameObject, parent) as GameObject;
                view = go2.GetComponent<StationView>();
            }
            else
#endif
            {
                view = Instantiate(stationNodePrefab, parent);
            }

            var go = view.gameObject;
            go.name = "Stn_" + stn.stationId;

            var rt = view.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * 0.5f;
            rt.anchoredPosition = uiPos;

            // 시각 구성은 프리팹+StationView가 담당
            view.Configure(stn, lineColors);
        }

        void DrawLineSegmentsFromData(LineData line, RectTransform container)
        {
            var s = line.stations;
            var col = LineColorFor(line); // [D1] 활성=고유색/비활성=회색
            for (int i = 0; i < s.Count - 1; i++)
            {
                if (s[i] == null || s[i + 1] == null) continue;
                SegmentDirect(UI(s[i].mapPosition), UI(s[i + 1].mapPosition),
                              col, LineThickness, container);
            }
            if (line.isCircular && s.Count > 1 && s[0] != null && s[s.Count - 1] != null)
                SegmentDirect(UI(s[s.Count - 1].mapPosition), UI(s[0].mapPosition),
                              col, LineThickness, container);
        }

        GameObject SegmentDirect(Vector2 from, Vector2 to, Color color, float thickness, RectTransform container)
        {
            var go  = new GameObject("Seg");
            go.transform.SetParent(container, false);
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;

            var rt  = img.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            Vector2 dir  = to - from;
            rt.anchoredPosition = from;
            rt.sizeDelta        = new Vector2(dir.magnitude, thickness);
            rt.localRotation    = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            return go;
        }

        void DrawPlayer(Vector2 uiPos)
        {
            // 에디터 미리보기: 기존처럼 transient(매번 재생성). 글라이드는 플레이 모드 전용.
            if (!Application.isPlaying)
            {
                Circ("PlayerRing",    mapContainer, uiPos, PlayerSize + 12f, PlayerRingColor);
                Circ("PlayerOutline", mapContainer, uiPos, PlayerSize + 4f,  Color.white);
                Circ("Player",        mapContainer, uiPos, PlayerSize,        PlayerColor);
                return;
            }

            // 플레이: 영속 [Player] 컨테이너를 만들어 두고, 목표 위치만 갱신(Update가 보간 이동).
            if (_playerMarker == null)
            {
                var existing = mapContainer.Find(PlayerTag) as RectTransform;
                if (existing != null) _playerMarker = existing;
            }
            if (_playerMarker == null)
            {
                _playerMarker = CreateContainer(PlayerTag, mapContainer.childCount);
                Circ("PlayerRing",    _playerMarker, Vector2.zero, PlayerSize + 12f, PlayerRingColor);
                Circ("PlayerOutline", _playerMarker, Vector2.zero, PlayerSize + 4f,  Color.white);
                Circ("Player",        _playerMarker, Vector2.zero, PlayerSize,        PlayerColor);
                _playerMarker.anchoredPosition = uiPos; // 최초엔 스냅
            }
            _playerMarker.SetSiblingIndex(mapContainer.childCount - 1); // 적·선 위
            _playerTarget = uiPos;
        }

        // [H6] 플레이어 마커를 목표 역 위치로 프레임 독립 보간 + 줌 보정.
        void Update()
        {
            if (!Application.isPlaying || _playerMarker == null) return;
            float k = 1f - Mathf.Exp(-PlayerGlideSharpness * Time.deltaTime);
            _playerMarker.anchoredPosition = Vector2.Lerp(_playerMarker.anchoredPosition, _playerTarget, k);
            _playerMarker.localScale = Vector3.one * _zoomComp;
        }

        void DrawEnemy(Vector2 uiPos, int index)
        {
            Circ($"EnemyRing_{index}",    mapContainer, uiPos, EnemySize + 12f, EnemyRingColor);
            Circ($"EnemyOutline_{index}", mapContainer, uiPos, EnemySize + 4f,  Color.white);
            Circ($"Enemy_{index}",        mapContainer, uiPos, EnemySize,        EnemyColor);
        }

        /// <summary>역의 현재 UI 좌표(MapContent 로컬 anchoredPosition). 없으면 null. (중앙 정렬 등에 사용)</summary>
        public Vector2? GetStationUIPos(string stationId)
        {
            var posMap = BuildPosMap();
            if (posMap.TryGetValue(stationId, out var pos)) return pos;
            var stn = FindStationData(stationId);
            return stn != null ? (Vector2?)UI(stn.mapPosition) : null;
        }

        GameObject Circ(string name, Transform parent, Vector2 anchoredPos, float size, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * 0.5f;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = Vector2.one * size;
            var img = go.AddComponent<Image>();
            img.sprite        = _circle;
            img.color         = color;
            img.raycastTarget = false;
            return go;
        }

        Vector2 UI(Vector2 mapPos)
        {
            Rect  r  = mapContainer.rect;
            float sx = r.width  / RefWidth;
            float sy = r.height / RefHeight;
            float cx = RefWidth  * 0.5f;
            float cy = RefHeight * 0.5f;
            float scaledX = cx + (mapPos.x - cx) * stationSpread;
            float scaledY = cy + (mapPos.y - cy) * stationSpread;
            return new Vector2(scaledX * sx - r.width  * 0.5f,
                              -(scaledY * sy) + r.height * 0.5f);
        }

        RectTransform CreateContainer(string containerName, int siblingIndex)
        {
            var go = new GameObject(containerName);
            go.transform.SetParent(mapContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * 0.5f;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;
            go.transform.SetSiblingIndex(siblingIndex);
            return rt;
        }

        RectTransform FindContainer(string containerName)
        {
            var t = mapContainer.Find(containerName);
            return t != null ? t.GetComponent<RectTransform>() : null;
        }

        void DestroyContainer(string containerName)
        {
            var t = mapContainer.Find(containerName);
            if (t == null) return;
            if (Application.isPlaying) Destroy(t.gameObject);
            else DestroyImmediate(t.gameObject);
        }

        StationData FindStationData(string id)
        {
            foreach (var line in networkData.lines)
                foreach (var stn in line.stations)
                    if (stn != null && stn.stationId == id) return stn;
            return null;
        }

        static Sprite MakeCircleSprite(int radius)
        {
            int size = radius * 2;
            var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float cr   = radius - 0.5f;
            var center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    pixels[y * size + x] = dist <= cr ? Color.white : Color.clear;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        }
    }
}
