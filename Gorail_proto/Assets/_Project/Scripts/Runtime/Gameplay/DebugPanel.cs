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
        [SerializeField] private FameSystem         fameSystem;
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
            GUILayout.Label("── 명성·수배도 ──");

            // 수배도는 명성 구간으로 자동 결정(scene_system_spec §4) — 디버그는 명성으로 통일.
            if (fameSystem != null)
            {
                int wanted = gameManager != null ? gameManager.WantedLevel : 0;
                GUILayout.Label($"명성: {fameSystem.CurrentFame:0.0}  → 수배도 {wanted} (노선당 상한 {Cap()})");
                float f = GUILayout.HorizontalSlider(fameSystem.CurrentFame, 0f, 250f);
                if (!Mathf.Approximately(f, fameSystem.CurrentFame)) fameSystem.SetFame(f);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("작품 상")) fameSystem.OnArtworkResult(ArtworkGrade.High, true);
                if (GUILayout.Button("중"))     fameSystem.OnArtworkResult(ArtworkGrade.Mid, true);
                if (GUILayout.Button("하"))     fameSystem.OnArtworkResult(ArtworkGrade.Low, true);
                GUILayout.EndHorizontal();
            }
            else if (gameManager != null)
            {
                // 폴백: FameSystem 미연결 시 수배도 직접 슬라이더
                if (_lastWanted < 0) _lastWanted = gameManager.WantedLevel;
                GUILayout.Label($"수배도: {gameManager.WantedLevel} (노선당 상한 {Cap()})");
                int w = Mathf.RoundToInt(GUILayout.HorizontalSlider(gameManager.WantedLevel, 0, GameManager.MaxWantedLevel));
                if (w != _lastWanted)
                {
                    gameManager.SetWantedLevel(w);
                    if (w < _lastWanted && trackerManager != null) trackerManager.TrimToCaps();
                    _lastWanted = w;
                }
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
