using UnityEngine;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// ② 검증용 디버그 입력 + 세션 부트스트랩.
    ///
    /// - Start 시 PlayerLocationData의 현재 역과 그 역의 첫 노선으로 Player를 초기화한다.
    ///   (G단계에서 GameManager 주도 세션 시작으로 이관 예정.)
    /// - 인스펙터의 destinationStationId로 TryMoveTo를 호출(컨텍스트 메뉴/공개 메서드).
    ///   역 클릭 입력(StationView)이 붙기 전, 코루틴 순차 해소를 빠르게 확인하는 용도.
    /// </summary>
    public class DebugMover : MonoBehaviour
    {
        [SerializeField] private Player             player;
        [SerializeField] private TurnResolver       turnResolver;
        [SerializeField] private MapGraphProvider   graphProvider;
        [SerializeField] private PlayerLocationData playerLocation;

        [Tooltip("시작 노선을 직접 지정(비우면 시작 역의 첫 노선 자동 선택).")]
        [SerializeField] private string startLineIdOverride = "";

        [Tooltip("이동 목적지 역 ID. Move() 호출 시 사용.")]
        [SerializeField] private string destinationStationId = "";

        void Start()
        {
            BootstrapSession();
        }

        /// <summary>시작 역·노선으로 Player를 초기화(② 디버그 부트스트랩).</summary>
        public void BootstrapSession()
        {
            if (player == null || graphProvider == null || graphProvider.Graph == null) return;
            if (playerLocation == null || string.IsNullOrEmpty(playerLocation.currentStationId))
            {
                Debug.LogWarning("[DebugMover] PlayerLocation 미설정 — 시작 역을 정할 수 없음");
                return;
            }

            string startStation = playerLocation.currentStationId;
            string startLine = !string.IsNullOrEmpty(startLineIdOverride)
                ? startLineIdOverride
                : FirstLineOf(startStation);

            if (string.IsNullOrEmpty(startLine))
            {
                Debug.LogWarning($"[DebugMover] 시작 역 '{startStation}'의 노선을 찾을 수 없음");
                return;
            }

            player.Initialize(startStation, startLine);
            Debug.Log($"[DebugMover] 세션 시작 — 역:{startStation} 노선:{startLine}");
        }

        string FirstLineOf(string stationId)
        {
            var lines = graphProvider.Graph.GetLineIds(stationId);
            return lines != null && lines.Count > 0 ? lines[0] : null;
        }

        /// <summary>인스펙터 destinationStationId로 이동 시도(컨텍스트 메뉴에서 실행).</summary>
        [ContextMenu("Move To Destination")]
        public void Move()
        {
            if (turnResolver == null) { Debug.LogWarning("[DebugMover] turnResolver 미할당"); return; }
            turnResolver.TryMoveTo(destinationStationId);
        }

        /// <summary>외부(버튼·역 클릭)에서 목적지를 지정해 이동.</summary>
        public void MoveTo(string stationId)
        {
            destinationStationId = stationId;
            Move();
        }
    }
}
