using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// 개별 추격자(§15 Tracker / §10-2). 현재 역·소속 노선과 추격 1스텝 메서드를 가진다.
    ///
    /// 추격은 두 규칙이 항상 동시·독립으로 작동(§4-1):
    ///   1규칙(속도): 정해진 역 수만큼 플레이어 쪽으로 전진.
    ///   2규칙(경로): 플레이어까지 최단경로 방향으로 이동(노선 제약 없음 — MapGraph BFS).
    /// MonoBehaviour가 아닌 순수 클래스 — TrackerManager가 List로 관리하고 테스트가 쉽다.
    /// </summary>
    public class Tracker
    {
        public string StationId { get; private set; }
        /// <summary>현재 이동 중인 노선(시각화·가시화 §6용). 추격 계산 자체는 노선 비제약.</summary>
        public string LineId { get; private set; }

        public Tracker(string stationId, string lineId)
        {
            StationId = stationId;
            LineId    = lineId;
        }

        /// <summary>
        /// 플레이어를 향해 steps역 전진(1규칙). 매 칸 최단경로 다음 역으로 이동(2규칙).
        /// 이미 같은 역이면 멈춘다(검문은 TurnResolver/InspectionSystem이 판정).
        /// </summary>
        public void ChaseToward(MapGraph graph, string playerStationId, int steps)
        {
            if (graph == null || steps <= 0) return;
            for (int i = 0; i < steps; i++)
            {
                if (StationId == playerStationId) return;       // 따라잡음 — 더 전진 안 함
                string next = graph.NextStepToward(StationId, playerStationId);
                if (string.IsNullOrEmpty(next) || next == StationId) return; // 경로 없음
                string lid = graph.GetConnectingLineId(StationId, next);
                if (!string.IsNullOrEmpty(lid)) LineId = lid;
                StationId = next;
            }
        }

        public void Teleport(string stationId, string lineId)
        {
            StationId = stationId;
            if (!string.IsNullOrEmpty(lineId)) LineId = lineId;
        }
    }
}
