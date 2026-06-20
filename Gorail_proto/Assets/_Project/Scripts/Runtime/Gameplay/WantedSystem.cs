using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 수배도 시스템(scene_system_spec §4). 수배도는 독립 자원이 아니라 현재 명성이 어느 구간인지로
    /// 실시간 결정된다. FameSystem.FameChanged를 구독해 WantedLevel을 갱신하고,
    /// 하락 시 TrackerManager가 초과 추격자를 제거(§5-5)하게 한다.
    /// </summary>
    public class WantedSystem : MonoBehaviour
    {
        public const int MaxWantedLevel = 5;

        [Header("수배도 — 명성 구간 경계 (§4-1)")]
        [Tooltip("레벨 1~5가 되는 명성 하한. 인덱스 0=레벨1 경계 … 인덱스 4=레벨5 경계. 그 미만이면 레벨0.")]
        [SerializeField] private float[] wantedFameThresholds = { 5f, 25f, 45f, 75f, 200f };

        public int WantedLevel { get; private set; }

        public event System.Action<int> WantedChanged;

        FameSystem     Fame     => GameCore.Instance?.Fame;
        TrackerManager Trackers => GameCore.Instance?.Trackers;

        void Start()
        {
            Fame.FameChanged += OnFameChanged;
            Recalculate();
        }

        void OnDestroy()
        {
            if (Fame != null) Fame.FameChanged -= OnFameChanged;
        }

        void OnFameChanged(float fame) => Recalculate();

        /// <summary>현재 명성 → 수배도 레벨 환산 후 반영(§4-2).</summary>
        public void Recalculate()
        {
            if (Fame == null) return;
            int newLevel = WantedLevelForFame(Fame.CurrentFame);
            SetWantedLevel(newLevel);
        }

        /// <summary>수배도를 0~5로 직접 설정(디버그·초기화 공통 진입점).</summary>
        public void SetWantedLevel(int level)
        {
            int clamped = Mathf.Clamp(level, 0, MaxWantedLevel);
            if (clamped == WantedLevel) return;
            int prev = WantedLevel;
            WantedLevel = clamped;
            WantedChanged?.Invoke(WantedLevel);
            if (WantedLevel < prev && Trackers != null) Trackers.TrimToCaps();
        }

        /// <summary>명성값 → 수배도 레벨(0~5). 경계표와 비교(§4-1).</summary>
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
