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
    public class PlatformController : MonoBehaviour
    {
        Player         player         => Game.Core.GameCore.Instance?.Player;
        MapGraph       Graph          => Game.Core.GameCore.Instance?.Graph?.Graph;
        SpaceManager   spaceManager   => Game.Core.GameCore.Instance?.Space;
        TrackerManager trackerManager => Game.Core.GameCore.Instance?.Trackers;
        GameTimeSystem GameTime       => Game.Core.GameCore.Instance?.GameTime;
        MoneySystem    Money          => Game.Core.GameCore.Instance?.Money;

        public string CurrentStation { get; private set; }

        // 지상에 나갔다 돌아온 경우 탑승·환승 모두 1500원(§자원기획서)
        private bool _cameFromGround;

        private readonly List<string> _transferLines = new List<string>();
        /// <summary>현재 승강장에서 환승 가능한 노선(현재 노선 제외). 환승역이 아니면 빈 목록.</summary>
        public IReadOnlyList<string> AvailableTransferLines => _transferLines;

        /// <summary>현재 역이 특별역(랜드마크/상점)이라 지상 진입 가능한가(§10·§9).</summary>
        public bool CanGoOutside
        {
            get
            {
                var s = Graph != null ? Graph.GetStation(CurrentStation) : null;
                return s != null && s.AllowsOutside;
            }
        }

        // ── 하차 → 승강장 공간 진입(§7-1) ────────────────────────────────
        public void OpenAt(string stationId)
        {
            CurrentStation = stationId;
            _transferLines.Clear();
            if (Graph != null && player != null)
                foreach (var line in Graph.GetLineIds(stationId))
                    if (line != player.CurrentLineId) _transferLines.Add(line);

            // 하차 시 추격자 개체수 갱신(§5-1) — 도착이 아니라 실제 하차 시점.
            if (trackerManager != null) trackerManager.OnPlayerDisembark();

            if (spaceManager != null) spaceManager.EnterPlatform(stationId);
        }

        // ── 네 갈래(§7-2) — PlatformSceneController가 호출 ───────────────

        /// <summary>① 가던 방향 재탑승 — 방향·노선 유지 후 지하철로.</summary>
        public void ContinueForward()
        {
            if (_cameFromGround) Money?.TrySpend(Money.boardingCost);
            _cameFromGround = false;
            if (spaceManager != null) spaceManager.EnterSubway();
        }

        /// <summary>② 반대방향 탑승 — 진행 방향 반전 후 지하철로.</summary>
        public void ReverseDirection()
        {
            if (player != null) player.ReverseDirection();
            GameTime?.Advance(GameTime.minutesReboard);
            if (_cameFromGround) Money?.TrySpend(Money.boardingCost);
            _cameFromGround = false;
            if (spaceManager != null) spaceManager.EnterSubway();
        }

        /// <summary>③ 환승 — 노선 변경(환승역만, §2-2) 후 지하철로. 활성 노선 누적(§5-3).</summary>
        public bool Transfer(string newLineId) => TransferWithDirection(newLineId, 0);

        /// <summary>③-b 방향 지정 환승 — 노선과 초기 방향을 함께 선택한다.</summary>
        public bool TransferWithDirection(string newLineId, int direction)
        {
            if (!_transferLines.Contains(newLineId)) return false;
            if (player != null) player.ChangeLine(newLineId); // DirectionLocked = false
            GameTime?.Advance(GameTime.minutesTransfer);
            Money?.TrySpend(_cameFromGround ? Money.boardingCost : Money.transferCost);
            _cameFromGround = false;
            // 환승으로 새로 활성화된 노선도 현재 수배도 상한까지 채운다(§5-3).
            if (trackerManager != null) trackerManager.OnPlayerDisembark();
            if (direction != 0 && player != null) player.LockDirection(direction);
            if (spaceManager != null) spaceManager.EnterSubway();
            return true;
        }

        /// <summary>방향 버튼 레이블용. 지정 노선의 양방향 종점 역 ID를 반환한다.</summary>
        public (string backward, string forward) GetTransferDirectionLabels(string lineId)
            => Graph?.GetLineNeighbors(lineId, CurrentStation) ?? (null, null);

        /// <summary>④ 지상으로 — 특별역만(§9·§10). 지상 공간(OutsideScene)으로.</summary>
         public bool GoOutside()
        {
            if (!CanGoOutside) { Debug.Log($"[Platform] {CurrentStation}은 특별역이 아니라 지상 진입 불가"); return false; }

            // 진입 30% 룰: 보유 재료가 IP 빈칸의 30% 미만이면 작품활동 진입 불가
            var screen = Game.Inventory.ArtworkScreen.Instance;
            if (screen != null && !screen.HasEnoughMaterials(out int have, out int need))
            {
                Debug.Log($"[Platform] 재료 부족({have}/{need}칸) — IP 빈칸의 30% 이상 있어야 작품활동 진입 가능");
                return false;
            }
            _cameFromGround = true;
            string stn = CurrentStation;
            Game.UI.ScreenFader.Instance?.Fade(onBlack: () =>
            {
                if (spaceManager != null) spaceManager.EnterGround(stn);
            });
            return true;
        }
    }
}
