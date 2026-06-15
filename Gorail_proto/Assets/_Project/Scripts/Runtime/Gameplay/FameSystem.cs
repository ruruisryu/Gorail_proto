using UnityEngine;
using Game.Data;

namespace Game.Gameplay
{
    public enum ArtworkGrade { High, Mid, Low }

    /// <summary>
    /// 명성(Fame) 자원(scene_system_spec §5). 작품활동 성공 시 증가, 무활동 시 게임 시간 기준 감소(하한 0).
    /// GameTimeSystem.TimeChanged를 구독해 게임 내 시간이 흐를 때만 감소가 적용된다.
    /// </summary>
    public class FameSystem : MonoBehaviour
    {
        [SerializeField] private SceneConfig config;
        [SerializeField] private RngService  rng;

        GameTimeSystem GameTime => Game.Core.GameCore.Instance?.GameTime;

        public float CurrentFame { get; private set; }

        /// <summary>명성이 바뀔 때 발생(증가·감소 공통). WantedSystem이 구독.</summary>
        public event System.Action<float> FameChanged;

        // 마지막 작품활동 시점의 게임 내 총 분(GameTimeSystem 기준)
        private int _totalMinutesAtLastArtwork;
        // 마지막 TimeChanged 때의 총 분 — 델타 계산용
        private int _prevTotalMinutes;

        /// <summary>
        /// GameTimeSystem.Initialize() 이후에 GameCore에서 명시적으로 호출해야 한다.
        /// Start()에서 호출하면 초기화 순서가 보장되지 않아 delta 계산이 틀린다.
        /// </summary>
        public void Initialize()
        {
            if (GameTime == null) return;
            int now = TotalMinutes(GameTime);
            _prevTotalMinutes          = now;
            _totalMinutesAtLastArtwork = now;
            GameTime.TimeChanged += OnGameTimeChanged;
        }

        void OnDestroy()
        {
            if (GameTime != null)
                GameTime.TimeChanged -= OnGameTimeChanged;
        }

        void OnGameTimeChanged(int day, int hour, int minute)
        {
            int now   = TotalMinutes(GameTime);
            int delta = now - _prevTotalMinutes;
            _prevTotalMinutes = now;
            if (delta <= 0) return;

            if (config == null || config.fameDecayPerMinute <= 0f) return;
            if (CurrentFame <= 0f) return;

            int minutesSinceArtwork = now - _totalMinutesAtLastArtwork;
            if (minutesSinceArtwork <= config.fameDecayGraceMinutes) return;

            SetFame(CurrentFame - config.fameDecayPerMinute * delta);
        }

        /// <summary>작품활동 결과(§5-1). 성공 시 완성도별 명성 증가 + 시간 소모. 실패 시 시간만 소모.</summary>
        public void OnArtworkResult(ArtworkGrade grade, bool success)
        {
            // 시간 소모 (성공 여부와 무관)
            int artworkMinutes = grade switch
            {
                ArtworkGrade.High => config != null ? RandRangeInt(config.artworkHighMinMin, config.artworkHighMinMax) : RandRangeInt(25, 35),
                ArtworkGrade.Mid  => config != null ? RandRangeInt(config.artworkMidMinMin,  config.artworkMidMinMax)  : RandRangeInt(15, 25),
                _                 => config != null ? RandRangeInt(config.artworkLowMinMin,  config.artworkLowMinMax)  : RandRangeInt(10, 20),
            };
            // 유예 타이머를 Advance 전에 리셋 — Advance가 TimeChanged를 즉시 발생시키므로
            // 핸들러 안에서 minutesSinceArtwork가 올바르게 0으로 읽히려면 먼저 갱신해야 한다.
            if (GameTime != null) _totalMinutesAtLastArtwork = TotalMinutes(GameTime) + artworkMinutes;
            GameTime?.Advance(artworkMinutes);

            if (!success) return;

            float baseV, variance;
            switch (grade)
            {
                case ArtworkGrade.High: baseV = Cfg(config?.fameHighBase, 30f); variance = Cfg(config?.fameHighVar, 6f); break;
                case ArtworkGrade.Mid:  baseV = Cfg(config?.fameMidBase,  20f); variance = Cfg(config?.fameMidVar,  4f); break;
                default:                baseV = Cfg(config?.fameLowBase,  10f); variance = Cfg(config?.fameLowVar,  2f); break;
            }
            float delta = baseV + RandRange(-variance, variance);
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

        // GameTimeSystem의 누적 게임 분 (Day 1 = 0분 기준)
        static int TotalMinutes(GameTimeSystem gt)
            => (gt.Day - 1) * (gt.dayEndHour - gt.dayStartHour) * 60
               + (gt.Hour - gt.dayStartHour) * 60
               + gt.Minute;

        static float Cfg(float? v, float fallback) => v ?? fallback;
        static int   Cfg(int? v,   int   fallback) => v ?? fallback;
        float RandRange(float a, float b) => rng != null ? a + (b - a) * rng.Value01() : Random.Range(a, b);
        int   RandRangeInt(int a, int b)  => rng != null ? a + Mathf.FloorToInt((b - a + 1) * rng.Value01()) : Random.Range(a, b + 1);
    }
}
