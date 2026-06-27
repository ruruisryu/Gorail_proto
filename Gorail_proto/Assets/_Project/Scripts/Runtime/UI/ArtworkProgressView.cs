using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Gameplay;
using Game.Inventory;   // RadialGauge

namespace Game.UI
{
    /// <summary>
    /// 작품활동 중 연출(외부IP §4) — 원형 진행 게이지 + 경과 시간 + 추격자 접근 표시.
    ///
    /// 게이지는 게임 시간 틱이 아니라 "연출"의 고정 실시간으로 차오른다(§5 로직/연출 분리):
    ///   0 → 90% : fastSeconds(빠르게)   /   90 → 100% : slowSeconds(급격히 느리게, 완료 직전 긴장감)
    /// 성패는 로직(ArtworkSystem)이 정한다:
    ///   추격자 도달(interrupted) → 즉시 실패로 패널 닫힘
    ///   게이지 완료 + 성공         → 패널 닫힘  (이후 결과 연출은 팀원 구현이 담당)
    ///
    /// 추격자 접근(§4-2)은 MapGraph.Distance로 가장 가까운 추격자까지의 역 수를 실시간 표시.
    /// (좌우 점 슬라이더 아트는 기획서상 변경 가능 표기 → 확정 후 교체)
    /// </summary>
    public class ArtworkProgressView : MonoBehaviour
    {
        [Header("표시 (한글 폰트 필요)")]
        [SerializeField] private TMP_FontAsset font;

        [Header("원형 진행 게이지")]
        [Tooltip("Unity 기본 Circle 스프라이트 등 흰 원. Create ▸ 2D ▸ Sprites ▸ Circle")]
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Color gaugeTrack = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color gaugeFill  = new Color(0.35f, 0.85f, 0.50f, 1f);
        [SerializeField] private float gaugeSize  = 160f;

        [Header("게이지 연출 속도(§4-1)")]
        [Tooltip("0→이 비율까지 균일하게 빠르게 차오르는 데 걸리는 실시간(초).")]
        [SerializeField] private float fastSeconds = 3f;
        [Tooltip("위 비율→100%까지 느리게 차오르는 데 걸리는 실시간(초, 완료 직전 긴장감).")]
        [SerializeField] private float slowSeconds = 4f;
        [Tooltip("빠른 구간이 끝나는 지점(0~1). 0.80이면 0→80% 빠르게, 80→100% 느리게.")]
        [Range(0.5f, 0.95f)]
        [SerializeField] private float fastEndPercent = 0.80f;

        [Header("추격자 접근 표시(§4-2)")]
        [Tooltip("이 역 수 이내의 추격자만 표시.")]
        [SerializeField] private int trackerRange = 3;
        [SerializeField] private Color warnColor = new Color(0.95f, 0.30f, 0.30f, 1f);

        [Header("배경 어둡게(연출 집중)")]
        [SerializeField] private Color dimColor = new Color(0.06f, 0.09f, 0.09f, 0.96f);

        private GameObject _ui;
        private RadialGauge _gauge;
        private TextMeshProUGUI _titleTxt, _elapsedTxt, _trackerTxt;

        private bool  _running;
        private float _animT;
        private bool  _logicDone, _interrupted, _success;
        private int   _elapsedMin;

        ArtworkSystem Artwork => GameCore.Instance?.Artwork;
        float TotalAnimSeconds => fastSeconds + slowSeconds;

        void Start()
        {
            Debug.Log($"[ArtworkProgress] Start 실행 (Artwork={(Artwork != null ? "있음" : "없음")})");
            BuildUI();
            _ui.SetActive(false);

            var aw = Artwork;
            if (aw != null)
            {
                aw.ArtworkStarted  += OnStarted;
                aw.ProgressTicked  += OnTicked;
                aw.ArtworkFinished += OnFinished;
                Debug.Log("[ArtworkProgress] ArtworkSystem 이벤트 구독 완료");
            }
            else Debug.LogWarning("[ArtworkProgress] GameCore.Artwork 없음 — 진행 게이지가 표시되지 않습니다.");
        }

        void OnDestroy()
        {
            var aw = Artwork;
            if (aw != null)
            {
                aw.ArtworkStarted  -= OnStarted;
                aw.ProgressTicked  -= OnTicked;
                aw.ArtworkFinished -= OnFinished;
            }
        }

        void OnStarted()
        {
            _ui.SetActive(true);
            _running     = true;
            _animT       = 0f;
            _logicDone   = false;
            _interrupted = false;
            _success     = false;
            _elapsedMin  = 0;

            _titleTxt.text   = ResolveTitle();
            _elapsedTxt.text = "0분 경과";
            _trackerTxt.text = "";
            _gauge.SetValue(0f, "0%");
            Debug.Log("[ArtworkProgress] 진행 게이지 표시 시작");
        }

        // 게임 시간(분)은 경과 시간 텍스트에만 사용(§4-1 ③). 게이지 속도와는 무관.
        void OnTicked(int elapsed, int total) => _elapsedMin = elapsed;

        void OnFinished(bool succeeded, float fameGain, bool interrupted)
        {
            _logicDone   = true;
            _interrupted = interrupted;
            _success     = succeeded && !interrupted;
            // 실제 닫기는 Update가 연출 타이밍에 맞춰 처리(성공이면 게이지 100% 후).
        }

