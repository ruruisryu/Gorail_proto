using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// [H1] 상태 HUD — uGUI/TMP 오버레이. 추격 상태(명성·수배도·추격자 수·최근접 거리·공간·이동중)를
    /// 상시 표시하고, 검문 판정·게임오버를 화면에 띄운다. "검문이 안 보인다"를 직접 해소한다.
    ///
    /// 시스템 각각이 아니라 <see cref="ChaseEvents"/> 허브 하나만 구독한다. UI는 코드로 생성해
    /// 씬 배선·재컴파일 instanceID 변동에 영향받지 않는다(폰트만 인스펙터로 주입).
    /// </summary>
    public class StatusHud : MonoBehaviour
    {
        [Header("폰트(한글 TMP)")]
        [SerializeField] private TMP_FontAsset bodyFont;   // Korail M SDF
        [SerializeField] private TMP_FontAsset titleFont;  // Korail B SDF

        [Header("토스트")]
        [Tooltip("검문 토스트가 보이는 시간(초).")]
        [SerializeField] private float toastSeconds = 2.2f;

        private TextMeshProUGUI _status;   // 좌상단 아래 상태 라인(우상단)
        private TextMeshProUGUI _toast;    // 검문 등 일시 알림
        private TextMeshProUGUI _banner;   // 게임오버 배너
        private float _toastT;
        private Color _toastColor = Color.white;

        void Start()
        {
            BuildUi();
            if (ChaseEvents.Instance != null)
            {
                ChaseEvents.Instance.InspectionResolved += OnInspection;
                ChaseEvents.Instance.GameOver           += OnGameOver;
            }
        }

        void OnDestroy()
        {
            if (ChaseEvents.Instance != null)
            {
                ChaseEvents.Instance.InspectionResolved -= OnInspection;
                ChaseEvents.Instance.GameOver           -= OnGameOver;
            }
        }

        void Update()
        {
            RefreshStatus();

            if (_toastT > 0f)
            {
                _toastT -= Time.deltaTime;
                if (_toast != null)
                {
                    float a = Mathf.Clamp01(_toastT / 0.5f); // 마지막 0.5초 페이드아웃
                    var c = _toastColor; c.a = a;
                    _toast.color = c;
                    if (_toastT <= 0f) _toast.text = "";
                }
            }
        }

        // ── 상태 갱신 ────────────────────────────────────────────────────
        void RefreshStatus()
        {
            if (_status == null) return;
            var core = GameCore.Instance;
            if (core == null) { _status.text = ""; return; }

            int wanted = core.Game != null ? core.Game.WantedLevel : 0;
            float fame = core.Fame != null ? core.Fame.CurrentFame : 0f;
            int trackers = core.Trackers != null ? core.Trackers.Trackers.Count : 0;
            string space = core.Space != null ? SpaceLabel(core.Space.Current) : "?";
            bool moving = core.TurnResolver != null && core.TurnResolver.IsMoving;

            int nearest = int.MaxValue;
            if (core.Graph != null && core.Trackers != null && core.Player != null)
                nearest = ChaseMetrics.NearestDistance(core.Graph.Graph, core.Trackers.Trackers, core.Player.CurrentStationId);
            string near = nearest == int.MaxValue ? "—" : $"{nearest}역";

            // 최근접 거리에 따라 색(가까울수록 빨강)으로 긴장감
            string nearCol = nearest == int.MaxValue ? "#9AA" : nearest <= 1 ? "#FF4438" : nearest <= 3 ? "#FF9A3A" : "#7FE08A";

            _status.text =
                $"<b>{space}</b>   {(moving ? "<color=#FFD23A>이동중</color>" : "")}\n" +
                $"명성 <b>{fame:0}</b>\n" +
                $"수배도 <b>{Stars(wanted)}</b> ({wanted})\n" +
                $"추격자 <b>{trackers}</b>\n" +
                $"최근접 <color={nearCol}><b>{near}</b></color>";
        }

        static string SpaceLabel(Game.Core.Space s) => s switch
        {
            Game.Core.Space.Subway   => "■ 지하철",
            Game.Core.Space.Platform => "■ 승강장",
            Game.Core.Space.Ground   => "■ 지상",
            _ => "?"
        };

        static string Stars(int wanted)
        {
            if (wanted <= 0) return "—";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < wanted; i++) sb.Append('★');
            return sb.ToString();
        }

        // ── 이벤트 → 토스트/배너 ──────────────────────────────────────────
        void OnInspection(string station, bool passed)
        {
            if (passed) ShowToast($"검문 통과 @ {station}", new Color(0.5f, 1f, 0.55f));
            else        ShowToast($"검문 실패 @ {station} — 체포!", new Color(1f, 0.35f, 0.30f));
        }

        void OnGameOver(string reason)
        {
            if (_banner == null) return;
            _banner.text = $"GAME OVER\n<size=50%>{reason}</size>";
            _banner.gameObject.SetActive(true);
        }

        void ShowToast(string msg, Color color)
        {
            if (_toast == null) return;
            _toast.text = msg;
            _toastColor = color;
            _toast.color = color;
            _toastT = toastSeconds;
        }

        // ── UI 생성(코드) ────────────────────────────────────────────────
        void BuildUi()
        {
            var canvasGo = new GameObject("StatusHudCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // 노선도 위, 디버그(IMGUI)와 무관
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRt = (RectTransform)canvasGo.transform;

            // 상태 패널 — 우상단
            _status = MakeText("StatusText", canvasRt, bodyFont, 26f, TextAlignmentOptions.TopRight);
            Anchor(_status.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                   new Vector2(-24, -20), new Vector2(360, 220));

            // 토스트 — 상단 중앙
            _toast = MakeText("Toast", canvasRt, titleFont != null ? titleFont : bodyFont, 38f, TextAlignmentOptions.Top);
            Anchor(_toast.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                   new Vector2(0, -120), new Vector2(900, 60));
            _toast.text = "";
            EnableOutline(_toast);

            // 게임오버 배너 — 화면 중앙(기본 숨김)
            _banner = MakeText("GameOverBanner", canvasRt, titleFont != null ? titleFont : bodyFont, 90f, TextAlignmentOptions.Center);
            Anchor(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   Vector2.zero, new Vector2(1200, 400));
            _banner.color = new Color(1f, 0.3f, 0.25f);
            EnableOutline(_banner);
            _banner.gameObject.SetActive(false);
        }

        TextMeshProUGUI MakeText(string name, RectTransform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        static void EnableOutline(TextMeshProUGUI t)
        {
            // 어떤 배경 위에서도 읽히도록 외곽선
            t.fontMaterial.EnableKeyword("OUTLINE_ON");
            t.outlineColor = new Color(0, 0, 0, 0.9f);
            t.outlineWidth = 0.2f;
        }

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
        }
    }
}
