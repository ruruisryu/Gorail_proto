using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Subway;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// ⑤⑥ 추격자 일괄 관리(§15). 스폰(노선당 상한)·추격(1+2규칙)·수배도 감소 시 제거를 담당하고,
    /// TurnResolver에 ITrackerStep으로 주입된다.
    /// </summary>
    public class TrackerManager : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private EnemyLocationData  enemyLocations;

        [Header("추격 속도")]
        [Tooltip("기본 추격: 플레이어 1역 이동 시 추격자도 1역. 추가로 플레이어 N역마다 보너스 1역.")]
        [SerializeField] public int bonusStepsPerPlayerSteps = 5;
        [Tooltip("지상 체류 시 추격자 이동 속도: 게임 시간 N분마다 1역.")]
        [SerializeField] public float trackerMinutesPerStep = 5f;

        [Header("노선당 개체수 상한 — §5-2")]
        [Tooltip("인덱스 = 수배도 레벨(0~5), 값 = 활성 노선 1개당 Tracker 상한.")]
        [SerializeField] private int[] perLineCapByWantedLevel = { 0, 1, 2, 3, 4, 5 };

        [Header("첫 등장 — §5-4")]
        [Tooltip("세션 최초 스폰 시 플레이어 뒤로 떨어뜨릴 역 범위(최소~최대).")]
        [SerializeField] private int firstSpawnMinBehind = 6;
        [SerializeField] private int firstSpawnMaxBehind = 8;

        RngService        Rng    => GameCore.Instance?.Rng;
        Player            player => GameCore.Instance?.Player;
        Game.Subway.SubwayMapRenderer mapRenderer => GameCore.Instance?.MapRenderer;

        private readonly List<Tracker> _trackers = new List<Tracker>();
        private float _debt;

        public IReadOnlyList<Tracker> Trackers  => _trackers;
        public float                  ChaseDebt => _debt;

        public MapGraph Graph  => GameCore.Instance?.Graph?.Graph;
        int             Wanted => GameCore.Instance?.Wanted?.WantedLevel ?? 0;

        public int PerLineCap(int wantedLevel)
        {
            if (perLineCapByWantedLevel == null || perLineCapByWantedLevel.Length == 0) return 0;
            return perLineCapByWantedLevel[Mathf.Clamp(wantedLevel, 0, perLineCapByWantedLevel.Length - 1)];
        }

        // ── 스폰 (§5-1 하차 시 갱신) ─────────────────────────────────────

        /// <summary>플레이어 하차(도착) 시 호출 — 활성 노선별 상한까지 추가 스폰(§5-1·§5-2).</summary>
        public void OnPlayerDisembark()
        {
            if (Graph == null || player == null) return;
            TrimToCaps();

            int cap = PerLineCap(Wanted);
            foreach (var line in player.ActiveLines)
            {
                int have = CountOnLine(line);
                for (int i = have; i < cap; i++)
                    SpawnOnLine(line);
            }
            SyncMarkers();
        }

        /// <summary>수배도 하락 시 호출 — 노선별 상한 초과분을 거리별 비율로 제거(§5-5).</summary>
        public void TrimToCaps()
        {
            if (Graph == null) return;
            int cap = PerLineCap(Wanted);

            foreach (var line in player != null ? player.ActiveLines.ToList() : new List<string>())
            {
                var onLine = _trackers.Where(t => t.LineId == line).ToList();
                int excess = onLine.Count - cap;
                if (excess <= 0) continue;
                foreach (var victim in PickByDistanceBands(onLine, excess))
                    _trackers.Remove(victim);
            }
            SyncMarkers();
        }

        // ── 추격 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 플레이어 1역 이동마다 호출.
        /// 기본 1:1 + 플레이어 N역마다 보너스 1역. 소수는 _debt에 이월.
        /// </summary>
        public void Advance(int playerSteps)
        {
            if (Graph == null || player == null || _trackers.Count == 0) return;

            _debt += (1f + 1f / bonusStepsPerPlayerSteps) * playerSteps;
            int steps = Mathf.FloorToInt(_debt);
            _debt -= steps;
            if (steps <= 0) return;

            foreach (var t in _trackers)
                t.ChaseToward(Graph, player.CurrentStationId, steps);
            SyncMarkers();
        }

        /// <summary>지상 체류 등 게임 시간 경과만큼 추격자 전진(보너스 없음).</summary>
        public void AdvanceByMinutes(int gameMinutes)
        {
            if (Graph == null || player == null || gameMinutes <= 0 || _trackers.Count == 0) return;

            _debt += gameMinutes / trackerMinutesPerStep;
            int steps = Mathf.FloorToInt(_debt);
            _debt -= steps;
            if (steps <= 0) return;

            foreach (var t in _trackers)
                t.ChaseToward(Graph, player.CurrentStationId, steps);
            SyncMarkers();
        }

        // ── 세션 ─────────────────────────────────────────────────────────

        public void ResetAll()
        {
            _trackers.Clear();
            _debt = 0f;
            SyncMarkers();
        }

        // ── 검문 연동 (⑧ InspectionSystem이 사용) ────────────────────────

        public bool HasTrackerAt(string stationId) =>
            !string.IsNullOrEmpty(stationId) && _trackers.Any(t => t.StationId == stationId);

        public int RemoveTrackersAt(string stationId)
        {
            int removed = _trackers.RemoveAll(t => t.StationId == stationId);
            if (removed > 0) SyncMarkers();
            return removed;
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────────

        int CountOnLine(string line) => _trackers.Count(t => t.LineId == line);

        int RandInt(int a, int b) => Rng != null ? Rng.RangeInt(a, b) : Random.Range(a, b);

        void SpawnOnLine(string line)
        {
            string station = PickSpawnStation(line);
            if (!string.IsNullOrEmpty(station))
                _trackers.Add(new Tracker(station, line));
        }

        string PickSpawnStation(string line)
        {
            var stations = Graph.GetLineStations(line);
            if (stations.Count == 0) return null;

            string playerStn = player.CurrentStationId;

            var inRange = stations
                .Where(s => s != playerStn)
                .Where(s => { int d = Graph.Distance(playerStn, s); return d >= firstSpawnMinBehind && d <= firstSpawnMaxBehind; })
                .ToList();

            if (inRange.Count > 0)
                return inRange[RandInt(0, inRange.Count)];

            return stations
                .Where(s => s != playerStn)
                .OrderByDescending(s => { int d = Graph.Distance(playerStn, s); return d == int.MaxValue ? -1 : d; })
                .FirstOrDefault() ?? stations[0];
        }

        List<Tracker> PickByDistanceBands(List<Tracker> pool, int count)
        {
            if (count >= pool.Count) return new List<Tracker>(pool);

            string playerStn = player != null ? player.CurrentStationId : null;
            var sorted = pool.OrderBy(t => DistanceToPlayer(t, playerStn)).ToList();

            int n = sorted.Count;
            var bands = new List<List<Tracker>>
            {
                sorted.Take(n / 3).ToList(),
                sorted.Skip(n / 3).Take(n / 3).ToList(),
                sorted.Skip(2 * (n / 3)).ToList(),
            };

            var picked = new List<Tracker>();
            int bi = 0;
            while (picked.Count < count)
            {
                var band = bands[bi % bands.Count];
                if (band.Count > 0)
                {
                    int idx = RandInt(0, band.Count);
                    picked.Add(band[idx]);
                    band.RemoveAt(idx);
                }
                bi++;
                if (bands.All(b => b.Count == 0)) break;
            }
            return picked;
        }

        int DistanceToPlayer(Tracker t, string playerStn)
        {
            if (Graph == null || string.IsNullOrEmpty(playerStn)) return int.MaxValue;
            return Graph.Distance(t.StationId, playerStn);
        }

        void SyncMarkers()
        {
            if (enemyLocations != null)
            {
                enemyLocations.enemyStationIds = _trackers
                    .Select(t => t.StationId)
                    .ToList();
            }
            if (mapRenderer != null) mapRenderer.RefreshMarkers();
        }
    }
}
