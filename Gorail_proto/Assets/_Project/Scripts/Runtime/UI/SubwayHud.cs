using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 지하철 공간 HUD — "하차" 버튼 (UGUI, 코드 생성).
    /// 지하철 공간 + 이동 중이 아닐 때만 표시.
    /// </summary>
    public class SubwayHud : MonoBehaviour
    {
        [Header("버튼 스타일")]
        [SerializeField] private Vector2 buttonSize    = new Vector2(240f, 48f);
        [SerializeField] private float   bottomMargin  = 24f;
        [SerializeField] private Color   buttonColor   = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        [SerializeField] private Color         textColor    = Color.white;
        [SerializeField] private float         fontSize     = 16f;
        [SerializeField] private TMP_FontAsset font;

        Button      _button;
        TMP_Text    _label;
        GameObject  _canvasGO;

        void Awake()
        {
            BuildUI();
        }

        void Start() => StartCoroutine(LateInit());

        IEnumerator LateInit()
        {
            yield return new WaitUntil(() =>
                GameCore.Instance != null &&
                !string.IsNullOrEmpty(GameCore.Instance.Player?.CurrentStationId));
            SubscribeEvents(true);
            Refresh();
        }

        void OnDestroy()
        {
            SubscribeEvents(false);
            if (_canvasGO != null) Destroy(_canvasGO);
        }

        // ── UI 생성 ──────────────────────────────────────────────────────

        void BuildUI()
        {
            // 전용 Canvas (sort order 15) — 중첩 Canvas 방지를 위해 루트에 생성
            _canvasGO = new GameObject("[SubwayHud]");
            var canvasGO = _canvasGO;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // 버튼 루트
            var btnGO = new GameObject("DisembarkButton");
            btnGO.transform.SetParent(canvasGO.transform, false);

            var bg = btnGO.AddComponent<Image>();
            bg.color = buttonColor;

            _button = btnGO.AddComponent<Button>();
            _button.targetGraphic = bg;
            _button.onClick.AddListener(OnDisembarkClicked);

            var rt = btnGO.GetComponent<RectTransform>();
            rt.sizeDelta        = buttonSize;
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, bottomMargin);

            // 라벨
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);

            _label = labelGO.AddComponent<TextMeshProUGUI>();
            _label.text      = "하차";
            _label.fontSize  = fontSize;
            _label.color     = textColor;
            if (font != null) _label.font = font;
            _label.alignment = TextAlignmentOptions.Center;
            _label.raycastTarget = false;

            var lrt = labelGO.GetComponent<RectTransform>();
            lrt.anchorMin        = Vector2.zero;
            lrt.anchorMax        = Vector2.one;
            lrt.offsetMin        = Vector2.zero;
            lrt.offsetMax        = Vector2.zero;
        }

        // ── 이벤트 구독 ──────────────────────────────────────────────────

        void SubscribeEvents(bool subscribe)
        {
            var core = GameCore.Instance;
            if (core == null) return;

            if (core.Space != null)
            {
                if (subscribe) core.Space.SpaceChanged   += OnSpaceChanged;
                else           core.Space.SpaceChanged   -= OnSpaceChanged;
            }
            if (core.TurnResolver != null)
            {
                if (subscribe) { core.TurnResolver.MoveStarted  += Refresh; core.TurnResolver.MoveCompleted += OnMoveCompleted; }
                else           { core.TurnResolver.MoveStarted  -= Refresh; core.TurnResolver.MoveCompleted -= OnMoveCompleted; }
            }
        }

        void OnSpaceChanged(GameSpace _) => Refresh();
        void OnMoveCompleted(string _, bool __) => Refresh();

        void Refresh()
        {
            var core = GameCore.Instance;
            if (_button == null || core == null) return;

            bool inSubway   = core.Space?.Current == GameSpace.Subway;
            bool isMoving   = core.TurnResolver?.IsMoving ?? false;
            bool hasStation = !string.IsNullOrEmpty(core.Player?.CurrentStationId);
            bool visible    = inSubway && !isMoving && hasStation;

            _button.gameObject.SetActive(visible);

            if (_label != null && hasStation)
                _label.text = $"하차 — {core.Player.CurrentStationId} 승강장";
        }

        // ── 버튼 클릭 ────────────────────────────────────────────────────

        void OnDisembarkClicked()
        {
            var core = GameCore.Instance;
            if (core == null) return;

            SoundManager.Instance?.PlaySFX("지하철_열림");
            string stationId = core.Player?.CurrentStationId;
            if (string.IsNullOrEmpty(stationId)) return;

            var fader = ScreenFader.Instance;
            if (fader != null)
                fader.Fade(onFadeOut: () =>
                {
                    SoundManager.Instance?.PlaySFX("지하철_닫힘");
                    core.Platform?.OpenAt(stationId);
                });
            else
                core.Platform?.OpenAt(stationId);
        }
    }
}
