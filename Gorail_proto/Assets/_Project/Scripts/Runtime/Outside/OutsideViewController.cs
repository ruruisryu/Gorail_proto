using UnityEngine;
using Game.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Gameplay
{
    /// <summary>
    /// 외부 IP 씬 시야 조작(§1-1). 우클릭 드래그로 좌우만 회전하며, 시작 방향 기준 ±maxYaw로 클램프한다.
    /// (DDP 밖을 못 보게.) 상하 시야 전환은 없음.
    ///
    /// viewPivot(보통 게임 카메라 또는 그 부모)의 로컬 yaw를 돌린다.
    /// 시작 시점의 yaw를 중심(0)으로 잡으므로, 씬에서 카메라가 DDP를 정면으로 보게 배치해두면 됨.
    /// </summary>
    public class OutsideViewController : MonoBehaviour
    {
        [Tooltip("회전시킬 대상(보통 3D 게임 카메라 또는 그 부모 피벗). 비우면 자기 자신.")]
        [SerializeField] private Transform viewPivot;

        [Tooltip("정면(0) 기준 좌측 한계(도, 음수). 기획자가 IP별로 지정 가능.")]
        [SerializeField] private float yawLeft = -30f;

        [Tooltip("정면(0) 기준 우측 한계(도, 양수).")]
        [SerializeField] private float yawRight = 30f;

        [Tooltip("켜면 시작 시 현재 역 IpCanvasData의 viewYawLeft/Right로 한계를 덮어씀.")]
        [SerializeField] private bool autoReadFromIp = true;

        [Tooltip("드래그 감도(픽셀당 도). 낮을수록 천천히. 휙휙 돌면 더 낮춰.")]
        [SerializeField] private float sensitivity = 0.08f;

        [Tooltip("회전 부드럽게(0=즉시).")]
        [SerializeField] private float smooth = 12f;

        float _centerYaw;   // 시작 정면 yaw
        float _targetYaw;   // 중심 기준 오프셋(-maxYaw ~ +maxYaw)
        float _curYaw;
        bool  _dragging;
        Vector2 _lastPos;

        void Awake()
        {
            if (viewPivot == null) viewPivot = transform;
        }

        void Start()
        {
            _centerYaw = viewPivot.localEulerAngles.y;
            _targetYaw = 0f;
            _curYaw    = 0f;
            if (autoReadFromIp) ReadLimitsFromIp();
        }

        void ReadLimitsFromIp()
        {
            var st = GameCore.Instance?.Graph?.Graph?.GetStation(GameCore.Instance?.Space?.CurrentStationId);
            var ip = st != null ? st.ipCanvas : null;
            if (ip != null) { yawLeft = ip.viewYawLeft; yawRight = ip.viewYawRight; }
        }

        void Update()
        {
            ReadDrag();

            _targetYaw = Mathf.Clamp(_targetYaw, Mathf.Min(yawLeft, yawRight), Mathf.Max(yawLeft, yawRight));
            _curYaw = smooth > 0f
                ? Mathf.Lerp(_curYaw, _targetYaw, 1f - Mathf.Exp(-smooth * Time.deltaTime))
                : _targetYaw;

            var e = viewPivot.localEulerAngles;
            e.y = _centerYaw + _curYaw;
            viewPivot.localEulerAngles = e;
        }

        void ReadDrag()
        {
            bool rightDown, rightHeld;
            Vector2 pos;

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;
            rightDown = mouse.rightButton.wasPressedThisFrame;
            rightHeld = mouse.rightButton.isPressed;
            pos = mouse.position.ReadValue();
#else
            rightDown = Input.GetMouseButtonDown(1);
            rightHeld = Input.GetMouseButton(1);
            pos = (Vector2)Input.mousePosition;
#endif

            if (rightDown) { _dragging = true; _lastPos = pos; }
            if (!rightHeld) { _dragging = false; return; }

            if (_dragging)
            {
                float dx = pos.x - _lastPos.x;
                _lastPos = pos;
                _targetYaw += dx * sensitivity;   // 오른쪽 드래그 → 오른쪽으로 시야 이동
            }
        }

        /// <summary>IP별 시야 한계를 외부에서 주입(정면=0 기준, left≤0≤right 권장).</summary>
        public void SetYawLimits(float left, float right) { yawLeft = left; yawRight = right; }

        /// <summary>현재 보고 있는 각도를 '왼쪽 한계'로 캡처(기획자 튜닝용).</summary>
        [ContextMenu("현재 각도 → 왼쪽 한계로 저장")]
        public void CaptureLeftLimit()
        {
            yawLeft = CurrentOffsetYaw();
            Debug.Log($"[OutsideView] 왼쪽 한계 = {yawLeft:0.0}° (이 값을 IpCanvasData.viewYawLeft에 입력)");
        }

        /// <summary>현재 보고 있는 각도를 '오른쪽 한계'로 캡처(기획자 튜닝용).</summary>
        [ContextMenu("현재 각도 → 오른쪽 한계로 저장")]
        public void CaptureRightLimit()
        {
            yawRight = CurrentOffsetYaw();
            Debug.Log($"[OutsideView] 오른쪽 한계 = {yawRight:0.0}° (이 값을 IpCanvasData.viewYawRight에 입력)");
        }

        [ContextMenu("현재 한계 값 로그로 보기")]
        public void LogLimits()
            => Debug.Log($"[OutsideView] viewYawLeft={yawLeft:0.0}, viewYawRight={yawRight:0.0}");

        // 정면(0) 기준 현재 오프셋 각도(-180~180)
        float CurrentOffsetYaw()
        {
            float raw = viewPivot.localEulerAngles.y - _centerYaw;
            return Mathf.DeltaAngle(0f, raw);
        }

        /// <summary>씬/IP 전환 시 정면을 다시 잡고 싶을 때.</summary>
        public void ResetView()
        {
            _centerYaw = viewPivot.localEulerAngles.y - _curYaw; // 현재 정면 보존
            _targetYaw = 0f;
        }
    }
}