        void Update()
        {
            if (!_running) return;

            // ── 게이지: 고정 실시간 연출(§4-1) ──
            _animT += Time.deltaTime;
            float fill = CurveFill(_animT);
            _gauge.SetValue(fill, $"{fill:P0}");
            _elapsedTxt.text = $"{_elapsedMin}분 경과";

            // ── 추격자 접근(§4-2) ──
            UpdateTrackerText();

            // ── 종료 판정 ──
            if (_interrupted) { EndShow("추격자 도달 → 실패"); return; }
            if (_animT >= TotalAnimSeconds && _logicDone && _success) EndShow("게이지 완료 → 성공");
        }

        void EndShow(string why)
        {
            _running = false;
            _ui.SetActive(false);
            Debug.Log($"[ArtworkProgress] 진행 게이지 종료 ({why})");
        }

        /// <summary>경과 연출 시간을 게이지 채움(0~1)으로. 0→fastEndPercent는 fastSeconds(균일), 그 이후는 slowSeconds(§4-1).</summary>
        float CurveFill(float t)
        {
            if (fastSeconds <= 0f) return 1f;
            if (t < fastSeconds) return fastEndPercent * (t / fastSeconds);
            if (slowSeconds <= 0f) return 1f;
            if (t < fastSeconds + slowSeconds)
                return fastEndPercent + (1f - fastEndPercent) * ((t - fastSeconds) / slowSeconds);
            return 1f;
        }

        /// <summary>가장 가까운 추격자까지의 역 수를 실시간 표시. 도달 시 경고(§4-1⑤·§4-2).</summary>
        void UpdateTrackerText()
        {
            var core   = GameCore.Instance;
            var graph  = core?.Graph?.Graph;
            var list   = core?.Trackers?.Trackers;
            string me  = core?.Space?.CurrentStationId;
            if (graph == null || list == null || string.IsNullOrEmpty(me)) { _trackerTxt.text = ""; return; }

            int closest = int.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                int d = graph.Distance(list[i].StationId, me);
                if (d >= 0 && d < closest) closest = d;
            }

            if (closest == int.MaxValue || closest > trackerRange)
            {
                _trackerTxt.text  = "추격자 접근 없음";
                _trackerTxt.color = new Color(1f, 1f, 1f, 0.6f);
            }
            else if (closest <= 0)
            {
                _trackerTxt.text  = "⚠ 추격자 현재 역 도착! 최대한 빨리 승강장으로 복귀하세요";
                _trackerTxt.color = warnColor;
            }
            else
            {
                _trackerTxt.text  = $"추격자 {closest}역 접근 중";
                _trackerTxt.color = closest <= 1 ? warnColor : new Color(1f, 0.85f, 0.4f, 1f);
            }
        }

        /// <summary>진행 중 타이틀 = 현재 역 IP명(없으면 역명).</summary>
        string ResolveTitle()
        {
            var core = GameCore.Instance;
            var st = core?.Graph?.Graph?.GetStation(core?.Space?.CurrentStationId);
            if (st == null) return "작품 활동 중";
            if (st.ipCanvas != null && !string.IsNullOrEmpty(st.ipCanvas.displayName))
                return st.ipCanvas.displayName;
            return string.IsNullOrEmpty(st.displayName) ? "작품 활동 중" : st.displayName;
        }

        // ── UI 생성 ──────────────────────────────────────────────
        void BuildUI()
        {
            // 부모 Canvas 밑에 전체화면 UI를 깐다(이 컴포넌트가 일반 Transform 위에 있어도 안전).
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;
            if (canvas == null)
                Debug.LogWarning("[ArtworkProgress] 부모에 Canvas가 없습니다 — ArtworkCanvas 밑에 두세요.");

            _ui = NewUI("ArtworkProgress", parent, out var root);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.anchoredPosition = Vector2.zero;
            root.localScale = Vector3.one;

            var dim = _ui.AddComponent<Image>();
            dim.color = dimColor; dim.raycastTarget = true;

            _titleTxt = NewText("Title", root, 30, new Vector2(0.5f, 0.5f), new Vector2(0, gaugeSize * 0.5f + 70f));
            _titleTxt.text = "작품 활동 중";

            var gGO = NewUI("Gauge", root, out var grt);
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
            grt.anchoredPosition = Vector2.zero;
            _gauge = gGO.AddComponent<RadialGauge>();
            _gauge.Build(circleSprite, gaugeTrack, gaugeFill, gaugeSize, font, 30f);

            _elapsedTxt = NewText("Elapsed", root, 22, new Vector2(0.5f, 0.5f), new Vector2(0, -(gaugeSize * 0.5f + 44f)));
            _elapsedTxt.text = "0분 경과";

            _trackerTxt = NewText("Tracker", root, 22, new Vector2(0.5f, 0f), new Vector2(0, 60f));
            _trackerTxt.text = "";
        }

        GameObject NewUI(string name, Transform parent, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            rt = (RectTransform)go.transform;
            return go;
        }

        TextMeshProUGUI NewText(string name, Transform parent, float size, Vector2 anchor, Vector2 pos)
        {
            var go = NewUI(name, parent, out var rt);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700, size + 12);
            rt.anchoredPosition = pos;
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }
    }
}