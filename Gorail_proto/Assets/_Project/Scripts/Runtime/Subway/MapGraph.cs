using System.Collections.Generic;

namespace Game.Subway
{
    /// <summary>
    /// SubwayNetworkData를 그래프로 빌드하고 BFS 탐색을 제공한다.
    /// 플레이어 이동(노선 제약 있음)과 Tracker 추격(노선 제약 없음) 모두 이 클래스에 의존한다.
    /// </summary>
    public class MapGraph
    {
        // stationId -> [(neighborId, lineId), ...]
        private readonly Dictionary<string, List<(string neighborId, string lineId)>> _adj = new();
        private readonly Dictionary<string, StationData> _stations = new();
        // stationId -> [lineId, ...]
        private readonly Dictionary<string, List<string>> _stationLines = new();

        public MapGraph(SubwayNetworkData network)
        {
            Build(network);
        }

        void Build(SubwayNetworkData network)
        {
            foreach (var line in network.lines)
            {
                if (line == null) continue;
                var s = line.stations;
                for (int i = 0; i < s.Count; i++)
                {
                    if (s[i] == null) continue;
                    Register(s[i], line.lineId);
                }
                for (int i = 0; i < s.Count - 1; i++)
                {
                    if (s[i] == null || s[i + 1] == null) continue;
                    AddEdge(s[i].stationId, s[i + 1].stationId, line.lineId);
                }
                if (line.isCircular && s.Count > 1 && s[0] != null && s[s.Count - 1] != null)
                    AddEdge(s[s.Count - 1].stationId, s[0].stationId, line.lineId);
            }
        }

        void Register(StationData stn, string lineId)
        {
            _stations[stn.stationId] = stn;

            if (!_stationLines.ContainsKey(stn.stationId))
                _stationLines[stn.stationId] = new List<string>();
            if (!_stationLines[stn.stationId].Contains(lineId))
                _stationLines[stn.stationId].Add(lineId);

            if (!_adj.ContainsKey(stn.stationId))
                _adj[stn.stationId] = new List<(string, string)>();
        }

        void AddEdge(string a, string b, string lineId)
        {
            _adj[a].Add((b, lineId));
            _adj[b].Add((a, lineId));
        }

        // ── 조회 ──────────────────────────────────────────────────────────

        public StationData GetStation(string id) =>
            _stations.TryGetValue(id, out var s) ? s : null;

        public bool IsTransfer(string stationId) =>
            _stationLines.TryGetValue(stationId, out var lines) && lines.Count > 1;

        public List<string> GetLineIds(string stationId) =>
            _stationLines.TryGetValue(stationId, out var lines) ? lines : new List<string>();

        public IEnumerable<string> AllStationIds => _stations.Keys;

        /// <summary>같은 노선 위 인접 역 반환 (플레이어 이동 가능 목록용)</summary>
        public List<string> GetNeighborsOnLine(string stationId, string lineId)
        {
            var result = new List<string>();
            if (!_adj.TryGetValue(stationId, out var edges)) return result;
            foreach (var (nbr, lid) in edges)
                if (lid == lineId) result.Add(nbr);
            return result;
        }

        // ── BFS (노선 제약 없음 — Tracker 추격용) ────────────────────────

        /// <summary>두 역 사이 최단 거리(역 수)를 반환. 연결 없으면 int.MaxValue.</summary>
        public int Distance(string from, string to)
        {
            if (from == to) return 0;
            var visited = new HashSet<string> { from };
            var queue = new Queue<(string id, int dist)>();
            queue.Enqueue((from, 0));
            while (queue.Count > 0)
            {
                var (cur, d) = queue.Dequeue();
                if (!_adj.TryGetValue(cur, out var edges)) continue;
                foreach (var (nbr, _) in edges)
                {
                    if (nbr == to) return d + 1;
                    if (visited.Add(nbr)) queue.Enqueue((nbr, d + 1));
                }
            }
            return int.MaxValue;
        }

        /// <summary>두 역 사이 최단 경로(역 ID 리스트)를 반환. 연결 없으면 빈 리스트.</summary>
        public List<string> ShortestPath(string from, string to)
        {
            if (from == to) return new List<string> { from };
            var prev = new Dictionary<string, string> { [from] = null };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (!_adj.TryGetValue(cur, out var edges)) continue;
                foreach (var (nbr, _) in edges)
                {
                    if (prev.ContainsKey(nbr)) continue;
                    prev[nbr] = cur;
                    if (nbr == to)
                    {
                        var path = new List<string>();
                        for (var n = to; n != null; n = prev[n]) path.Add(n);
                        path.Reverse();
                        return path;
                    }
                    queue.Enqueue(nbr);
                }
            }
            return new List<string>();
        }

        /// <summary>from에서 to 방향으로 한 걸음 이동할 때 가야 할 다음 역 ID.</summary>
        public string NextStepToward(string from, string to)
        {
            var path = ShortestPath(from, to);
            return path.Count >= 2 ? path[1] : from;
        }
    }
}
