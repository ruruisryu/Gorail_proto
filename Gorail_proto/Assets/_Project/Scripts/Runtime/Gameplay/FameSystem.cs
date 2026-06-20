using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 명성(Fame) 자원(scene_system_spec §5). 무활동 시 게임 시간 기준 감소(하한 0).
    /// ArtworkSystem이 작품활동 결과를 OnArtworkCompleted()로 전달한다.
    /// GameTimeSystem.TimeChanged를 구독해 게임 내 시간이 흐를 때만 감소가 적용된다.
    /// </summary>
    public class FameSystem : MonoBehaviour
    {
        [Header("명성 감소 — 무활동 시 (§5-2) / 기획서: 120분 유예 후 5분마다 2씩")]
        [Tooltip("분당 명성 감소량. 5분마다 2씩 = 분당 0.4.")]
        [SerializeField] private float fameDecayPerMinute = 0.4f;
        [Tooltip("마지막 작품활동 성공 후 감소 시작까지 유예(분). 기획서: 120분.")]
        [SerializeField] private float fameDecayGraceMinutes = 120f;

        GameTimeSystem GameTime => Game.Core.GameCore.Instance?.GameTime;

        public float CurrentFame { get; private set; }

        /// <summary>명성이 바뀔 때 발생(증가·감소 공통). WantedSystem이 구독.</summary>
        public event System.Action<float> FameChanged;

        private int _totalMinutesAtLastArtwork;
        private int _prevTotalMinutes;

        /// <summary>GameTimeSystem.Initialize() 이후에 GameCore에서 명시적으로 호출해야 한다.</summary>
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
            if (GameTime != null) GameTime.TimeChanged -= OnGameTimeChanged;
        }

        void OnGameTimeChanged(int day, int hour, int minute)
        {
            int now   = TotalMinutes(GameTime);
            int delta = now - _prevTotalMinutes;
            _prevTotalMinutes = now;
            if (delta <= 0) return;

            if (fameDecayPerMinute <= 0f || CurrentFame <= 0f) return;

            int minutesSinceArtwork = now - _totalMinutesAtLastArtwork;
            if (minutesSinceArtwork <= fameDecayGraceMinutes) return;

            SetFame(CurrentFame - fameDecayPerMinute * delta);
        }

        /// <summary>
        /// ArtworkSystem이 작품활동 완료 시 호출. 유예 타이머 리셋 → 시간 전진 → 명성 증가 순으로 처리.
        /// fameGain이 0이면 명성 변화 없음(실패).
        /// </summary>
        public void OnArtworkCompleted(float fameGain)
        {
            if (GameTime != null)
                _totalMinutesAtLastArtwork = TotalMinutes(GameTime);

            if (fameGain > 0f) SetFame(CurrentFame + fameGain);
        }

        /// <summary>명성을 직접 설정(디버그·초기화). 하한 0.</summary>
        public void SetFame(float value)
        {
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(clamped, CurrentFame)) return;
            CurrentFame = clamped;
            FameChanged?.Invoke(CurrentFame);
        }

        static int TotalMinutes(GameTimeSystem gt)
            => (gt.Day - 1) * (gt.dayEndHour - gt.dayStartHour) * 60
               + (gt.Hour - gt.dayStartHour) * 60
               + gt.Minute;
    }
}
