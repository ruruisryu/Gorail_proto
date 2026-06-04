using System.Collections.Generic;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// [버퍼 H1] 추격 상태 계측 — 순수 계산소. 상태 HUD·통계(H2)·연출(H6 최근접 강조)이
    /// 전부 여기서 읽어 중복 계산을 없앤다. MonoBehaviour가 아닌 정적 함수라 테스트가 쉽다.
    ///
    /// "추격 거리가 출렁이는가"(§4-2)를 정량화하는 1차 지표가 최근접 추격자 거리다.
    /// </summary>
    public static class ChaseMetrics
    {
        /// <summary>플레이어까지 최단 거리가 가장 가까운 추격자(없으면 null).</summary>
        public static Tracker NearestTracker(MapGraph graph, IReadOnlyList<Tracker> trackers, string playerStationId)
        {
            if (graph == null || trackers == null || string.IsNullOrEmpty(playerStationId)) return null;
            Tracker best = null;
            int bestD = int.MaxValue;
            foreach (var t in trackers)
            {
                if (t == null) continue;
                int d = graph.Distance(t.StationId, playerStationId);
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        /// <summary>최근접 추격자까지의 거리(역 수). 추격자가 없거나 연결이 없으면 int.MaxValue.</summary>
        public static int NearestDistance(MapGraph graph, IReadOnlyList<Tracker> trackers, string playerStationId)
        {
            if (graph == null || trackers == null || string.IsNullOrEmpty(playerStationId)) return int.MaxValue;
            int bestD = int.MaxValue;
            foreach (var t in trackers)
            {
                if (t == null) continue;
                int d = graph.Distance(t.StationId, playerStationId);
                if (d < bestD) bestD = d;
            }
            return bestD;
        }

        /// <summary>노선별 추격자 수(LineId 기준). 가시화·압박 밀도 확인용(§5-2).</summary>
        public static Dictionary<string, int> CountPerLine(IReadOnlyList<Tracker> trackers)
        {
            var result = new Dictionary<string, int>();
            if (trackers == null) return result;
            foreach (var t in trackers)
            {
                if (t == null || string.IsNullOrEmpty(t.LineId)) continue;
                result[t.LineId] = result.TryGetValue(t.LineId, out var c) ? c + 1 : 1;
            }
            return result;
        }

        /// <summary>플레이어와 같은 역에 있는 추격자가 있는지(검문 발동 조건 §8-1).</summary>
        public static bool AnyAtPlayer(IReadOnlyList<Tracker> trackers, string playerStationId)
        {
            if (trackers == null || string.IsNullOrEmpty(playerStationId)) return false;
            foreach (var t in trackers)
                if (t != null && t.StationId == playerStationId) return true;
            return false;
        }
    }
}
