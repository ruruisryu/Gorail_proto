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

            // 현재 노선 위 이동 경로(이동 가능 역만). 불가/같은 역이면 프리뷰 제거.
            var path = graph.GetLineOrderedPath(player.CurrentLineId, player.CurrentStationId, stationId);
            if (path == null || path.Count < 2) { mapRenderer.ClearChasePreview(); return; }

            var config  = tr != null ? tr.Config : null;
            float baseM = config != null ? config.chaserStepsPerPlayerStep : 1f;
            int   k     = path.Count - 1;
            float mult  = config != null ? Mathf.Max(0.01f, config.congestionCurve.Evaluate(k)) : 1f;

            // 현재 보이는(활성 노선) 추격자만 복사해 시뮬레이션
            var sims = new List<Tracker>();
            foreach (var t in core.Trackers.Trackers)
                if (player.HasVisitedLine(t.LineId)) sims.Add(new Tracker(t.StationId, t.LineId));

            float debt = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                int steps = TrackerManager.ComputeAdvanceSteps(baseM, mult, 1, ref debt);
                foreach (var s in sims) s.ChaseToward(graph, path[i], steps);
            }

            var preds = new List<string>(sims.Count);
            foreach (var s in sims) preds.Add(s.StationId);
            mapRenderer.ShowChasePreview(preds);
        }

        void OnExit()
        {
            if (mapRenderer != null) mapRenderer.ClearChasePreview();
        }
    }
}
