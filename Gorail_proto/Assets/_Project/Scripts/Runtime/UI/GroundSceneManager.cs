using UnityEngine;
using Game.Core;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// [S4] 지상 씬(scene_system_spec §3). 체류 타이머 + 작품활동 6버튼(상/중/하 × 성공/실패) +
    /// 강제도주. 복귀 시 체류 분만큼 추격자 전진(§9-1) 후 나간 역 승강장으로,
    /// 같은 역에 추격자가 있으면 즉시 검문(확률 게이트).
    /// </summary>
    public class GroundSceneManager : MonoBehaviour
    {
        [Header("지상 씬 설정 (§3)")]
        //[Tooltip("외부 체류 강제 도주 시간(분). 초과 시 나간 역 승강장으로 복귀.")]
        //[SerializeField] private float forceExitMinutes = 30f;

        private int   _stayGameMinutes;  // 게임 시간 기준 체류 분
        private int   _enterGameMinutes; // 진입 시점의 게임 누적 분
        private bool  _returning;

        GameTimeSystem GameTime => GameCore.Instance?.GameTime;

        void OnEnable()
        {
            _stayGameMinutes = 0;
            _returning       = false;

            var gt = GameTime;
            if (gt != null)
            {
                _enterGameMinutes = TotalMinutes(gt);
                gt.TimeChanged += OnGameTimeChanged;
            }
        }

        void OnDisable()
        {
            var gt = GameTime;
            if (gt != null) gt.TimeChanged -= OnGameTimeChanged;
        }

        void OnGameTimeChanged(int day, int hour, int minute)
        {
            if (_returning) return;
            var gt = GameTime;
            if (gt == null) return;

            _stayGameMinutes = TotalMinutes(gt) - _enterGameMinutes;

            //float limit = forceExitMinutes;
            //if (_stayGameMinutes >= limit) ReturnToSubway(true);
        }

        void OnGUI()
        {
            var core = GameCore.Instance;
            if (core == null) return;
            //float limit = forceExitMinutes;

            var prevC = GUI.color;
            GUI.color = new Color(0.10f, 0.17f, 0.16f, 0.97f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevC;
            GUI.Label(new Rect(0, 24f, Screen.width, 30f), "■ 지상 (Ground)");

            GUILayout.BeginArea(new Rect(Screen.width / 2f - 190f, 70f, 380f, 380f), GUI.skin.box);
            GUILayout.Label($"=== 지상 @ {core.Space?.CurrentStationId} ===");
            //GUILayout.Label($"체류 {_stayGameMinutes}분(게임) / 강제도주 {limit}분");
            GUILayout.Label($"명성 {core.Fame?.CurrentFame:0.0} · 수배도 {core.Wanted?.WantedLevel}");

            GUILayout.Space(6);
            GUILayout.Label("작품활동 (완성도 × 결과):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("상 성공")) core.Artwork?.OnArtworkResult(ArtworkGrade.High, true);
            if (GUILayout.Button("중 성공")) core.Artwork?.OnArtworkResult(ArtworkGrade.Mid, true);
            if (GUILayout.Button("하 성공")) core.Artwork?.OnArtworkResult(ArtworkGrade.Low, true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("상 실패")) core.Artwork?.OnArtworkResult(ArtworkGrade.High, false);
            if (GUILayout.Button("중 실패")) core.Artwork?.OnArtworkResult(ArtworkGrade.Mid, false);
            if (GUILayout.Button("하 실패")) core.Artwork?.OnArtworkResult(ArtworkGrade.Low, false);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (GUILayout.Button("지하철로 복귀")) ReturnToSubway(false);
            GUILayout.EndArea();
        }

        void ReturnToSubway(bool forced)
        {
            if (_returning) return;
            _returning = true;
            var core = GameCore.Instance;
            if (core == null) return;

            string station   = core.Space?.CurrentStationId;
            int    stayMin   = _stayGameMinutes;
            string logTag    = forced ? "강제도주" : "자발";

            ScreenFader.Instance?.Fade(onBlack: () =>
            {
                // §9-1: 게임 시간 기준 체류 분 → 추격자 전진
                core.Trackers?.AdvanceByMinutes(stayMin);
                Debug.Log($"[Ground] 복귀({logTag}) 체류 {stayMin}분(게임) → 추격자 전진");

                if (core.Platform != null) core.Platform.OpenAt(station);
                else if (core.Space != null) core.Space.EnterPlatform(station);

                if (core.Inspection != null && !string.IsNullOrEmpty(station))
                    core.Inspection.ResolveAt(station);
            });
        }

        static int TotalMinutes(GameTimeSystem gt)
            => (gt.Day - 1) * (gt.dayEndHour - gt.dayStartHour) * 60
               + (gt.Hour - gt.dayStartHour) * 60
               + gt.Minute;
    }
}
