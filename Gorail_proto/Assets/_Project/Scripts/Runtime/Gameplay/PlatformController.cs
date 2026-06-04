using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// ③ 승강장(§7). 도착역에서 하차 시 열리며, 네 갈래 행동을 제공한다(§7-2):
    ///   ① 가던 방향 재탑승  ② 같은 노선 반대방향 탑승  ③ 환승(환승역에서만)  ④ 야외로(이 슬라이스 stub)
    ///
    /// 노선 변경의 유일한 경로(§2-2)가 환승이므로, 이 컴포넌트의 Transfer가 곧 노선 전환점이다.
    /// 이 슬라이스에서는 행동 로직만 구현하고 비주얼 형식은 무관(§7-2 [미확정]) — UI는 DebugPanel/저녁에 붙인다.
    /// TurnResolver에 IPlatform으로 주입되어 도착 시 OpenAt이 호출된다.
    /// </summary>
    public class PlatformController : MonoBehaviour, IPlatform
    {
        [SerializeField] private Player            player;
        [SerializeField] private MapGraphProvider  graphProvider;

        public bool   IsOpen        { get; private set; }
        public string CurrentStation { get; private set; }

        private readonly List<string> _transferLines = new List<string>();
        /// <summary>현재 승강장에서 환승 가능한 노선(현재 노선 제외). 환승역이 아니면 빈 목록.</summary>
        public IReadOnlyList<string> AvailableTransferLines => _transferLines;

        /// <summary>승강장이 열릴 때 발생(도착역, 환승 가능 노선들).</summary>
        public event System.Action<string, IReadOnlyList<string>> PlatformOpened;
        /// <summary>승강장 행동이 선택되어 닫힐 때 발생.</summary>
        public event System.Action PlatformClosed;

        MapGraph Graph => graphProvider != null ? graphProvider.Graph : null;

        /// <summary>도착역에서 승강장 진입(§7-1). 환승 가능 노선을 계산해 알린다.</summary>
        public void OpenAt(string stationId)
        {
            CurrentStation = stationId;
            IsOpen = true;

            _transferLines.Clear();
            if (Graph != null && player != null)
            {
                foreach (var line in Graph.GetLineIds(stationId))
                    if (line != player.CurrentLineId) _transferLines.Add(line);
            }

            Debug.Log($"[Platform] 승강장 @ {stationId} — 환승 가능: " +
                      (_transferLines.Count > 0 ? string.Join(",", _transferLines) : "없음"));
            PlatformOpened?.Invoke(stationId, _transferLines);
        }

        // ── 네 갈래 행동(§7-2) ───────────────────────────────────────────

        /// <summary>① 가던 방향 재탑승 — 방향·노선 유지. 다음 목적지 선택으로 이어진다.</summary>
        public void ContinueForward() => Close();

        /// <summary>② 같은 노선 반대방향 탑승 — 진행 방향 반전(추격자와 상대 위치 역전).</summary>
        public void ReverseDirection()
        {
            if (player != null) player.ReverseDirection();
            Close();
        }

        /// <summary>③ 환승 — 다른 노선으로 변경(환승역에서만, §2-2). 활성 노선 누적(§5-3).</summary>
        public bool Transfer(string newLineId)
        {
            if (!_transferLines.Contains(newLineId))
            {
                Debug.Log($"[Platform] '{newLineId}'은(는) 이 역에서 환승 불가");
                return false;
            }
            if (player != null) player.ChangeLine(newLineId);
            Debug.Log($"[Platform] 환승 → {newLineId}");
            Close();
            return true;
        }

        /// <summary>④ 야외로 이동 — 이 슬라이스에선 stub(§7-2 [추후 F]).</summary>
        public void GoOutside()
        {
            Debug.Log("[Platform] 야외로 이동 (이 슬라이스 stub)");
            Close();
        }

        void Close()
        {
            IsOpen = false;
            PlatformClosed?.Invoke();
        }
    }
}
