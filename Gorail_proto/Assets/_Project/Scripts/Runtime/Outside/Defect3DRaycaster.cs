using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.UI
{
    /// <summary>
    /// 화면에 직접 렌더되는 카메라 기준으로 마우스 레이를 쏴서 Defect3D를 호버/클릭 처리한다.
    /// 결함이 RenderTexture(RawImage)로 표시되는 방식이라면 좌표 변환이 필요하므로 이 버전은 직접 렌더 전용.
    /// </summary>
    public class Defect3DRaycaster : MonoBehaviour
    {
        [Tooltip("레이를 쏠 카메라(픽셀 viewCamera 등 화면에 그리는 카메라). 비우면 Camera.main.")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("레이 최대 거리.")]
        [SerializeField] private float maxDistance = 1000f;
        [Tooltip("결함 콜라이더가 있는 레이어만(기본 전체).")]
        [SerializeField] private LayerMask hitMask = ~0;

        Defect3D _hovered;
        Collider _lastLoggedCol;

        void Awake() { if (viewCamera == null) viewCamera = Camera.main; }

        void Update()
        {
            if (viewCamera == null) return;

            // 작품활동 패널 등 UI 위에 마우스가 있으면 3D 결함 무시 → UI 우선
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            { SetHover(null); return; }

            Vector2 mp; bool click;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) { SetHover(null); return; }
            mp = mouse.position.ReadValue();
            click = mouse.leftButton.wasPressedThisFrame;
#else
            mp = (Vector2)Input.mousePosition;
            click = Input.GetMouseButtonDown(0);
#endif

            Defect3D target = null;
            var ray = viewCamera.ScreenPointToRay(mp);
            Collider hitCol = null;
            if (Physics.Raycast(ray, out var hit, maxDistance, hitMask))
            {
                hitCol = hit.collider;
                target = hit.collider.GetComponentInParent<Defect3D>();
            }
            if (hitCol != _lastLoggedCol)
            {
                _lastLoggedCol = hitCol;
                Debug.Log($"[DefectRaycaster] 레이 충돌: {(hitCol != null ? hitCol.name : "— 없음")}, Defect3D: {(target != null ? "찾음 O" : "없음 X")}");
            }

            SetHover(target);
            if (target != null && click) target.Click();
        }

        void SetHover(Defect3D d)
        {
            if (_hovered == d) return;
            if (_hovered != null) _hovered.SetHover(false);
            _hovered = d;
            if (_hovered != null) _hovered.SetHover(true);
        }
    }
}