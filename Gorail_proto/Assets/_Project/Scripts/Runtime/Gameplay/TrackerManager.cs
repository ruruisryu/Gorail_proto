using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Subway;
using Game.Data;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// ⑤⑥ 추격자 일괄 관리(§15 TrackerManager). 스폰(노선당 상한)·추격(1+2규칙+체증)·
    /// 수배도 감소 시 제거를 담당하고, TurnResolver에 ITrackerStep으로 주입된다.
    ///
    /// 스폰 위치 등 [미확정] 세부는 기획서 §5 의도에 맞춘 프로토타입 구현이며 D단계에서 조정한다.
    /// 랜덤은 지금 UnityEngine.Random을 쓰고, H1에서 시드 고정 RngService로 교체 예정.
    /// </summary>
    public class TrackerManager : MonoBehaviour, ITrackerStep
    {
        [Header("참조")]
        [SerializeField] private MapGraphProvider   graphProvider;
        [SerializeField] private Player             player;
        [SerializeField] private ChaseConfig        config;
        [SerializeField] private GameManager        gameManager;
        [SerializeField] private EnemyLocationData  enemyLocations;
        [SerializeField] private SubwayMapRenderer  mapRenderer;
        [Tooltip("시드 고정 난수(선택). 미할당이면 UnityEngine.Random 폴백.")]
        [SerializeField] private RngService         rng;

        private readonly List<Tracker> _trackers = new List<Tracker>();
        private float _chaseDebt; // 체증 보정으로 생긴 소수 추격량 누적(정수 스텝으로 환산)

        public IReadOnlyList<Tracker> Trackers => _trackers;

        MapGraph Graph => graphProvider != null ? graphProvider.Graph : null;
        int Wanted => gameManager != null ? gameManager.WantedLevel : 0;

        // ── 스폰 (§5-1 하차 시 갱신) ─────────────────────────────────────

        /// <summary>플레이어 하차(도착) 시 호출 — 활성 노선별 상한까지 추가 스폰(§5-1·§5-2).</summary>
        public void OnPlayerDisembark()
        {
            if (Graph == null || player == null) return;
            int cap = config != null ? config.PerLineCap(Wanted) : 0;

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
            int cap = config != null ? config.PerLineCap(Wanted) : 0;

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

        // ── 추격 1스텝 (ITrackerStep, §4-1 + §4-2) ───────────────────────

        public void Advance(int playerSteps, int totalMoveStations)
        {
            if (Graph == null || player == null || _trackers.Count == 0) return;

            float baseM = config != null ? config.chaserStepsPerPlayerStep : 1;
            float mult  = config != null ? Mathf.Max(0.01f, config.congestionCurve.Evaluate(totalMoveStations)) : 1f;
            int steps   = ComputeAdvanceSteps(baseM, mult, playerSteps, ref _chaseDebt);
            if (steps <= 0) return;

            foreach (var t in _trackers)
                t.ChaseToward(Graph, player.CurrentStationId, steps);

            SyncMarkers();
        }

        /// <summary>외부 체류 등으로 모든 추격자를 steps역 일괄 전진(§9-1 체류→추격 전진). 체증 없음.</summary>
        public void AdvanceAll(int steps)
        {
            if (Graph == null || player == null || steps <= 0 || _trackers.Count == 0) return;
            foreach (var t in _trackers)
                t.ChaseToward(Graph, player.CurrentStationId, steps);
            SyncMarkers();
        }

        /// <summary>체증 누적을 정수 스텝으로 환산(소수분은 다음 호출로 이월). 순수 함수 — 테스트 대상.</summary>
        public static int ComputeAdvanceSteps(float basePerStep, float congestionMult, int playerSteps, ref float debt)
        {
            debt += basePerStep * congestionMult * Mathf.Max(0, playerSteps);
            int steps = Mathf.FloorToInt(debt);
            debt -= steps;
            return steps;
        }

        // ── 세션 ─────────────────────────────────────────────────────────

        public void ResetAll()
        {
            _trackers.Clear();
            _chaseDebt = 0f;
            SyncMarkers();
        }

        // ── 검문 연동 (⑧ InspectionSystem이 사용) ────────────────────────

        /// <summary>해당 역에 추격자가 있는지(같은 역 검문 발동 조건 §8-1).</summary>
        public bool HasTrackerAt(string stationId) =>
            !string.IsNullOrEmpty(stationId) && _trackers.Any(t => t.StationId == stationId);

        /// <summary>해당 역의 추격자를 모두 제거(검문 통과 시 어그로 해제 §8-2). 제거 수 반환.</summary>
        public int RemoveTrackersAt(string stationId)
        {
            int removed = _trackers.RemoveAll(t => t.StationId == stationId);
            if (removed > 0) SyncMarkers();
            return removed;
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────────

        int CountOnLine(string line) => _trackers.Count(t => t.LineId == line);

        /// <summary>[a,b) 정수 — RngService 있으면 시드 기반, 없으면 UnityEngine.Random.</summary>
        int RandInt(int a, int b) => rng != null ? rng.RangeInt(a, b) : Random.Range(a, b);

        void SpawnOnLine(string line)
        {
            string station = PickSpawnStation(line);
            if (!string.IsNullOrEmpty(station))
                _trackers.Add(new Tracker(station, line));
        }

        /// <summary>
        /// 노선 위에서 플레이어로부터 적당히 떨어진(첫 등장 §5-4 범위 우선) 스폰 역을 고른다.
        /// 범위 내 후보가 없으면 그 노선에서 플레이어로부터 가장 먼 역으로 폴백.
        /// </summary>
        string PickSpawnStation(string line)
        {
            var stations = Graph.GetLineStations(line);
            if (stations.Count == 0) return null;

            string playerStn = player.CurrentStationId;
            int min = config != null ? config.firstSpawnMinBehind : 6;
            int max = config != null ? config.firstSpawnMaxBehind : 8;

            var inRange = stations
                .Where(s => s != playerStn)
                .Where(s => { int d = Graph.Distance(playerStn, s); return d >= min && d <= max; })
                .ToList();

            if (inRange.Count > 0)
                return inRange[RandInt(0, inRange.Count)];

            // 폴백: 플레이어로부터 가장 먼 역(연결 없으면 아무 역)
            return stations
                .Where(s => s != playerStn)
                .OrderByDescending(s => { int d = Graph.Distance(playerStn, s); return d == int.MaxValue ? -1 : d; })
                .FirstOrDefault() ?? stations[0];
        }

        /// <summary>거리별(근/중/원 3밴드)로 나눠 각 밴드에서 비슷한 비율로 count개를 제거 대상으로 고른다(§5-5).</summary>
        List<Tracker> PickByDistanceBands(List<Tracker> pool, int count)
        {
            if (count >= pool.Count) return new List<Tracker>(pool);

            string playerStn = player != null ? player.CurrentStationId : null;
            var sorted = pool.OrderBy(t => DistanceToPlayer(t, playerStn)).ToList();

            int n = sorted.Count;
            var bands = new List<List<Tracker>>
            {
                sorted.Take(n / 3).ToList(),                       // 근거리
                sorted.Skip(n / 3).Take(n / 3).ToList(),           // 중거리
                sorted.Skip(2 * (n / 3)).ToList(),                 // 원거리
            };

            var picked = new List<Tracker>();
            int bi = 0;
            // 밴드를 돌며 한 명씩 균등하게 뽑아 비율을 맞춘다
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

        /// <summary>
        /// 마커 동기화 — ⑦ 가시화 규칙(§6): 활성 노선 위의 추격자만 표시한다.
        /// 추격자가 2규칙으로 비활성 노선 구간에 들어가 있으면 숨고, 그 노선으로 환승하면 드러난다.
        /// </summary>
        void SyncMarkers()
        {
            if (enemyLocations != null)
            {
                enemyLocations.enemyStationIds = _trackers
                    .Where(t => IsLineVisible(t.LineId))
                    .Select(t => t.StationId)
                    .ToList();
            }
            if (mapRenderer != null) mapRenderer.RefreshMarkers();
        }

        /// <summary>플레이어가 한 번이라도 탑승한 노선인지(활성 노선만 가시 §6).</summary>
        bool IsLineVisible(string lineId) =>
            player != null && !string.IsNullOrEmpty(lineId) && player.HasVisitedLine(lineId);
    }
}
