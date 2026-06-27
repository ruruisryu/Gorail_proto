using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// OutsideScene 기본 뷰(§1-1) — uGUI 버전.
    /// 게임이 Screen Space - Overlay 캔버스 중심이라 월드 카메라 배경은 캔버스 뒤에 묻힌다.
    /// 그래서 가로로 긴 배경을 캔버스 Image로 깔고, 우클릭 드래그로 그 Image를 좌우로 민다(클램프).
    /// 결함(§2)은 이 배경 Image의 자식으로 붙이면 배경과 함께 팬된다.
    ///
    /// 구조:
    ///   GroundBaseCanvas (Canvas, Screen Space - Overlay, Sort Order = 서브웨이 위 / 팝업 아래)
    ///     └ Viewport (RectTransform, 전체화면) … 이 컴포넌트
    ///         └ Background (Image, 가로로 긴) … 팬 대상. 결함은 이 밑에 둔다.
    /// </summary>
    public class GroundBaseView : MonoBehaviour
    {
        [Tooltip("팬할 가로 배경 Image(이 오브젝트의 자식). 스프라이트는 현재 IP 것으로 런타임 교체된다.")]
        [SerializeField] private Image background;

        [Tooltip("아직 IP가 지정 안 된 역에서 기본으로 쓸 IP(예: 국중박/NMK).")]
        [SerializeField] private Game.Inventory.IpCanvasData defaultIp;

        [Header("우클릭 드래그 팬")]
        [Tooltip("드래그 감도(클수록 빠르게).")]
        [SerializeField] private float dragSpeed = 1f;
        [Tooltip("드래그 방향 반전. 끄면 '배경을 잡아끄는' 느낌.")]
        [SerializeField] private bool invertDrag = false;

        private RectTransform _self, _bg;
        private float _minX, _maxX;
        private bool _ready;

        /// <summary>현재 역의 IP. 역에 ipCanvas가 없으면 기본 IP(국중박)로 폴백한다.</summary>
        public Game.Inventory.IpCanvasData CurrentIp
        {
            get
            {
                var st = GameCore.Instance?.Graph?.Graph?.GetStation(GameCore.Instance?.Space?.CurrentStationId);
                return (st != null && st.ipCanvas != null) ? st.ipCanvas : defaultIp;
            }
        }

        void Start() => Setup();

        /// <summary>현재 IP의 가로 배경을 싣고 크기/팬 범위를 계산한다.</summary>
        public void Setup()
        {
            _self = (RectTransform)transform;
            if (background == null) { Debug.LogWarning("[GroundBaseView] Background Image 미연결."); _ready = false; return; }
            _bg = background.rectTransform;

            var ip = CurrentIp;
            var sprite = ip != null ? ip.outsideBackground : null;
            Debug.Log($"[GroundBaseView] Setup — IP={(ip != null ? ip.displayName : "null")}, outsideBackground={(sprite != null)}");
            if (sprite != null) background.sprite = sprite;

            if (background.sprite == null)
            {
                _ready = false;
                Debug.LogWarning("[GroundBaseView] IP에 outsideBackground 미지정 — IpCanvasData(또는 기본 IP)에 지정하세요.");
                return;
            }

            // 뷰포트 높이에 맞추고, 스프라이트 비율로 가로 폭 산출
            float vh = _self.rect.height, vw = _self.rect.width;
            if (vh <= 1f) vh = Screen.height;       // 레이아웃 미완 시 폴백
            if (vw <= 1f) vw = Screen.width;
            var sr = background.sprite.rect;
            float aspect = sr.width / Mathf.Max(sr.height, 1f);
            float bw = vh * aspect;

            _bg.anchorMin = _bg.anchorMax = _bg.pivot = new Vector2(0.5f, 0.5f);
            _bg.sizeDelta = new Vector2(bw, vh);
            background.preserveAspect = false;       // 정확히 채움

            float halfOver = Mathf.Max(0f, (bw - vw) * 0.5f);  // 화면 밖으로 넘치는 절반
            _minX = -halfOver; _maxX = halfOver;
            _bg.anchoredPosition = Vector2.zero;
            _ready = true;
            Debug.Log($"[GroundBaseView] 준비 완료 — 배경폭={bw:F0}, 뷰포트={vw:F0}x{vh:F0}, 팬범위 X=±{halfOver:F0}");
        }

        void Update()
        {
            if (!_ready) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed) return;

            float dx = mouse.delta.ReadValue().x;
            if (Mathf.Approximately(dx, 0f)) return;

            float dir = invertDrag ? -1f : 1f;     // 잡아끄는 느낌: 오른쪽 드래그 → 배경 오른쪽으로
            float nx = Mathf.Clamp(_bg.anchoredPosition.x + dir * dx * dragSpeed, _minX, _maxX);
            _bg.anchoredPosition = new Vector2(nx, _bg.anchoredPosition.y);
        }
    }
}