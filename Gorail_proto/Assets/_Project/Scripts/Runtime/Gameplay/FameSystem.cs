using UnityEngine;
using Game.Data;

namespace Game.Gameplay
{
    public enum ArtworkGrade { High, Mid, Low }

    /// <summary>
    /// 명성(Fame) 자원(scene_system_spec §5). 작품활동 성공 시 증가, 무활동 시 시간 감소(하한 0).
    /// 수배도는 이 값의 구간으로 자동 결정되므로(§4), 명성이 단일 손잡이다.
    /// 전역 자원이라 모든 씬에서 감소가 진행된다 → 영속 매니저에 둔다(S3).
    /// </summary>
    public class FameSystem : MonoBehaviour
    {
        [SerializeField] private SceneConfig config;
        [SerializeField] private RngService rng; // 선택: 명성 증가 ± 랜덤 재현성

        public float CurrentFame { get; private set; }

        /// <summary>명성이 바뀔 때 발생(증가·감소 공통). WantedSystem이 구독.</summary>
        public event System.Action<float> FameChanged;

        private float _minutesSinceArtwork;

        void Update()
        {
            // 무활동 감소(§5-2): 유예 후 분당 감소, 하한 0
            if (config == null || config.fameDecayPerMinute <= 0f) return;
            float dtMin = Time.deltaTime / 60f;
            _minutesSinceArtwork += dtMin;
            if (_minutesSinceArtwork < config.fameDecayGraceMinutes) return;
            if (CurrentFame <= 0f) return;
            SetFame(CurrentFame - config.fameDecayPerMinute * dtMin);
        }

        /// <summary>작품활동 결과(§5-1). 성공 시 완성도별 명성 증가, 실패 시 변화 없음.</summary>
        public void OnArtworkResult(ArtworkGrade grade, bool success)
        {
            _minutesSinceArtwork = 0f; // 감소 타이머 리셋(§5-4)
            if (!success) return;

            float baseV, var;
            switch (grade)
            {
                case ArtworkGrade.High: baseV = Cfg(config?.fameHighBase, 30f); var = Cfg(config?.fameHighVar, 6f); break;
                case ArtworkGrade.Mid:  baseV = Cfg(config?.fameMidBase, 20f);  var = Cfg(config?.fameMidVar, 4f);  break;
                default:                baseV = Cfg(config?.fameLowBase, 10f);  var = Cfg(config?.fameLowVar, 2f);  break;
            }
            float delta = baseV + RandRange(-var, var);
            SetFame(CurrentFame + delta);
            Debug.Log($"[FameSystem] 작품활동 {grade} 성공 +{delta:0.0} → 명성 {CurrentFame:0.0}");
        }

        /// <summary>명성을 직접 설정(디버그·초기화). 하한 0.</summary>
        public void SetFame(float value)
        {
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(clamped, CurrentFame)) return;
            CurrentFame = clamped;
            FameChanged?.Invoke(CurrentFame);
        }

        static float Cfg(float? v, float fallback) => v ?? fallback;
        float RandRange(float a, float b) => rng != null ? a + (b - a) * rng.Value01() : Random.Range(a, b);
    }
}
