using System.Collections.Generic;
using UnityEngine;
using Game.Subway;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// ③ 승강장 로직(§7) — 멀티 씬판. 도착 시 SpaceManager로 승강장 공간(PlatformScene) 진입을 트리거하고,
    /// 네 갈래 행동(§7-2)을 실제 공간 전환으로 수행한다. UI는 PlatformSceneController(PlatformScene)가 그린다.
    /// TurnResolver에 IPlatform으로 주입되어 도착 시 OpenAt이 호출된다.
    /// </summary>
    public class PlatformController : MonoBehaviour, IPlatform
    {
        [SerializeField] private Player           player;
        [SerializeField] private MapGraphProvider graphProvider;
        [SerializeField] private SpaceManager     spaceManager;

        public string CurrentStation { get; private set; }

        private readonly List<string> _transferLines = new List<string>();
        /// <summary>현재 승강장에서 환승 가능한 노선(현재 노선 제외). 환승역이 아니면 빈 목록.</summary>
        public IReadOnlyList<string> AvailableTransferLines => _transferLines;

        MapGraph Graph => graphProvider != null ? graphProvider.Graph : null;

        /// <summary>현재 역이 특별역(랜드마크/상점)이라 지상 진입 가능한가(§10·§9).</summary>
        public bool CanGoOutside
        {
            get
            {
                var s = Graph != null ? Graph.GetStation(CurrentStation) : null;
                return s != null && s.AllowsOutside;
            }
        }

        // ── 도착 → 승강장 공간 진입(§7-1) ────────────────────────────────
        public void OpenAt(string stationId)
        {
            CurrentStation = stationId;
            _transferLines.Clear();
            if (Graph != null && player != null)
                foreach (var line in Graph.GetLineIds(stationId))
                    if (line != player.CurrentLineId) _transferLines.Add(line);

            if (spaceManager != null) spaceManager.EnterPlatform(stationId);
        }

        // ── 네 갈래(§7-2) — PlatformSceneController가 호출 ───────────────

        /// <summary>① 가던 방향 재탑승 — 방향·노선 유지 후 지하철로.</summary>
        public void ContinueForward()
        {
            if (spaceManager != null) spaceManager.EnterSubway();
        }

        /// <summary>② 반대방향 탑승 — 진행 방향 반전 후 지하철로.</summary>
        public void ReverseDirection()
        {
            if (player != null) player.ReverseDirection();
            if (spaceManager != null) spaceManager.EnterSubway();
        }

        /// <summary>③ 환승 — 노선 변경(환승역만, §2-2) 후 지하철로. 활성 노선 누적(§5-3).</summary>
        public bool Transfer(string newLineId)
        {
            if (!_transferLines.Contains(newLineId)) return false;
            if (player != null) player.ChangeLine(newLineId);
            if (spaceManager != null) spaceManager.EnterSubway();
            return true;
        }

        /// <summary>④ 지상으로 — 특별역만(§9·§10). 지상 공간(OutsideScene)으로.</summary>
        public bool GoOutside()
        {
            if (!CanGoOutside) { Debug.Log($"[Platform] {CurrentStation}은 특별역이 아니라 지상 진입 불가"); return false; }
            if (spaceManager != null) spaceManager.EnterGround(CurrentStation);
            return true;
        }
    }
}
