using System.Collections.Generic;
using UnityEngine;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// 플레이어의 권위 있는 런타임 상태(§15 Player).
    /// 현재 역·현재 노선·진행 방향과 누적 활성 노선(§5-3)을 보유한다.
    ///
    /// 위치의 단일 소스는 여전히 StationData.mapPosition(SO)이며, 이 컴포넌트는
    /// "어느 역에 있는가"만 들고 있다가 매 스텝 PlayerLocationData(SO)에 currentStationId를
    /// 써넣어 SubwayMapRenderer의 기존 마커 렌더링을 그대로 재사용한다.
    /// </summary>
    public class Player : MonoBehaviour
    {
        [Tooltip("렌더러가 읽는 마커용 SO. 매 스텝 currentStationId를 여기에 동기화한다.")]
        [SerializeField] private PlayerLocationData locationData;

        /// <summary>현재 역 ID.</summary>
        public string CurrentStationId { get; private set; }

        /// <summary>현재 탑승 중인 노선 ID. 이동은 이 노선 위에서만 가능(§2-2).</summary>
        public string CurrentLineId { get; private set; }

        /// <summary>
        /// 현재 노선의 station 리스트 인덱스 기준 진행 방향(+1 / -1).
        /// "반대방향 재탑승"(§7-2)·승강장 방향 선택의 토대.
        /// </summary>
        public int Direction { get; private set; } = +1;

        private readonly HashSet<string> _activeLines = new HashSet<string>();

        /// <summary>세션 중 한 번이라도 탑승한 모든 노선의 누적 집합(§5-3). 비활성으로 돌아가지 않는다.</summary>
        public IReadOnlyCollection<string> ActiveLines => _activeLines;

        /// <summary>위치·노선·방향이 바뀌었을 때(초기화·전진·환승) 발생.</summary>
        public event System.Action StateChanged;

        /// <summary>세션 시작 위치·노선을 설정한다.</summary>
        public void Initialize(string startStationId, string startLineId)
        {
            CurrentStationId = startStationId;
            CurrentLineId    = startLineId;
            Direction        = +1;
            _activeLines.Clear();
            if (!string.IsNullOrEmpty(startLineId)) _activeLines.Add(startLineId);
            SyncLocation();
            StateChanged?.Invoke();
        }

        /// <summary>현재 노선을 변경한다(환승, §2-2). 활성 노선 집합에 누적된다(§5-3).</summary>
        public void ChangeLine(string newLineId)
        {
            if (string.IsNullOrEmpty(newLineId)) return;
            CurrentLineId = newLineId;
            _activeLines.Add(newLineId);
            StateChanged?.Invoke();
        }

        /// <summary>역 1칸 전진(해소 단위, §2-1). dir은 현재 노선 인덱스 기준 진행 방향.</summary>
        public void StepTo(string nextStationId, int dir)
        {
            CurrentStationId = nextStationId;
            if (dir != 0) Direction = dir;
            SyncLocation();
            StateChanged?.Invoke();
        }

        public bool HasVisitedLine(string lineId) => _activeLines.Contains(lineId);

        void SyncLocation()
        {
            if (locationData != null) locationData.currentStationId = CurrentStationId;
        }
    }
}
