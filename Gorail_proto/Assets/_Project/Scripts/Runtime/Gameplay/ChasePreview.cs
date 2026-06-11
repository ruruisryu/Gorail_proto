using System.Collections.Generic;
using UnityEngine;
using Game.Subway;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// [D10] 이동 가능 역 호버 시 적 이동 프리뷰.
    /// 플레이어가 현재 노선 위의 어떤 역에 마우스를 올리면, 그 역까지 이동했을 때
    /// 추격자(현재 보이는 활성 노선 추격자)가 어디까지 따라올지 시뮬레이션해 고스트로 보여준다.
    /// 실제 이동 로직(TurnResolver.ResolveMove)과 같은 규칙(1+2규칙·체증)으로 복사본을 굴린다.
    /// </summary>
    public class ChasePreview : MonoBehaviour
    {
        [SerializeField] private SubwayMapRenderer mapRenderer;

        void OnEnable()
        {
            StationView.StationHovered += OnHover;
            StationView.StationHoverExited += OnExit;
        }

        void OnDisable()
        {
            StationView.StationHovered -= OnHover;
            StationView.StationHoverExited -= OnExit;
        }

        void OnHover(string stationId)
        {
            var core = GameCore.Instance;
            if (core == null || mapRenderer == null) return;
            var player = core.Player;
            var graph  = core.Graph != null ? core.Graph.Graph : null;
            var tr     = core.TurnResolver;
            if (player == null || graph == null) return;
            if (tr != null && tr.IsMoving) return;                                  // 이동 중엔 프리뷰 안 함
            if (core.Space != null && core.Space.Current != Game.Core.Space.Subway) return; // 지하철에서만

            // 현재 노선 위 이동 경로 — 방향 고정 상태면 현재 방향으로만, 미고정이면 최단 경로.
            var path = player.DirectionLocked
                ? graph.GetDirectionalPath(player.CurrentLineId, player.CurrentStationId, stationId, player.Direction)
                : graph.GetLineOrderedPath(player.CurrentLineId, player.CurrentStationId, stationId);
            if (path == null || path.Count < 2) { mapRenderer.ClearChasePreview(); return; }

            var config  = tr != null ? tr.Config : null;
            float baseM = config != null ? config.chaserStepsPerPlayerStep : 1f;
            int   k     = path.Count - 1;
            float mult  = config != null ? Mathf.Max(0.01f, config.congestionCurve.Evaluate(k)) : 1f;

            // 한 이동 동안 적이 플레이어 경로 각 칸을 좇으며 전진할 스텝 스케줄(모든 적 공통, 체증 반영).
            var schedule = new int[path.Count];
            float debt = 0f;
            for (int i = 1; i < path.Count; i++)
                schedule[i] = TrackerManager.ComputeAdvanceSteps(baseM, mult, 1, ref debt);

            // 적 출처 = 지도에 실제 표시 중인 적 마커(런타임 추격자=디버그 정적 적 모두 동일 경로).
            var enemyStarts = mapRenderer.DisplayedEnemyStations;
            var enemyPaths = new List<IReadOnlyList<string>>();
            if (enemyStarts != null)
                foreach (var startId in enemyStarts)
                {
                    if (string.IsNullOrEmpty(startId)) continue;
                    var sim  = new Tracker(startId, player.CurrentLineId);
                    var traj = new List<string> { startId };
                    for (int i = 1; i < path.Count; i++)
                        for (int s = 0; s < schedule[i]; s++)
                        {
                            string before = sim.StationId;
                            sim.ChaseToward(graph, path[i], 1);
                            if (sim.StationId == before) break;   // 더 못 감(따라잡음/경로없음)
                            traj.Add(sim.StationId);
                        }
                    enemyPaths.Add(traj);
                }

            // 플레이어 경로(노랑)는 항상, 적 경로(빨강)는 표시 중인 적이 있을 때.
            mapRenderer.ShowChasePreview(path, enemyPaths);
        }

        void OnExit()
        {
            if (mapRenderer != null) mapRenderer.ClearChasePreview();
        }
    }
}
