using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 추격 루프 통합 배선(§12). 빌드 순서상 분리돼 있던 ②~⑧을 런타임에 하나로 잇는다:
    ///   - TurnResolver에 실물 시스템(추격·검문·승강장)을 주입(경계 인터페이스 → 실구현 교체)
    ///   - 도착(하차) 시 TrackerManager 스폰 갱신(§5-1)을 트리거
    ///
    /// 이 컴포넌트가 없으면 TurnResolver는 Null* 빈 구현으로 동작한다(② 단독 검증 상태).
    /// 있으면 한 사이클(이동→추격→검문→승강장)이 완결된다.
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

        void OnEnable()
        {
            if (turnResolver != null) turnResolver.MoveCompleted += OnMoveCompleted;
        }

        void OnDisable()
        {
            if (turnResolver != null) turnResolver.MoveCompleted -= OnMoveCompleted;
        }

        /// <summary>도착 시(게임오버 아님) 하차 스폰 갱신(§5-1). 승강장은 TurnResolver가 이미 연다.</summary>
        void OnMoveCompleted(string stationId, bool gameOver)
        {
            if (gameOver) return;
            if (trackerManager != null) trackerManager.OnPlayerDisembark();
        }
    }
}
