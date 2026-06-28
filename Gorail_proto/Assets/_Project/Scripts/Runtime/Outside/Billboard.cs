using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 대상을 항상 카메라와 평행(또는 카메라를 바라보게) 회전시키는 빌보드.
    /// 월드 '위치'는 그대로 두고 '회전'만 매 프레임 카메라에 맞춰, 어느 각도에서 봐도 정면 사각형으로 보인다.
    /// → 비스듬히 박힌 스프라이트의 전단(shear)이 사라져 BoxCollider가 정확히 맞고 클릭이 된다.
    /// → 위치는 월드 고정이라 카메라를 패닝하면 건물에 붙은 것처럼 화면을 가로질러 이동한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class Billboard : MonoBehaviour
    {
        [Tooltip("기준 카메라. 비우면 Camera.main을 사용.")]
        [SerializeField] private Camera target;

        [Tooltip("켜면 카메라 '위치'를 바라봄(약간 기울 수 있음). 끄면 카메라 '방향'과 평행 = 스크린 평행(전단 0, 권장).")]
        [SerializeField] private bool faceCameraPosition = false;

        void Awake() { if (target == null) target = Camera.main; }

        void LateUpdate()
        {
            if (target == null) { target = Camera.main; if (target == null) return; }

            if (faceCameraPosition)
                transform.rotation = Quaternion.LookRotation(transform.position - target.transform.position);
            else
                transform.rotation = target.transform.rotation;   // 스크린과 완전 평행
        }

        public void SetTarget(Camera cam) => target = cam;
    }
}