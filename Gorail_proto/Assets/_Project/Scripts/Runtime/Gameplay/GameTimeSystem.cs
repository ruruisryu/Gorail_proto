using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 게임 내 시간 관리(자원기획서 §3). 1턴 = 5분. 하루 07:00~22:00.
    /// 22:00 도달 시 DayEnded 발생 — 씬 전환 연출은 구독자가 담당.
    /// </summary>
    public class GameTimeSystem : MonoBehaviour
    {
        [Header("시간 설정")]
        [SerializeField] public int minutesPerMove    = 5;  // 역 이동 1칸
        [SerializeField] public int minutesTransfer   = 5;  // 환승
        [SerializeField] public int minutesReboard    = 5;  // 반대방향 재탑승
        [SerializeField] public int dayStartHour      = 7;
        [SerializeField] public int dayEndHour        = 22;

        public int Day    { get; private set; } = 1;
        public int Hour   { get; private set; } = 7;
        public int Minute { get; private set; } = 0;

        /// <summary>시간이 바뀔 때마다 발생 (day, hour, minute).</summary>
        public event System.Action<int, int, int> TimeChanged;

        /// <summary>22:00 도달 → 다음 날 전환 직전에 발생.</summary>
        public event System.Action<int> DayEnded;

        public void Initialize()
        {
            Day    = 1;
            Hour   = dayStartHour;
            Minute = 0;
            TimeChanged?.Invoke(Day, Hour, Minute);
        }

        /// <summary>지정 분 만큼 게임 시간을 전진시킨다.</summary>
        public void Advance(int minutes)
        {
            if (minutes <= 0) return;
            int total = Hour * 60 + Minute + minutes;

            int endMinutes = dayEndHour * 60;
            if (total >= endMinutes)
            {
                Hour   = dayEndHour;
                Minute = 0;
                TimeChanged?.Invoke(Day, Hour, Minute);
                DayEnded?.Invoke(Day);
            }
            else
            {
                Hour   = total / 60;
                Minute = total % 60;
                TimeChanged?.Invoke(Day, Hour, Minute);
            }
        }

        /// <summary>
        /// DayEnded 구독자(페이드 콜백 등)가 연출 완료 후 호출한다.
        /// Day를 올리고 다음 날 07:00으로 TimeChanged를 발생시킨다.
        /// </summary>
        public void BeginNextDay()
        {
            Day++;
            Hour   = dayStartHour;
            Minute = 0;
            TimeChanged?.Invoke(Day, Hour, Minute);
        }

        public string FormatTime() => $"{Hour:00}:{Minute:00}";

        public void Restore(int day, int hour, int minute)
        {
            Day    = day;
            Hour   = hour;
            Minute = minute;
            TimeChanged?.Invoke(Day, Hour, Minute);
        }
    }
}
