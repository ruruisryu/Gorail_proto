using UnityEngine;
using Game.Data;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// ⑧ 검문 시스템(§8). 같은 역에서 Tracker와 만나면 확률 게이트로 통과/게임오버를 판정한다.
    ///
    /// 이 슬라이스에서 검문은 결정론(마스크 보유)이 아니라 config 통과 확률로 추상화한다(§8-2) —
    /// 검증 목표가 "통과율 몇 %에서 긴장이 사는가"이기 때문. 통과 시 해당 Tracker는 어그로 해제로
    /// 맵에서 제거한다. 중간역 검문 발동 여부(§8-1)는 TurnResolver가 토글로 제어하므로,
    /// 여기서는 "같은 역인가 → 굴림"만 책임진다.
    /// </summary>
    public class InspectionSystem : MonoBehaviour, IInspection
    {
        [SerializeField] private TrackerManager trackerManager;
        [SerializeField] private GameManager    gameManager;
        [SerializeField] private ChaseConfig    config;

        /// <summary>검문이 발동·판정될 때 발생(stationId, 통과여부). 통계·연출용.</summary>
        public event System.Action<string, bool> InspectionResolved;

        /// <returns>검문 실패로 게임오버이면 true. 만남이 없거나 통과하면 false.</returns>
        public bool ResolveAt(string stationId)
        {
            if (trackerManager == null || string.IsNullOrEmpty(stationId)) return false;
            if (!trackerManager.HasTrackerAt(stationId)) return false; // 같은 역 아님 → 검문 없음

            float passRate = config != null ? config.inspectionPassRate : 0.7f;
            bool passed = Random.value <= passRate;

            if (passed)
            {
                // 어그로 해제 — 만난 Tracker 제거(§8-2)
                trackerManager.RemoveTrackersAt(stationId);
                Debug.Log($"[InspectionSystem] 검문 통과 @ {stationId} (통과율 {passRate:P0})");
                InspectionResolved?.Invoke(stationId, true);
                return false;
            }

            Debug.Log($"[InspectionSystem] 검문 실패 @ {stationId} → 게임오버");
            InspectionResolved?.Invoke(stationId, false);
            if (gameManager != null) gameManager.TriggerGameOver($"검문 실패 @ {stationId}");
            return true;
        }
    }
}
