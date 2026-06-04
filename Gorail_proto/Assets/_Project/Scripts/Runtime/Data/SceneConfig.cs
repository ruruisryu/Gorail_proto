using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 승강장·지상 씬 + 명성·수배도 시스템의 튜닝값(scene_system_spec §6). 모든 수치를 한 곳에.
    /// 대부분 [미확정] 자리표시자 — 플레이테스트로 확정.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneConfig", menuName = "Chase/Scene Config")]
    public class SceneConfig : ScriptableObject
    {
        [Header("작품활동 명성 증가 — 성공 시 (§5-1)")]
        [Tooltip("완성도 상: base ± variance")]
        public float fameHighBase = 30f; public float fameHighVar = 6f;
        [Tooltip("완성도 중")]
        public float fameMidBase  = 20f; public float fameMidVar  = 4f;
        [Tooltip("완성도 하")]
        public float fameLowBase  = 10f; public float fameLowVar  = 2f;

        [Header("명성 감소 — 무활동 시 (§5-2)")]
        [Tooltip("분당 명성 감소량(전역, 모든 씬에서 진행). 하한 0.")]
        public float fameDecayPerMinute = 1f;
        [Tooltip("마지막 작품활동 성공 후 감소 시작까지 유예(분). 0이면 즉시 감소.")]
        public float fameDecayGraceMinutes = 0f;

        [Header("수배도 — 명성 구간 경계 (§4-1)")]
        [Tooltip("레벨 1~5가 되는 명성 하한. 인덱스 0=레벨1 경계 … 인덱스 4=레벨5 경계. 그 미만이면 레벨0.")]
        public float[] wantedFameThresholds = { 5f, 25f, 45f, 75f, 200f };

        [Header("지상 씬 (§3)")]
        [Tooltip("외부 체류 강제 도주 시간(분). 초과 시 나간 역 승강장으로 복귀.")]
        public float forceExitMinutes = 30f;
        [Tooltip("외부 체류 1분당 추격자 전진 역 수(§9-1 체류→추격 전진).")]
        public float trackerStepsPerOutsideMinute = 1f;

        /// <summary>명성값 → 수배도 레벨(0~5). 경계표와 비교(§4-1). 순수 함수 — 테스트 대상.</summary>
        public int WantedLevelForFame(float fame)
        {
            if (wantedFameThresholds == null) return 0;
            int level = 0;
            for (int i = 0; i < wantedFameThresholds.Length; i++)
                if (fame >= wantedFameThresholds[i]) level = i + 1; else break;
            return level;
        }
    }
}
