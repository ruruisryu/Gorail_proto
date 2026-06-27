using UnityEngine;
using Game.Subway;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 역 클릭 입력(②)을 TurnResolver로 잇는 라우터.
    /// StationView(렌더링 레이어)는 클릭을 이벤트로만 발신하고, 게임플레이 쪽인
    /// 이 컴포넌트가 구독해 이동을 시도한다 — 레이어 의존 방향(게임플레이→렌더링)을 지킨다.
    /// </summary>
    public class StationClickRouter : MonoBehaviour
    {
        TurnResolver turnResolver => GameCore.Instance?.TurnResolver;

        void Start()  => StationView.StationClicked += OnStationClicked;
        void OnDestroy() => StationView.StationClicked -= OnStationClicked;

        void OnStationClicked(string stationId)
        {
            SoundManager.Instance.PlaySFX(turnResolver.TryMoveTo(stationId) ? "ui_click" : "ui_click_disabled");
        }
    }
}
