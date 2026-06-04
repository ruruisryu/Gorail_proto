using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 추격 루프 통합 배선(§12). TurnResolver에 실물 시스템(추격·검문·승강장)을 주입한다
    /// (경계 인터페이스 → 실구현 교체). 이 컴포넌트가 없으면 TurnResolver는 Null* 빈 구현으로 동작.
    ///
    /// 하차 스폰(§5-1)은 도착이 아니라 '하차' 시점(PlatformController.OpenAt)에서 일어난다 —
    /// 이동은 지하철 공간에 머물고, 승강장 진입은 하차 버튼으로만.
    /// </summary>
    public class ChaseSession : MonoBehaviour
    {
        [SerializeField] private TurnResolver       turnResolver;
        [SerializeField] private TrackerManager     trackerManager;
        [SerializeField] private InspectionSystem   inspection;
        [SerializeField] private PlatformController platform;

        void Awake()
        {
            if (turnResolver != null)
                turnResolver.SetSystems(trackerManager, inspection, platform);
        }
    }
}
