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
        // lineId -> 순서대로의 역 ID 리스트 (노선 위 경로 산출용)
        private readonly Dictionary<string, List<string>> _lineStations = new();
        // lineId -> 순환 여부
        private readonly Dictionary<string, bool> _lineCircular = new();

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

                // 노선 순서·순환 여부 보존 (GetLineOrderedPath용)
                var ordered = new List<string>();
                foreach (var stn in s)
                    if (stn != null) ordered.Add(stn.stationId);
                _lineStations[line.lineId] = ordered;
                _lineCircular[line.lineId] = line.isCircular;

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

        /// <summary>해당 노선이 순환선인지 여부.</summary>
        public bool IsLineCircular(string lineId) =>
            _lineCircular.TryGetValue(lineId, out var c) && c;

        /// <summary>해당 노선이 지나는 역인지 여부.</summary>
        public bool LineHasStation(string lineId, string stationId) =>
            _lineStations.TryGetValue(lineId, out var s) && s.Contains(stationId);

        /// <summary>해당 노선의 순서대로의 역 ID 리스트(읽기 전용 복사본). 없으면 빈 리스트.</summary>
        public List<string> GetLineStations(string lineId) =>
            _lineStations.TryGetValue(lineId, out var s) ? new List<string>(s) : new List<string>();

        /// <summary>
        /// 지정 방향(+1/-1)으로만 이동하는 경로를 반환한다.
        /// 비순환선에서 direction이 to 방향과 반대이면 빈 리스트.
        /// 순환선에서는 direction 방향으로 순회해 to에 닿으면 그 경로를 반환한다.
        /// DirectionLocked 상태의 TryMoveTo에서 사용한다.
        /// </summary>
        public List<string> GetDirectionalPath(string lineId, string from, string to, int direction)
        {
            var empty = new List<string>();
            if (string.IsNullOrEmpty(lineId)) return empty;
            if (!_lineStations.TryGetValue(lineId, out var stations)) return empty;

            int iFrom = stations.IndexOf(from);
            int iTo   = stations.IndexOf(to);
            if (iFrom < 0 || iTo < 0) return empty;
            if (iFrom == iTo) return new List<string> { from };

            int n   = stations.Count;
            int dir = direction >= 0 ? 1 : -1;
            bool circular = _lineCircular.TryGetValue(lineId, out var c) && c;

            if (!circular)
            {
                int naturalDir = iTo > iFrom ? 1 : -1;
                if (naturalDir != dir) return empty; // 요청 방향과 반대
                var path = new List<string>();
                for (int i = iFrom; i != iTo; i += dir) path.Add(stations[i]);
                path.Add(stations[iTo]);
                return path;
            }

            // 순환선: direction 방향으로 순회해 to를 찾는다
            var cpath = new List<string> { stations[iFrom] };
            int idx = iFrom;
            for (int k = 0; k < n - 1; k++)
            {
                idx = (idx + dir + n) % n;
                cpath.Add(stations[idx]);
                if (idx == iTo) return cpath;
            }
            return empty; // 도달 불가
        }

        /// <summary>
        /// 한 노선 위에서 from → to 경로(역 ID 리스트, from·to 포함)를 반환한다(§2-2 노선 내 이동).
        /// 두 역이 모두 같은 노선에 있어야 하며, 아니면 빈 리스트(환승은 ③ 승강장에서만).
        /// 비순환선: 인덱스 사이 구간을 방향에 맞게 슬라이스.
        /// 순환선(2호선): 시계/반시계 두 호(弧) 중 짧은 쪽을 자동 선택.
        /// 방향 미고정 상태(DirectionLocked=false)의 첫 탑승에서 사용한다.
        /// </summary>
        public List<string> GetLineOrderedPath(string lineId, string from, string to)
        {
            var empty = new List<string>();
            if (string.IsNullOrEmpty(lineId)) return empty;
            if (!_lineStations.TryGetValue(lineId, out var stations)) return empty;

            int iFrom = stations.IndexOf(from);
            int iTo   = stations.IndexOf(to);
            if (iFrom < 0 || iTo < 0) return empty;        // 둘 중 하나라도 이 노선에 없음
            if (iFrom == iTo) return new List<string> { from };

            int n = stations.Count;
            bool circular = _lineCircular.TryGetValue(lineId, out var c) && c;

            if (!circular)
            {
                // 비순환: from→to 한 방향 슬라이스
                var path = new List<string>();
                int step = iTo > iFrom ? 1 : -1;
                for (int i = iFrom; i != iTo; i += step) path.Add(stations[i]);
                path.Add(stations[iTo]);
                return path;
            }

            // 순환: 정방향(+1)·역방향(-1) 호 길이를 비교해 짧은 쪽 채택
            int fwdLen = (iTo - iFrom + n) % n;            // +1로 도달하는 칸 수
            int bwdLen = n - fwdLen;                        // -1로 도달하는 칸 수
            int dir    = fwdLen <= bwdLen ? 1 : -1;
            int len    = fwdLen <= bwdLen ? fwdLen : bwdLen;

            var cpath = new List<string> { stations[iFrom] };
            int idx = iFrom;
            for (int k = 0; k < len; k++)
            {
                idx = (idx + dir + n) % n;
                cpath.Add(stations[idx]);
            }
            return cpath;
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

        /// <summary>인접한 두 역 a-b를 잇는 노선 ID(여럿이면 첫 번째). 인접이 아니면 null.</summary>
        public string GetConnectingLineId(string a, string b)
        {
            if (!_adj.TryGetValue(a, out var edges)) return null;
            foreach (var (nbr, lid) in edges)
                if (nbr == b) return lid;
            return null;
        }
    }
}
