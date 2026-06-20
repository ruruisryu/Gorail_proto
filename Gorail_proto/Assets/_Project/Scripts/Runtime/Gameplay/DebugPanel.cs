using UnityEngine;
using Game.Core;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// ④ DebugPanel(§1-1·§16). 자동 변동(§11)이 들어오기 전, 기획자가 수동으로 조종하는 손잡이.
    /// 수배도(0~5)·통과율·연출 속도를 실시간 조정하고, 승강장 행동·세션 리셋을 직접 트리거한다.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        [SerializeField] private DebugMover   debugMover;
        [SerializeField] private bool         show = true;

        Vector2 _scroll;

        GameManager       Manager    => GameCore.Instance?.Game;
        FameSystem        Fame       => GameCore.Instance?.Fame;
        WantedSystem      Wanted     => GameCore.Instance?.Wanted;
        TrackerManager    Trackers   => GameCore.Instance?.Trackers;
        TurnResolver      Resolver   => GameCore.Instance?.TurnResolver;
        InspectionSystem  Inspection => GameCore.Instance?.Inspection;
        Player            Player     => GameCore.Instance?.Player;

        void OnGUI()
        {
            if (GUI.Button(new Rect(10, 10, 90, 26), show ? "DBG ▲" : "DBG ▼"))
                show = !show;
            if (!show) return;

            GUILayout.BeginArea(new Rect(10, 44, 320, 560), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawStatus();
            GUILayout.Space(6);
            DrawWanted();
            GUILayout.Space(6);
            DrawSpace();
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
            var p = Player;
            if (p != null)
            {
                GUILayout.Label($"역: {p.CurrentStationId}  노선: {p.CurrentLineId}  방향: {p.Direction}");
                GUILayout.Label($"활성 노선: {p.ActiveLines.Count}");
            }
            if (Trackers != null) GUILayout.Label($"추격자: {Trackers.Trackers.Count}");
            if (Resolver != null) GUILayout.Label($"이동중: {Resolver.IsMoving}");
            if (Manager  != null) GUILayout.Label($"게임오버: {Manager.IsGameOver}");
        }

        void DrawWanted()
        {
            GUILayout.Label("── 명성·수배도 ──");
            var fame   = Fame;
            var wanted = Wanted;

            if (fame != null)
            {
                int wLevel = wanted != null ? wanted.WantedLevel : 0;
                GUILayout.Label($"명성: {fame.CurrentFame:0.0}  → 수배도 {wLevel} (노선당 상한 {Cap()})");
                float f = GUILayout.HorizontalSlider(fame.CurrentFame, 0f, 250f);
                if (!Mathf.Approximately(f, fame.CurrentFame)) fame.SetFame(f);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("작품 상")) GameCore.Instance?.Artwork?.StartArtwork(ArtworkGrade.High);
                if (GUILayout.Button("중"))     GameCore.Instance?.Artwork?.StartArtwork(ArtworkGrade.Mid);
                if (GUILayout.Button("하"))     GameCore.Instance?.Artwork?.StartArtwork(ArtworkGrade.Low);
                GUILayout.EndHorizontal();
            }
            else if (wanted != null)
            {
                // 폴백: FameSystem 미연결 시 수배도 직접 슬라이더
                GUILayout.Label($"수배도: {wanted.WantedLevel} (노선당 상한 {Cap()})");
                int w = Mathf.RoundToInt(GUILayout.HorizontalSlider(wanted.WantedLevel, 0, WantedSystem.MaxWantedLevel));
                if (w != wanted.WantedLevel) wanted.SetWantedLevel(w);
            }
        }

        int Cap() => Trackers != null && Wanted != null ? Trackers.PerLineCap(Wanted.WantedLevel) : 0;

        void DrawConfigSliders()
        {
            GUILayout.Label("── 튜닝 ──");

            var insp = Inspection;
            if (insp != null)
            {
                GUILayout.Label($"검문 통과율: {insp.InspectionPassRate:P0}");
                insp.InspectionPassRate = GUILayout.HorizontalSlider(insp.InspectionPassRate, 0f, 1f);
            }

            var tr = Resolver;
            if (tr != null)
            {
                GUILayout.Label($"연출 속도(초/역): {tr.StepAnimSeconds:0.00}");
                tr.StepAnimSeconds = GUILayout.HorizontalSlider(tr.StepAnimSeconds, 0.02f, 1.5f);

                tr.InspectAtMidStations = GUILayout.Toggle(tr.InspectAtMidStations, "중간역 검문(§8-1)");
            }
        }

        void DrawSpace()
        {
            var core = GameCore.Instance;
            if (core == null || core.Space == null) return;
            GUILayout.Label($"── 공간: {core.Space.Current} ──");
            string stn = core.Player != null ? core.Player.CurrentStationId : "?";
            if (GUILayout.Button($"승강장 진입 (현재역 {stn})"))
                core.Platform?.OpenAt(stn);
            if (GUILayout.Button("지상 강제 진입 (현재역)"))
                core.Space.EnterGround(stn);
            if (GUILayout.Button("지하철 복귀"))
                core.Space.EnterSubway();
        }

        void DrawSession()
        {
            GUILayout.Label("── 세션 ──");
            if (GUILayout.Button("세션 리셋"))
            {
                Trackers?.ResetAll();
                Manager?.ResetSession();
                debugMover?.BootstrapSession();
            }
            if (GUILayout.Button("스폰 갱신(하차 시뮬)"))
                Trackers?.OnPlayerDisembark();
        }
    }
}
