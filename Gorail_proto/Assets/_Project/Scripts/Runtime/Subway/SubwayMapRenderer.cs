using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Subway
{
    /// <summary>
    /// 씬에 직접 배치된 UIBezierLine·StationView를 스캔해 노선도를 운영한다.
    /// 역·선은 에디터에서 수동으로 배치하며, 이 컴포넌트는 색·오버레이·마커만 제어한다.
    /// </summary>
    public class SubwayMapRenderer : MonoBehaviour
    {
        // ── 참조 ────────────────────────────────────────────────────────
        [SerializeField] private SubwayNetworkData  networkData;
        [SerializeField] private PlayerLocationData playerLocation;
        [SerializeField] private EnemyLocationData  enemyLocations;
        [SerializeField] private RectTransform      mapContainer;

        MapGraph Graph => Game.Core.GameCore.Instance?.Graph?.Graph;

        // ── 비활성 색 ────────────────────────────────────────────────────
        [Header("색")]
        [SerializeField] private Color inactiveLineColor = new Color(0.62f, 0.62f, 0.62f, 1f);

        // ── 마커 아이콘 ──────────────────────────────────────────────────
        [Header("마커 프리팹 (비우면 원형 폴백)")]
        [SerializeField] private GameObject playerMarkerPrefab;
        [SerializeField] private GameObject trackerMarkerPrefab;

        [Header("마커 크기 (비율 40 : 52.3 고정)")]
        [SerializeField] private float markerWidth = 20f;
        private static readonly Vector2 MarkerRatio = new Vector2(40f, 52.3f);
        Vector2 MarkerSize => new Vector2(markerWidth, markerWidth * MarkerRatio.y / MarkerRatio.x);

        [Header("마커 밥 애니메이션")]
        [SerializeField] private float bobAmplitude = 4f;
        [SerializeField] private float bobSpeed     = 2.2f;

        private static readonly Color PlayerFallbackColor  = new Color(0.22f, 0.92f, 0.42f);
        private static readonly Color TrackerFallbackColor = new Color(0.95f, 0.18f, 0.18f);

        // ── 컨테이너 태그 ────────────────────────────────────────────────
        private const string LinesTag     = "[Lines]";
        private const string StationsTag  = "[Stations]";
        private const string PreviewTag   = "[Preview]";
        private const string FxTag        = "[Fx]";
        private const string PlayerTag    = "[Player]";
        private const string LineHLTag    = "[LineHL]";
        private const string RouteHintTag = "[RouteHint]";

        private static readonly Color RouteColor      = new Color(1f, 0.85f, 0.10f, 0.95f);
        private static readonly Color DestRingColor   = new Color(1f, 0.85f, 0.10f, 0.45f);
        private static readonly Color DestColor       = new Color(1f, 0.85f, 0.10f, 0.95f);
        private static readonly Color EnemyRouteColor = new Color(0.95f, 0.18f, 0.18f, 0.85f);
        private static readonly Color HintRouteColor  = new Color(0.75f, 0.92f, 1f, 0.70f);
        private static readonly Color HintDestColor   = new Color(0.75f, 0.92f, 1f, 0.90f);

        // ── 내부 상태 ────────────────────────────────────────────────────
        private Sprite _circle;
        private float  _zoomComp = 1f;
        private HashSet<string> _activeLines;

        private readonly List<UIBezierLine>                             _bezierLines = new();
        private readonly Dictionary<(string, string), List<UIBezierLine>> _segmentMap = new();

        private const float PlayerGlideSharpness = 14f;
        private RectTransform _playerMarker;
        private Vector2       _playerTarget;
        private Vector2       _playerBasePos;
        private Image         _playerBorder; // 노선 색 테두리

        // 추격자 마커 목록 (RefreshMarkers마다 갱신)
        private readonly List<RectTransform> _enemyBobRTs  = new();
        private readonly List<RectTransform> _enemyOuterRTs = new();

        private readonly Dictionary<string, RectTransform> _containerCache = new();
        private readonly List<StationView>                 _stationViews   = new();
        private readonly Dictionary<string, StationView>   _stationViewMap = new();
        private readonly Dictionary<string, RectTransform> _stationRTMap   = new();
        private Dictionary<string, Vector2>                _posMap;

        public Vector2 StationBoundsMin { get; private set; }
        public Vector2 StationBoundsMax { get; private set; }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 런타임
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        void Awake()
        {
            _circle = MakeCircleSprite(128);
            var stationsRT = FindContainer(StationsTag);
            if (stationsRT != null) RebuildStationCache(stationsRT);
            var linesRT = FindContainer(LinesTag);
            if (linesRT != null) RebuildBezierCache(linesRT);
            RefreshMarkers();
        }

        void Start()
        {
            foreach (var outer in _enemyOuterRTs)
                if (outer != null) outer.SetSiblingIndex(mapContainer.childCount - 1);
            if (_playerMarker != null)
                _playerMarker.SetSiblingIndex(mapContainer.childCount - 1);
        }

        // ── 마커 ────────────────────────────────────────────────────────

        public void RefreshMarkers()
        {
            _enemyBobRTs.Clear();
            _enemyOuterRTs.Clear();

            for (int i = mapContainer.childCount - 1; i >= 0; i--)
            {
                var child = mapContainer.GetChild(i);
                if (child.name == LinesTag    || child.name == StationsTag ||
                    child.name == PreviewTag  || child.name == FxTag       ||
                    child.name == PlayerTag   || child.name == LineHLTag   ||
                    child.name == RouteHintTag) continue;
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
                    if (pos.HasValue) DrawTracker(pos.Value, id, idx++);
                }
            }

            if (playerLocation != null && !string.IsNullOrEmpty(playerLocation.currentStationId))
            {
                var pos = GetStationUIPos(playerLocation.currentStationId);
                if (pos.HasValue) DrawPlayer(pos.Value);
            }

        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [D2] 줌 크기 고정
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 줌 배율이 바뀔 때 SubwayMapZoom이 호출한다.
        /// 마커는 화면상 크기를 고정(역배율 적용), 선·프리뷰는 줌과 함께 스케일됨.
        /// </summary>
        public void ApplyZoomCompensation(float zoom, float lockThreshold)
        {
            _zoomComp = zoom > lockThreshold && zoom > 0f ? lockThreshold / zoom : 1f;

            // 플레이어 마커
            if (_playerMarker != null)
                _playerMarker.localScale = Vector3.one * _zoomComp;

            // 추격자 마커 (outer 앵커 기준)
            foreach (var outer in _enemyOuterRTs)
                if (outer != null) outer.localScale = Vector3.one * _zoomComp;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [D10] 적 이동 프리뷰
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public IReadOnlyList<string> DisplayedEnemyStations =>
            enemyLocations != null ? enemyLocations.enemyStationIds : null;

        public void ShowChasePreview(IReadOnlyList<string> playerPath, IList<IReadOnlyList<string>> enemyPaths)
        {
            ClearChasePreview();
            bool hasPlayer = playerPath != null && playerPath.Count >= 2;
            bool hasEnemy  = enemyPaths != null && enemyPaths.Count > 0;
            if (!hasPlayer && !hasEnemy) return;

            var prev = CreateContainer(PreviewTag, mapContainer.childCount);

            if (hasEnemy)
                foreach (var ep in enemyPaths)
                {
                    if (ep == null || ep.Count == 0) continue;
                    DrawPathOverlay(ep, EnemyRouteColor, 1.2f, prev);
                    var end = GetStationUIPos(ep[ep.Count - 1]);
                    if (end.HasValue)
                    {
                        Circ("PreviewRing", prev, end.Value, 48f, new Color(0.95f, 0.18f, 0.18f, 0.22f));
                        Circ("Preview",     prev, end.Value, 36f, new Color(0.95f, 0.18f, 0.18f, 0.70f));
                    }
                }

            if (hasPlayer)
            {
                DrawPathOverlay(playerPath, RouteColor, 1.8f, prev);
                var dest = GetStationUIPos(playerPath[playerPath.Count - 1]);
                if (dest.HasValue)
                {
                    Circ("DestRing", prev, dest.Value, 48f, DestRingColor);
                    Circ("Dest",     prev, dest.Value, 34f, DestColor);
                }
            }

            for (int i = 0; i < prev.childCount; i++)
            {
                var ch = prev.GetChild(i);
                if (ch.GetComponent<UIBezierLine>() == null)
                    ch.localScale = Vector3.one * _zoomComp;
            }
        }

        public void ClearChasePreview() => DestroyContainer(PreviewTag);

        // ── 오버레이 내부 ────────────────────────────────────────────────

        void DrawSegmentOverlay(string a, string b, Color color, float widthMult, RectTransform layer)
        {
            if (!_segmentMap.TryGetValue((a, b), out var lines)) return;
            foreach (var orig in lines)
            {
                if (orig == null) continue;
                var clone = Instantiate(orig.gameObject, layer);
                var cl = clone.GetComponent<UIBezierLine>();
                cl.color = color;
                cl.SetWidthScale(_zoomComp * widthMult);
                clone.name = "OverlaySeg";
            }
        }

        void DrawPathOverlay(IReadOnlyList<string> path, Color color, float widthMult, RectTransform layer)
        {
            for (int i = 0; i < path.Count - 1; i++)
                DrawSegmentOverlay(path[i], path[i + 1], color, widthMult, layer);
        }

        void ApplyWidthScaleToContainer(RectTransform container, float scale)
        {
            if (container == null) return;
            foreach (var line in container.GetComponentsInChildren<UIBezierLine>())
                if (line != null) line.SetWidthScale(scale);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 추천 경로 힌트 (갈 수 없는 역 호버)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void ShowRouteHint(IReadOnlyList<string> path)
        {
            ClearRouteHint();
            if (path == null || path.Count < 2) return;

            var layer = CreateContainer(RouteHintTag, mapContainer.childCount);
            DrawPathOverlay(path, HintRouteColor, 1.5f, layer);

            var dest = GetStationUIPos(path[path.Count - 1]);
            if (dest.HasValue)
            {
                Circ("HintRing", layer, dest.Value, 48f, new Color(0.75f, 0.92f, 1f, 0.25f));
                Circ("HintDest", layer, dest.Value, 34f, HintDestColor);
            }

            for (int i = 0; i < layer.childCount; i++)
            {
                var ch = layer.GetChild(i);
                if (ch.GetComponent<UIBezierLine>() == null)
                    ch.localScale = Vector3.one * _zoomComp;
            }
        }

        public void ClearRouteHint() => DestroyContainer(RouteHintTag);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [H6] 연출 오버레이 레이어
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public float  ZoomComp    => _zoomComp;
        public Sprite CircleSprite => _circle != null ? _circle : (_circle = MakeCircleSprite(128));

        public RectTransform GetOrCreateFxLayer()
        {
            var rt = FindContainer(FxTag);
            if (rt == null) rt = CreateContainer(FxTag, mapContainer.childCount);
            else rt.SetSiblingIndex(mapContainer.childCount - 1);
            return rt;
        }

        public Image CreateFxCircle(RectTransform fxLayer, Vector2 anchoredPos, float size, Color color)
        {
            var go = Circ("Fx", fxLayer, anchoredPos, size, color);
            go.transform.localScale = Vector3.one * _zoomComp;
            return go.GetComponent<Image>();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // [D1] 활성 노선 색
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void ApplyActiveLineColors(IEnumerable<string> activeLines, string currentLineId = null)
        {
            if (networkData == null || mapContainer == null) return;
            var active = new HashSet<string>(activeLines ?? Enumerable.Empty<string>());
            _activeLines = active;

            foreach (var seg in _bezierLines)
            {
                if (seg == null) continue;
                var lineData = networkData.lines.FirstOrDefault(l => l != null && l.lineId == seg.lineId);
                seg.color = active.Contains(seg.lineId)
                    ? (lineData != null ? lineData.lineColor : Color.white)
                    : inactiveLineColor;
                seg.SetVerticesDirty();
            }

            var stationLineIds = new Dictionary<string, List<string>>();
            foreach (var line in networkData.lines)
            {
                if (line == null) continue;
                foreach (var stn in line.stations)
                {
                    if (stn == null) continue;
                    if (!stationLineIds.ContainsKey(stn.stationId))
                        stationLineIds[stn.stationId] = new List<string>();
                    if (!stationLineIds[stn.stationId].Contains(line.lineId))
                        stationLineIds[stn.stationId].Add(line.lineId);
                }
            }

            foreach (var kv in _stationViewMap)
            {
                var view = kv.Value;
                stationLineIds.TryGetValue(kv.Key, out var lineIds);
                lineIds ??= new List<string>();

                bool isOnCurrentLine = currentLineId != null && lineIds.Contains(currentLineId);
                bool anyActive       = lineIds.Any(id => active.Contains(id));
                view.SetInactiveOverlayVisible(!anyActive && !isOnCurrentLine);
                view.ApplyLineRingColors(lineId =>
                {
                    bool showColor = active.Contains(lineId) || isOnCurrentLine;
                    if (!showColor) return inactiveLineColor;
                    var lineData = networkData.lines.FirstOrDefault(l => l != null && l.lineId == lineId);
                    return lineData != null ? lineData.lineColor : inactiveLineColor;
                });
            }

            RefreshLineHighlight(currentLineId);
        }

        public void RefreshLineHighlight(string currentLineId)
        {
            var hlRT = FindContainer(LineHLTag);

            if (hlRT != null)
                for (int i = hlRT.childCount - 1; i >= 0; i--)
                    Destroy(hlRT.GetChild(i).gameObject);

            if (string.IsNullOrEmpty(currentLineId)) return;

            var lineData = networkData != null
                ? networkData.lines.FirstOrDefault(l => l != null && l.lineId == currentLineId)
                : null;
            if (lineData == null) return;

            if (hlRT == null) hlRT = CreateContainer(LineHLTag, siblingIndex: 0);
            else hlRT.SetSiblingIndex(0);

            var hlColor = new Color(lineData.lineColor.r, lineData.lineColor.g, lineData.lineColor.b, 0.35f);

            foreach (var seg in _bezierLines)
            {
                if (seg == null || seg.lineId != currentLineId) continue;
                var clone = Instantiate(seg.gameObject, hlRT);
                var cl = clone.GetComponent<UIBezierLine>();
                cl.color = hlColor;
                cl.SetWidthScale(_zoomComp * 2f);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 내부 헬퍼
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        void RebuildBezierCache(RectTransform linesRT)
        {
            _bezierLines.Clear();
            _segmentMap.Clear();
            foreach (var line in linesRT.GetComponentsInChildren<UIBezierLine>())
            {
                if (line == null) continue;
                _bezierLines.Add(line);
                AddToSegmentMap(line.stationA, line.stationB, line);
            }
        }

        void AddToSegmentMap(string a, string b, UIBezierLine line)
        {
            void Add((string, string) key)
            {
                if (!_segmentMap.TryGetValue(key, out var list))
                    _segmentMap[key] = list = new List<UIBezierLine>();
                if (!list.Contains(line)) list.Add(line);
            }
            Add((a, b));
            Add((b, a));
        }

        void RebuildStationCache(RectTransform stationsRT)
        {
            var dataById = new Dictionary<string, StationData>();
            if (networkData != null)
                foreach (var line in networkData.lines)
                    if (line != null)
                        foreach (var stn in line.stations)
                            if (stn != null && !dataById.ContainsKey(stn.stationId))
                                dataById[stn.stationId] = stn;

            _stationViews.Clear();
            _stationViewMap.Clear();
            _stationRTMap.Clear();
            foreach (var v in stationsRT.GetComponentsInChildren<StationView>())
            {
                if (v == null) continue;

                var n = v.gameObject.name;
                if (n.StartsWith("Stn_") && dataById.TryGetValue(n.Substring(4), out var sd))
                    v.stationData = sd;

                if (v.stationData == null) continue;
                _stationViews.Add(v);
                _stationViewMap[v.stationData.stationId] = v;
                var rt = v.GetComponent<RectTransform>();
                if (rt != null) _stationRTMap[v.stationData.stationId] = rt;
            }
        }

        Dictionary<string, Vector2> GetPosMap()
        {
            if (_posMap != null) return _posMap;
            if (_stationRTMap.Count == 0)
            {
                var stationsRT = FindContainer(StationsTag);
                if (stationsRT != null) RebuildStationCache(stationsRT);
            }
            _posMap = new Dictionary<string, Vector2>(_stationRTMap.Count);
            foreach (var kv in _stationRTMap)
                _posMap[kv.Key] = kv.Value.anchoredPosition;
            return _posMap;
        }

        public Vector2? GetStationUIPos(string stationId)
        {
            var posMap = GetPosMap();
            return posMap.TryGetValue(stationId, out var pos) ? pos : (Vector2?)null;
        }

        // ── 마커 그리기 ──────────────────────────────────────────────────

        void DrawPlayer(Vector2 uiPos)
        {
            if (!Application.isPlaying)
            {
                if (playerMarkerPrefab != null)
                    Instantiate(playerMarkerPrefab, mapContainer).GetComponent<RectTransform>()
                               .anchoredPosition = uiPos;
                else
                    Circ("Player", mapContainer, uiPos, 32f, PlayerFallbackColor);
                return;
            }

            if (_playerMarker == null) _playerMarker = FindContainer(PlayerTag);
            if (_playerMarker == null)
            {
                _playerMarker = CreateContainer(PlayerTag, mapContainer.childCount);

                if (playerMarkerPrefab != null)
                {
                    var inst   = Instantiate(playerMarkerPrefab, _playerMarker);
                    var instRT = inst.GetComponent<RectTransform>();
                    if (instRT != null) { instRT.anchoredPosition = Vector2.zero; instRT.sizeDelta = MarkerSize; }
                    foreach (var img in inst.GetComponentsInChildren<Image>(true))
                        if (img.name == "Border")
                        {
                            _playerBorder = img;
                            var brt = img.GetComponent<RectTransform>();
                            if (brt != null) brt.sizeDelta = MarkerSize;
                            break;
                        }
                }
                else
                {
                    Circ("PlayerOutline", _playerMarker, Vector2.zero, markerWidth + 4f, Color.white);
                    Circ("Player",        _playerMarker, Vector2.zero, markerWidth,       PlayerFallbackColor);
                }

                _playerMarker.anchoredPosition = uiPos;
                _playerMarker.localScale = Vector3.one * _zoomComp;
                _playerBasePos = uiPos;
            }
            _playerMarker.SetSiblingIndex(mapContainer.childCount - 1);
            _playerTarget = uiPos;
        }

        void DrawTracker(Vector2 uiPos, string stationId, int index)
        {
            // 외부 앵커 (역 위치 고정, 줌 보정 적용 대상)
            var outer   = new GameObject($"Enemy_{index}");
            outer.transform.SetParent(mapContainer, false);
            var outerRT = outer.AddComponent<RectTransform>();
            outerRT.anchorMin = outerRT.anchorMax = outerRT.pivot = Vector2.one * 0.5f;
            outerRT.anchoredPosition = uiPos;
            outerRT.sizeDelta        = Vector2.zero;
            outerRT.localScale       = Vector3.one * _zoomComp;
            _enemyOuterRTs.Add(outerRT);

            // 내부 밥 컨테이너 (위아래 애니메이션)
            var bobGO = new GameObject("Bob");
            bobGO.transform.SetParent(outer.transform, false);
            var bobRT = bobGO.AddComponent<RectTransform>();
            bobRT.anchorMin = bobRT.anchorMax = bobRT.pivot = Vector2.one * 0.5f;
            bobRT.anchoredPosition = Vector2.zero;
            bobRT.sizeDelta        = Vector2.zero;
            _enemyBobRTs.Add(bobRT);

            if (trackerMarkerPrefab != null)
            {
                var inst   = Instantiate(trackerMarkerPrefab, bobRT);
                var instRT = inst.GetComponent<RectTransform>();
                if (instRT != null) { instRT.anchoredPosition = Vector2.zero; instRT.sizeDelta = MarkerSize; }
            }
            else
            {
                Circ("Ring",    bobRT, Vector2.zero, markerWidth + 12f, new Color(0.95f, 0.18f, 0.18f, 0.30f));
                Circ("Outline", bobRT, Vector2.zero, markerWidth + 4f,  Color.white);
                Circ("Dot",     bobRT, Vector2.zero, markerWidth,        TrackerFallbackColor);
            }

            // 거리 텍스트
            int dist = Graph != null && playerLocation != null
                ? Graph.Distance(stationId, playerLocation.currentStationId)
                : int.MaxValue;
            var txtGO = new GameObject("Dist");
            txtGO.transform.SetParent(bobRT, false);
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = txtRT.anchorMax = txtRT.pivot = new Vector2(0.5f, 0f);
            txtRT.anchoredPosition = new Vector2(0f, 18f);
            txtRT.sizeDelta = new Vector2(36f, 18f);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text          = dist == int.MaxValue ? "?" : dist.ToString();
            tmp.fontSize      = 13f;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.color         = Color.black;
            tmp.raycastTarget = false;
        }

        // ── Update: 글라이드 + 밥 + 노선 색 갱신 ────────────────────────

        void Update()
        {
            if (!Application.isPlaying) return;

            // 플레이어 글라이드 + 밥
            if (_playerMarker != null)
            {
                float k = 1f - Mathf.Exp(-PlayerGlideSharpness * Time.deltaTime);
                _playerBasePos = Vector2.Lerp(_playerBasePos, _playerTarget, k);
                // sin [-1,1] → [0,1] 로 변환해 원래 위치 아래로 안 내려감
                float playerBob = (Mathf.Sin(Time.time * bobSpeed) + 1f) * 0.5f * bobAmplitude;
                _playerMarker.anchoredPosition = _playerBasePos + Vector2.up * playerBob;

                // 노선 색 테두리 갱신
                if (_playerBorder != null)
                {
                    var core   = Game.Core.GameCore.Instance;
                    string lid = core?.Player?.CurrentLineId;
                    if (lid != null && networkData != null)
                    {
                        var ld = networkData.lines.FirstOrDefault(l => l != null && l.lineId == lid);
                        if (ld != null) _playerBorder.color = ld.lineColor;
                    }
                }
            }

            // 추격자 밥 (위상 엇갈림으로 동기화 방지, 위쪽으로만)
            for (int i = 0; i < _enemyBobRTs.Count; i++)
            {
                var bobRT = _enemyBobRTs[i];
                if (bobRT == null) continue;
                float phase = i * Mathf.PI * 0.71f;
                float yBob  = (Mathf.Sin(Time.time * bobSpeed + phase) + 1f) * 0.5f * bobAmplitude;
                bobRT.anchoredPosition = new Vector2(0f, yBob);
            }


        }

        // ── UI 헬퍼 ──────────────────────────────────────────────────────

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

        RectTransform CreateContainer(string containerName, int siblingIndex)
        {
            var go = new GameObject(containerName);
            go.transform.SetParent(mapContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * 0.5f;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;
            go.transform.SetSiblingIndex(siblingIndex);
            _containerCache[containerName] = rt;
            return rt;
        }

        RectTransform FindContainer(string containerName)
        {
            if (_containerCache.TryGetValue(containerName, out var cached) && cached != null)
                return cached;
            var t  = mapContainer.Find(containerName);
            var rt = t != null ? t.GetComponent<RectTransform>() : null;
            if (rt != null) _containerCache[containerName] = rt;
            return rt;
        }

        void DestroyContainer(string containerName)
        {
            if (!_containerCache.TryGetValue(containerName, out var rt))
            {
                var t = mapContainer.Find(containerName);
                if (t == null) return;
                rt = t.GetComponent<RectTransform>();
            }
            _containerCache.Remove(containerName);
            if (rt == null) return;
            if (Application.isPlaying) Destroy(rt.gameObject);
            else DestroyImmediate(rt.gameObject);
        }

        static Sprite MakeCircleSprite(int radius)
        {
            int size   = radius * 2;
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
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
