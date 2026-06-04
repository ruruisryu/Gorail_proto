using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Subway;
using Game.Data;

namespace Game.Gameplay
{
    /// <summary>
    /// ②TurnResolver — 입력은 크게, 해소는 잘게(§2-1).
    ///
    /// 플레이어가 현재 노선 위의 목적지 역을 한 번 찍으면(입력 단위),
    /// 출발역→목적지를 1역씩 코루틴으로 순차 해소한다(해소 단위).
    /// 각 해소 스텝마다(§10-3 순서 엄수):
    ///   (1) 플레이어 1역 전진 + 마커 연출
    ///   (2) TrackerManager 추격 1스텝 (체증 보정 k 전달)   — ITrackerStep (⑤⑥)
    ///   (3) 같은 역 검문 판정 (중간역 검문 토글 §8-1)        — IInspection (⑧)
    /// 목적지 도달 시 PlatformController로 넘긴다              — IPlatform (③)
    ///
    /// ⑤⑧③이 아직 없으면 Null* 빈 구현으로 안전하게 동작한다(SetSystems로 실물 주입).
    /// </summary>
    public class TurnResolver : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Player            player;
        [SerializeField] private MapGraphProvider  graphProvider;
        [SerializeField] private SubwayMapRenderer mapRenderer;
        [SerializeField] private ChaseConfig       config;

        // ⑤⑧③ 경계 — 구현 전엔 빈 구현. 나중에 SetSystems로 실물 주입.
        private ITrackerStep _tracker    = new NullTrackerStep();
        private IInspection  _inspection = new NullInspection();
        private IPlatform    _platform   = new NullPlatform();

        /// <summary>현재 이동(코루틴) 진행 중인지. 이동 중 추가 입력 무시용.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>한 역 해소가 끝날 때마다 발생(연출·계측용). (도달한 역, 스텝 i, 총 k)</summary>
        public event System.Action<string, int, int> StepResolved;

        /// <summary>이동 끝(도착 또는 검문 게임오버)에 발생. (마지막 역, gameOver)</summary>
        public event System.Action<string, bool> MoveCompleted;

        MapGraph Graph => graphProvider != null ? graphProvider.Graph : null;
        public ChaseConfig Config => config;

        /// <summary>⑤⑧③ 구현체를 주입한다(없으면 Null* 유지).</summary>
        public void SetSystems(ITrackerStep tracker, IInspection inspection, IPlatform platform)
        {
            if (tracker    != null) _tracker    = tracker;
            if (inspection != null) _inspection = inspection;
            if (platform   != null) _platform   = platform;
        }

        /// <summary>
        /// 목적지 역으로 이동을 시도한다. 현재 노선 위에 있고 이동 중이 아니면 시작.
        /// 노선이 다르면(환승 필요) false — 환승은 ③ 승강장에서만(§2-2).
        /// </summary>
        public bool TryMoveTo(string destStationId)
        {
            if (IsMoving) { Debug.Log("[TurnResolver] 이동 중 — 입력 무시"); return false; }
            if (player == null || Graph == null)
            {
                Debug.LogWarning("[TurnResolver] player 또는 MapGraph 미할당");
                return false;
            }
            if (string.IsNullOrEmpty(destStationId) || destStationId == player.CurrentStationId)
                return false;

            var path = Graph.GetLineOrderedPath(player.CurrentLineId, player.CurrentStationId, destStationId);
            if (path == null || path.Count < 2)
            {
                Debug.Log($"[TurnResolver] '{destStationId}'은(는) 현재 노선({player.CurrentLineId}) 위에 없음 — 환승 필요(§2-2)");
                return false;
            }

            StartCoroutine(ResolveMove(path));
            return true;
        }

        /// <summary>경로를 1역씩 순차 해소(§10-3).</summary>
        IEnumerator ResolveMove(List<string> path)
        {
            IsMoving = true;

            int totalK = path.Count - 1;                 // 이번 1회 행동에 포함된 역 수(체증 §4-2)
            int dir    = ResolveDirection(path);
            float wait = config != null ? config.stepAnimSeconds : 0.25f;
            bool inspectMid = config == null || config.inspectAtMidStations;

            for (int i = 1; i < path.Count; i++)
            {
                bool isArrival = i == path.Count - 1;

                // (1) 플레이어 1역 전진 + 마커 연출
                player.StepTo(path[i], dir);
                if (mapRenderer != null) mapRenderer.RefreshMarkers();
                StepResolved?.Invoke(player.CurrentStationId, i, totalK);

                // (2) 추격 1스텝 (체증 보정용 k 전달)
                _tracker.Advance(1, totalK);

                // (3) 같은 역 검문 — 도착역은 항상, 중간역은 토글(§8-1)
                if (isArrival || inspectMid)
                {
                    bool gameOver = _inspection.ResolveAt(player.CurrentStationId);
                    if (gameOver)
                    {
                        Debug.Log($"[TurnResolver] 검문 실패 — 게임오버 @ {player.CurrentStationId}");
                        IsMoving = false;
                        MoveCompleted?.Invoke(player.CurrentStationId, true);
                        yield break;
                    }
                }

                if (wait > 0f) yield return new WaitForSeconds(wait);
            }

            // 목적지 도달 → 승강장(§7-1)
            IsMoving = false;
            _platform.OpenAt(player.CurrentStationId);
            MoveCompleted?.Invoke(player.CurrentStationId, false);
        }

        /// <summary>경로의 노선 인덱스 기준 진행 방향(+1/-1)을 산출.</summary>
        int ResolveDirection(List<string> path)
        {
            if (Graph == null || path.Count < 2) return +1;
            var stations = Graph.GetLineStations(player.CurrentLineId);
            int i0 = stations.IndexOf(path[0]);
            int i1 = stations.IndexOf(path[1]);
            if (i0 < 0 || i1 < 0) return +1;

            // 순환선 랩어라운드: 끝↔처음 점프면 부호를 뒤집어 해석
            int n = stations.Count;
            if (Graph.IsLineCircular(player.CurrentLineId))
            {
                if (i1 == (i0 + 1) % n) return +1;
                if (i0 == (i1 + 1) % n) return -1;
            }
            return i1 >= i0 ? +1 : -1;
        }
    }
}
