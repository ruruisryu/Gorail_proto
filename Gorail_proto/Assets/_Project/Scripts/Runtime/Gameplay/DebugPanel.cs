using UnityEngine;
using Game.Core;
using Game.Data;

namespace Game.Gameplay
{
    /// <summary>
    /// ④ DebugPanel(§1-1·§16). 자동 변동(§11)이 들어오기 전, 기획자가 수동으로 조종하는 손잡이.
    /// 수배도(0~5)·통과율·연출 속도를 실시간 조정하고, 승강장 행동·세션 리셋을 직접 트리거한다.
    ///
    /// IMGUI(OnGUI)로 그린다 — 별도 Canvas·프리팹 배선이 필요 없어 시각 확인 없이도 안전하게 동작하는
    /// 디버그 오버레이. 정식 UI는 §13(별도 기획자) 영역이라 침범하지 않는다.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        [SerializeField] private GameManager        gameManager;
        [SerializeField] private ChaseConfig        config;
        [SerializeField] private TrackerManager     trackerManager;
        [SerializeField] private PlatformController platform;
        [SerializeField] private Player             player;
        [SerializeField] private TurnResolver       turnResolver;
        [SerializeField] private DebugMover         debugMover;

        [SerializeField] private bool show = true;

        int _lastWanted = -1;
        Vector2 _scroll;

        void OnGUI()
        {
            // 항상 보이는 토글 버튼 (Input System 의존 없이 안전)
            if (GUI.Button(new Rect(10, 10, 90, 26), show ? "DBG ▲" : "DBG ▼"))
                show = !show;
            if (!show) return;

            GUILayout.BeginArea(new Rect(10, 44, 320, 560), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawStatus();
            GUILayout.Space(6);
            DrawWanted();
            GUILayout.Space(6);
            DrawConfigSliders();
            GUILayout.Space(6);
            DrawPlatform();
            GUILayout.Space(6);
            DrawSession();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawStatus()
        {
            GUILayout.Label("── 상태 ──");
            if (player != null)
            {
                GUILayout.Label($"역: {player.CurrentStationId}  노선: {player.CurrentLineId}  방향: {player.Direction}");
                GUILayout.Label($"활성 노선: {player.ActiveLines.Count}");
            }
            if (trackerManager != null) GUILayout.Label($"추격자: {trackerManager.Trackers.Count}");
            if (turnResolver != null)   GUILayout.Label($"이동중: {turnResolver.IsMoving}");
            if (gameManager != null)    GUILayout.Label($"게임오버: {gameManager.IsGameOver}");
        }

        void DrawWanted()
        {
            if (gameManager == null) return;
            if (_lastWanted < 0) _lastWanted = gameManager.WantedLevel;

            GUILayout.Label($"수배도: {gameManager.WantedLevel}  (노선당 상한 {Cap()})");
            int w = Mathf.RoundToInt(GUILayout.HorizontalSlider(gameManager.WantedLevel, 0, GameManager.MaxWantedLevel));
            if (w != _lastWanted)
            {
                gameManager.SetWantedLevel(w);
                // 하락 시 즉시 상한 초과분 제거(§5-5), 상승은 다음 하차 스폰(§5-1)에서 반영
                if (w < _lastWanted && trackerManager != null) trackerManager.TrimToCaps();
                _lastWanted = w;
            }
        }

        int Cap() => config != null && gameManager != null ? config.PerLineCap(gameManager.WantedLevel) : 0;

        void DrawConfigSliders()
        {
            if (config == null) return;
            GUILayout.Label("── 튜닝 ──");

            GUILayout.Label($"검문 통과율: {config.inspectionPassRate:P0}");
            config.inspectionPassRate = GUILayout.HorizontalSlider(config.inspectionPassRate, 0f, 1f);

            GUILayout.Label($"연출 속도(초/역): {config.stepAnimSeconds:0.00}");
            config.stepAnimSeconds = GUILayout.HorizontalSlider(config.stepAnimSeconds, 0.02f, 1.5f);

            config.inspectAtMidStations =
                GUILayout.Toggle(config.inspectAtMidStations, "중간역 검문(§8-1)");
        }

        void DrawPlatform()
        {
            if (platform == null) return;
            GUILayout.Label("── 승강장 ──");
            if (!platform.IsOpen) { GUILayout.Label("(닫힘 — 도착 시 열림)"); return; }

            GUILayout.Label($"승강장 @ {platform.CurrentStation}");
            if (GUILayout.Button("반대방향 재탑승")) platform.ReverseDirection();
            if (GUILayout.Button("가던 방향 재탑승")) platform.ContinueForward();
            foreach (var line in platform.AvailableTransferLines)
                if (GUILayout.Button($"환승 → {line}")) platform.Transfer(line);
            if (GUILayout.Button("야외로(stub)")) platform.GoOutside();
        }

        void DrawSession()
        {
            GUILayout.Label("── 세션 ──");
            if (GUILayout.Button("세션 리셋"))
            {
                if (trackerManager != null) trackerManager.ResetAll();
                if (gameManager != null)    gameManager.ResetSession();
                if (debugMover != null)     debugMover.BootstrapSession();
            }
            if (GUILayout.Button("스폰 갱신(하차 시뮬)") && trackerManager != null)
                trackerManager.OnPlayerDisembark();
        }
    }
}
