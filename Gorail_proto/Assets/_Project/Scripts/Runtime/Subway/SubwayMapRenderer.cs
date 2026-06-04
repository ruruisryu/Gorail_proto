using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Subway
{
    public class SubwayMapRenderer : MonoBehaviour
    {
        [SerializeField] private SubwayNetworkData networkData;
        [SerializeField] private PlayerLocationData playerLocation;
        [SerializeField] private EnemyLocationData enemyLocations;
        [SerializeField] private RectTransform mapContainer;

        private const float RefWidth       = 860f;
        private const float RefHeight      = 550f;

        [Header("Layout")]
        [Tooltip("역 간 간격 배율. 1 = 원본, 0.5 = 절반으로 압축.")]
        [SerializeField][Range(0.1f, 1f)] private float stationSpread = 0.4f;
        private const float LineThickness  = 6f;
        private const float StationSize    = 12f;
        private const float TransferDot    = 11f;
        private const float TransferPad    = 3f;
        private const float PlayerSize     = 18f;
        private const float EnemySize      = 18f;

        private static readonly Color PlayerColor     = new Color(0.22f, 0.92f, 0.42f);
        private static readonly Color PlayerRingColor = new Color(0.22f, 0.92f, 0.42f, 0.30f);
        private static readonly Color EnemyColor      = new Color(0.95f, 0.18f, 0.18f);
        private static readonly Color EnemyRingColor  = new Color(0.95f, 0.18f, 0.18f, 0.30f);

        // Render() 후 채워지는 역 UI 좌표 경계 (MapContent 로컬 기준)
        public Vector2 StationBoundsMin { get; private set; }
        public Vector2 StationBoundsMax { get; private set; }

        [Header("Fonts")]
        [SerializeField] private TMP_FontAsset fontBold;    // 환승역 — Korail B SDF
        [SerializeField] private TMP_FontAsset fontMedium;  // 일반역 — Korail M SDF

        private Sprite circle;

        void Awake()
        {
            circle = MakeCircleSprite(128);
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            if (mapContainer == null || networkData == null) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || mapContainer == null || networkData == null) return;
                circle = MakeCircleSprite(128);
                Render();
            };
#endif
        }

        static Sprite MakeCircleSprite(int radius)
        {
            int size = radius * 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float cr = radius - 0.5f;
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

        public void Render()
        {
            for (int i = mapContainer.childCount - 1; i >= 0; i--)
            {
                var child = mapContainer.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            if (networkData == null) return;

            // 역별 노선 색상 목록 수집
            var stationLines = new Dictionary<string, List<Color>>();
            foreach (var line in networkData.lines)
            {
                foreach (var stn in line.stations)
                {
                    if (stn == null) continue;
                    if (!stationLines.ContainsKey(stn.stationId))
                        stationLines[stn.stationId] = new List<Color>();
                    if (!stationLines[stn.stationId].Contains(line.lineColor))
                        stationLines[stn.stationId].Add(line.lineColor);
                }
            }

            // 역 UI 좌표 경계 계산
            var bMin = new Vector2(float.MaxValue,  float.MaxValue);
            var bMax = new Vector2(float.MinValue, float.MinValue);
            var seenForBounds = new HashSet<string>();
            foreach (var line in networkData.lines)
                foreach (var stn in line.stations)
                {
                    if (stn == null || seenForBounds.Contains(stn.stationId)) continue;
                    seenForBounds.Add(stn.stationId);
                    var p = UI(stn.mapPosition);
                    bMin = Vector2.Min(bMin, p);
                    bMax = Vector2.Max(bMax, p);
                }
            StationBoundsMin = bMin;
            StationBoundsMax = bMax;

            // 1. 노선 선분
            foreach (var line in networkData.lines)
                DrawLineSegments(line);

            // 2. 역 마커 (중복 없이)
            var drawn = new HashSet<string>();
            foreach (var line in networkData.lines)
            {
                foreach (var stn in line.stations)
                {
                    if (stn == null || drawn.Contains(stn.stationId)) continue;
                    drawn.Add(stn.stationId);
                    var colors = stationLines[stn.stationId];
                    DrawStation(stn, colors);
                }
            }

            // 3. 적 마커
            if (enemyLocations != null)
            {
                int enemyIndex = 0;
                foreach (var id in enemyLocations.enemyStationIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    var stn = FindStation(id);
                    if (stn != null) DrawEnemy(stn.mapPosition, enemyIndex);
                    enemyIndex++;
                }
            }

            // 4. 플레이어 마커 (항상 최상단)
            if (playerLocation != null && !string.IsNullOrEmpty(playerLocation.currentStationId))
            {
                var stn = FindStation(playerLocation.currentStationId);
                if (stn != null) DrawPlayer(stn.mapPosition);
            }
        }

        // ── 선분 ──────────────────────────────────────────────────────

        void DrawLineSegments(LineData line)
        {
            var s = line.stations;
            for (int i = 0; i < s.Count - 1; i++)
            {
                if (s[i] == null || s[i + 1] == null) continue;
                Segment(s[i].mapPosition, s[i + 1].mapPosition, line.lineColor, LineThickness);
            }
            if (line.isCircular && s.Count > 1 && s[0] != null && s[s.Count - 1] != null)
                Segment(s[s.Count - 1].mapPosition, s[0].mapPosition, line.lineColor, LineThickness);
        }

        void Segment(Vector2 from, Vector2 to, Color color, float thickness)
        {
            var go  = new GameObject("Seg");
            go.transform.SetParent(mapContainer, false);
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;

            var rt = img.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot      = new Vector2(0f, 0.5f);

            Vector2 uiFrom = UI(from), uiTo = UI(to);
            Vector2 dir    = uiTo - uiFrom;
            rt.anchoredPosition = uiFrom;
            rt.sizeDelta        = new Vector2(dir.magnitude, thickness);
            rt.localRotation    = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        // ── 역 마커 ───────────────────────────────────────────────────

        void DrawStation(StationData stn, List<Color> lineColors)
        {
            Vector2 uiPos      = UI(stn.mapPosition);
            bool    isTransfer = lineColors.Count > 1;

            GameObject markerGO;

            if (!isTransfer)
            {
                // 흰 테두리 + 색상 원
                Circ("StnBg_" + stn.stationId, mapContainer, uiPos, StationSize + 4f, Color.white);
                markerGO = Circ("Stn_" + stn.stationId, mapContainer, uiPos, StationSize, lineColors[0]);
            }
            else
            {
                int   n       = lineColors.Count;
                float spacing = TransferDot * 0.85f;
                float totalW  = TransferDot + (n - 1) * spacing;
                float startX  = -(totalW * 0.5f) + TransferDot * 0.5f;

                // 사각형 배경 없이 각 점마다 흰 테두리 원 + 색상 원
                markerGO = new GameObject("Stn_" + stn.stationId);
                markerGO.transform.SetParent(mapContainer, false);
                var rt = markerGO.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * 0.5f;
                rt.anchoredPosition = uiPos;
                rt.sizeDelta        = Vector2.zero;

                for (int i = 0; i < n; i++)
                {
                    var offset = new Vector2(startX + i * spacing, 0f);
                    Circ($"DotBg_{i}", markerGO.transform, offset, TransferDot + 4f, Color.white);
                    Circ($"Dot_{i}",   markerGO.transform, offset, TransferDot, lineColors[i]);
                }
            }

            DrawLabel(stn, markerGO.transform, isTransfer);
        }

        void DrawLabel(StationData stn, Transform parent, bool isTransfer)
        {
            var lbl = new GameObject("Label");
            lbl.transform.SetParent(parent, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin        = lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot            = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -2f);
            lrt.sizeDelta        = new Vector2(80f, 24f);

            var txt = lbl.AddComponent<TextMeshProUGUI>();
            txt.text              = stn.displayName;
            txt.font              = isTransfer ? fontBold : fontMedium;
            txt.fontSize          = isTransfer ? 7f : 6f;
            txt.color             = new Color(0.1f, 0.1f, 0.1f);
            txt.alignment         = TextAlignmentOptions.Top;
            txt.enableWordWrapping = false;
            txt.raycastTarget     = false;
        }

        // ── 플레이어 마커 ──────────────────────────────────────────────

        void DrawPlayer(Vector2 mapPos)
        {
            Vector2 uiPos = UI(mapPos);
            Circ("PlayerRing",    mapContainer, uiPos, PlayerSize + 12f, PlayerRingColor);
            Circ("PlayerOutline", mapContainer, uiPos, PlayerSize + 4f,  Color.white);
            Circ("Player",        mapContainer, uiPos, PlayerSize,        PlayerColor);
        }

        // ── 적 마커 ───────────────────────────────────────────────────

        void DrawEnemy(Vector2 mapPos, int index)
        {
            Vector2 uiPos = UI(mapPos);
            Circ($"EnemyRing_{index}",    mapContainer, uiPos, EnemySize + 12f, EnemyRingColor);
            Circ($"EnemyOutline_{index}", mapContainer, uiPos, EnemySize + 4f,  Color.white);
            Circ($"Enemy_{index}",        mapContainer, uiPos, EnemySize,        EnemyColor);
        }

        // ── 공통 유틸 ──────────────────────────────────────────────────

        GameObject Circ(string name, Transform parent, Vector2 anchoredPos, float size, Color color)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * 0.5f;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = Vector2.one * size;
            var img = go.AddComponent<Image>();
            img.sprite        = circle;
            img.color         = color;
            img.raycastTarget = false;
            return go;
        }

        // 맵 좌표(860×550 기준, y↓) → uGUI 좌표(중앙 앵커, y↑), 실제 컨테이너 크기로 스케일
        // stationSpread: 1=원본 간격, 0.5=절반으로 중앙 압축
        Vector2 UI(Vector2 mapPos)
        {
            Rect r   = mapContainer.rect;
            float sx = r.width  / RefWidth;
            float sy = r.height / RefHeight;

            float cx = RefWidth  * 0.5f;
            float cy = RefHeight * 0.5f;
            float scaledX = cx + (mapPos.x - cx) * stationSpread;
            float scaledY = cy + (mapPos.y - cy) * stationSpread;

            return new Vector2(scaledX * sx - r.width  * 0.5f,
                              -(scaledY * sy) + r.height * 0.5f);
        }

        StationData FindStation(string id)
        {
            foreach (var line in networkData.lines)
                foreach (var stn in line.stations)
                    if (stn != null && stn.stationId == id) return stn;
            return null;
        }
    }
}
