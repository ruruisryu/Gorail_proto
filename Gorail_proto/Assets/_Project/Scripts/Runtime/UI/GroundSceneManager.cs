using UnityEngine;
using Game.Core;
using Game.Gameplay;

namespace Game.UI
{
    /// <summary>
    /// [S4] 지상 씬(scene_system_spec §3). 체류 타이머 + 작품활동 6버튼(상/중/하 × 성공/실패) +
    /// 30분 초과 강제 도주. 복귀 시 체류 분만큼 추격자 전진(§9-1) 후 나간 역 승강장으로,
    /// 같은 역에 추격자가 있으면 즉시 검문(확률 게이트).
    /// </summary>
    public class GroundSceneManager : MonoBehaviour
    {
        private float _stayMinutes;
        private bool  _returning;

        void OnEnable() { _stayMinutes = 0f; _returning = false; }

        void Update()
        {
            var core = GameCore.Instance;
            if (core == null || _returning) return;
            _stayMinutes += Time.deltaTime / 60f;
            float limit = core.SceneConfig != null ? core.SceneConfig.forceExitMinutes : 30f;
            if (_stayMinutes >= limit) ReturnToSubway(true);
        }

        void OnGUI()
        {
            var core = GameCore.Instance;
            if (core == null) return;
            float limit = core.SceneConfig != null ? core.SceneConfig.forceExitMinutes : 30f;

            GUILayout.BeginArea(new Rect(Screen.width / 2f - 190f, 70f, 380f, 380f), GUI.skin.box);
            GUILayout.Label($"=== 지상 @ {core.Space?.CurrentStationId} ===");
            GUILayout.Label($"체류 {_stayMinutes:0.0}분 / 강제도주 {limit}분");
            GUILayout.Label($"명성 {core.Fame?.CurrentFame:0.0} · 수배도 {core.Game?.WantedLevel}");

            GUILayout.Space(6);
            GUILayout.Label("작품활동 (완성도 × 결과):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("상 성공")) core.Fame?.OnArtworkResult(ArtworkGrade.High, true);
            if (GUILayout.Button("중 성공")) core.Fame?.OnArtworkResult(ArtworkGrade.Mid, true);
            if (GUILayout.Button("하 성공")) core.Fame?.OnArtworkResult(ArtworkGrade.Low, true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("상 실패")) core.Fame?.OnArtworkResult(ArtworkGrade.High, false);
            if (GUILayout.Button("중 실패")) core.Fame?.OnArtworkResult(ArtworkGrade.Mid, false);
            if (GUILayout.Button("하 실패")) core.Fame?.OnArtworkResult(ArtworkGrade.Low, false);
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

            string station = core.Space != null ? core.Space.CurrentStationId : null;

            // §9-1: 체류 분 → 추격자 전진
            float perMin = core.SceneConfig != null ? core.SceneConfig.trackerStepsPerOutsideMinute : 1f;
            int steps = Mathf.RoundToInt(_stayMinutes * perMin);
            if (steps > 0 && core.Trackers != null) core.Trackers.AdvanceAll(steps);
            Debug.Log($"[Ground] 복귀({(forced ? "강제도주" : "자발")}) 체류 {_stayMinutes:0.0}분 → 추격 {steps}스텝");

            // 나간 역 승강장으로 복귀
            if (core.Space != null) core.Space.EnterPlatform(station);

            // §9-1: 같은 역에 추격자면 즉시 검문(확률 게이트)
            if (core.Inspection != null && !string.IsNullOrEmpty(station))
                core.Inspection.ResolveAt(station);
        }
    }
}
