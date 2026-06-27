using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Gameplay;
using Game.Inventory;   // RadialGauge

namespace Game.UI
{
    /// <summary>
    /// 작품활동 중 연출(외부IP §4-1) — 원형 진행 게이지 + 경과 시간 + 활동명.
    /// ArtworkSystem 이벤트에 맞춰 표시되며, 진행률은 5분 틱마다 갱신된다.
    ///
    /// 게이지 채움 곡선(§4-1):
    ///   0 ~ 확정시간(등급 최소치)      → 0% ~ 90%  (확실히 소요되는 구간, 빠르게)
    ///   확정시간 ~ 실제 총 소요시간     → 90% ~ 100% (±오차 랜덤 구간, 느리게)
    /// 소요시간/총량 수치 자체는 표시하지 않는다(§3-5). 경과 시간만 텍스트로.
    ///
    /// 추격자 진행 슬라이더(§4-2)는 추격 시스템 데이터가 필요해 여기서는 미구현(추후).
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

        [Header("배경 어둡게(연출 집중)")]
        [SerializeField] private Color dimColor = new Color(0.06f, 0.09f, 0.09f, 0.96f);

        private GameObject _ui;
        private RadialGauge _gauge;
        private TextMeshProUGUI _titleTxt, _elapsedTxt;

        ArtworkSystem Artwork => GameCore.Instance?.Artwork;

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
            else
            {
                Debug.LogWarning("[ArtworkProgress] GameCore.Artwork 없음 — 진행 게이지가 표시되지 않습니다.");
            }
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
            _titleTxt.text   = ResolveTitle();
            _elapsedTxt.text = "0분 경과";
            _gauge.SetValue(0f, "0%");
            var sz = ((RectTransform)_ui.transform).rect.size;
            Debug.Log($"[ArtworkProgress] 진행 게이지 표시 시작 — _ui active={_ui.activeInHierarchy}, 크기={sz}");
        }

        void OnTicked(int elapsed, int total)
        {
            float fill = DisplayFill(elapsed, total, Artwork != null ? Artwork.ConfirmedMinutes : 0);
            _gauge.SetValue(fill, $"{fill:P0}");
            _elapsedTxt.text = $"{elapsed}분 경과";
        }

        void OnFinished(bool succeeded, float fameGain, bool interrupted)
        {
            Debug.Log($"[ArtworkProgress] 진행 게이지 종료 (성공={succeeded}, 강제실패={interrupted})");
            _ui.SetActive(false);   // 결과 연출은 GroundSceneManager가 담당
        }

        /// <summary>경과 시간을 게이지 채움(0~1)으로. 확정시간까지 0~90%, 그 뒤 90~100%(§4-1).</summary>
        static float DisplayFill(int elapsed, int total, int confirmedMin)
        {
            if (total <= 0) return 0f;
            if (confirmedMin <= 0 || total <= confirmedMin)
                return Mathf.Clamp01((float)elapsed / total);   // 꼬리 없음 → 그냥 선형

            if (elapsed <= confirmedMin)
                return 0.90f * elapsed / confirmedMin;           // 0~90% 빠르게
            return 0.90f + 0.10f * (float)(elapsed - confirmedMin) / (total - confirmedMin); // 90~100% 느리게
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
            // 부모 Canvas 밑에 전체화면 UI를 깐다. 이 컴포넌트가 일반 Transform 위에 있어도 안전
            // (자기 transform을 RectTransform으로 캐스트하지 않음).
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
            rt.sizeDelta = new Vector2(600, size + 12);
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