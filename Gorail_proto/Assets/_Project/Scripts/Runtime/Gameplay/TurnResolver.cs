using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// ②TurnResolver — 입력은 크게, 해소는 잘게(§2-1).
    ///
    /// 플레이어가 현재 노선 위의 목적지 역을 한 번 찍으면(입력 단위),
    /// 출발역→목적지를 1역씩 코루틴으로 순차 해소한다(해소 단위).
    /// 각 해소 스텝마다(§10-3 순서 엄수):
    ///   (1) 플레이어 1역 전진 + 마커 연출
    ///   (2) TrackerManager 추격 1스텝
    ///   (3) 같은 역 검문 판정 (중간역 검문 토글 §8-1)
    /// </summary>
    public enum MoveRejectedReason { WrongLine, WrongDirection, InactiveLine }

    public class TurnResolver : MonoBehaviour
    {
        [Header("이동 연출")]
        [Range(0.02f, 1.5f)]
        [SerializeField] private float stepAnimSeconds = 0.25f;
        [SerializeField] private bool  inspectAtMidStations = true;

        public float StepAnimSeconds      { get => stepAnimSeconds;      set => stepAnimSeconds = value; }
        public bool  InspectAtMidStations { get => inspectAtMidStations; set => inspectAtMidStations = value; }

        Player            player      => Game.Core.GameCore.Instance?.Player;
        MapGraph          Graph       => Game.Core.GameCore.Instance?.Graph?.Graph;
        SubwayMapRenderer mapRenderer => Game.Core.GameCore.Instance?.MapRenderer;
        GameTimeSystem    GameTime    => Game.Core.GameCore.Instance?.GameTime;
        MoneySystem       Money       => Game.Core.GameCore.Instance?.Money;

        /// <summary>현재 이동(코루틴) 진행 중인지. 이동 중 추가 입력 무시용.</summary>
        public bool IsMoving { get; private set; }

        private bool _forceStop;

        /// <summary>진행 중인 이동을 현재 역에서 즉시 중단한다. 하루 종료 등 외부 인터럽트용.</summary>
        public void ForceStop() => _forceStop = true;

        /// <summary>이동 요청이 거부됐을 때 발생. UI 알림용.</summary>
        public event System.Action<MoveRejectedReason> MoveRejected;

        /// <summary>한 역 해소가 끝날 때마다 발생(연출·계측용). (도달한 역, 스텝 i, 총 k)</summary>
        public event System.Action<string, int, int> StepResolved;

        /// <summary>이동 끝(도착 또는 검문 게임오버)에 발생. (마지막 역, gameOver)</summary>
        public event System.Action<string, bool> MoveCompleted;

        /// <summary>
        /// 목적지 역으로 이동을 시도한다. 현재 노선 위에 있고 이동 중이 아니면 시작.
        /// 노선이 다르면(환승 필요) false — 환승은 ③ 승강장에서만(§2-2).
        /// </summary>
        public bool TryMoveTo(string destStationId)
        {
            if (IsMoving) { Debug.Log("[TurnResolver] 이동 중 — 입력 무시"); return false; }
            if (player == null || Graph == null)
            {
                Debug.LogWarning("[TurnResolver] Player 또는 MapGraph 미할당");
                return false;
            }
            if (string.IsNullOrEmpty(destStationId) || destStationId == player.CurrentStationId)
                return false;

            List<string> path;
            if (player.DirectionLocked)
            {
                path = Graph.GetDirectionalPath(player.CurrentLineId, player.CurrentStationId, destStationId, player.Direction);
                if (path == null || path.Count < 2)
                {
                    // 같은 노선인지 확인 — 반대 방향인지 아니면 아예 다른 노선인지 구분
                    var sameLine = Graph.GetLineOrderedPath(player.CurrentLineId, player.CurrentStationId, destStationId);
                    bool isOpposite = sameLine != null && sameLine.Count >= 2;
                    MoveRejected?.Invoke(isOpposite ? MoveRejectedReason.WrongDirection : ClassifyWrongLineReason(destStationId));
                    Debug.Log($"[TurnResolver] 이동 거부 — {(isOpposite ? "반대 방향" : "다른 노선")}");
                    return false;
                }
            }
            else
            {
                path = Graph.GetLineOrderedPath(player.CurrentLineId, player.CurrentStationId, destStationId);
                if (path == null || path.Count < 2)
                {
                    MoveRejected?.Invoke(ClassifyWrongLineReason(destStationId));
                    Debug.Log($"[TurnResolver] '{destStationId}'은(는) 현재 노선({player.CurrentLineId}) 위에 없음");
                    return false;
                }
            }

            StartCoroutine(ResolveMove(path));
            return true;
        }

        /// <summary>경로를 1역씩 순차 해소(§10-3).</summary>
        IEnumerator ResolveMove(List<string> path)
        {
            IsMoving = true;

            int dir = ResolveDirection(path);
            float wait = stepAnimSeconds;
            bool inspectMid = inspectAtMidStations;

            for (int i = 1; i < path.Count; i++)
            {
                bool isArrival = i == path.Count - 1;

                // (1) 플레이어 1역 전진 + 마커 연출
                player.StepTo(path[i], dir);
                GameTime?.Advance(GameTime.minutesPerMove);
                if (mapRenderer != null) mapRenderer.RefreshMarkers();
                StepResolved?.Invoke(player.CurrentStationId, i, path.Count - 1);

                // 하루 종료 인터럽트 — 현재 역에서 이동 중단 (다음 날 이 역 승강장에서 시작)
                if (_forceStop)
                {
                    _forceStop = false;
                    IsMoving   = false;
                    MoveCompleted?.Invoke(player.CurrentStationId, false);
                    yield break;
                }

                // (2) 추격 1스텝
                Game.Core.GameCore.Instance?.Trackers?.Advance(1);

                // (3) 같은 역 검문 — 도착역은 항상, 중간역은 토글(§8-1)
                if (isArrival || inspectMid)
                {
                    bool gameOver = Game.Core.GameCore.Instance?.Inspection?.ResolveAt(player.CurrentStationId) ?? false;
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

            // 목적지 도달 — 자동하차: 도착 즉시 승강장으로 진입한다.
            IsMoving = false;
            MoveCompleted?.Invoke(player.CurrentStationId, false);
            var _core = Game.Core.GameCore.Instance;
            if (_core?.AutoDisembark == true)
                _core.Platform?.OpenAt(player.CurrentStationId);
        }

        /// <summary>현재 노선에 없는 역 클릭 시 이유를 구분한다.</summary>
        MoveRejectedReason ClassifyWrongLineReason(string destStationId)
        {
            if (Graph == null) return MoveRejectedReason.WrongLine;
            var destLines = Graph.GetLineIds(destStationId);
            bool anyActive = false;
            foreach (var line in destLines)
            {
                Debug.Log("Line " + line + ": " + player.HasVisitedLine(line));
                if (player.HasVisitedLine(line))
                {
                    anyActive = true;
                    break;
                }
            }

            return anyActive ? MoveRejectedReason.WrongLine : MoveRejectedReason.InactiveLine;
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
