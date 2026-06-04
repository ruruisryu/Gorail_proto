using UnityEngine;
using Game.Data;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// 수배도 시스템(scene_system_spec §4). 수배도는 독립 자원이 아니라 현재 명성이 어느 구간인지로
    /// 실시간 결정된다. 명성이 오르면 올라가고, 깎이면 내려간다(별도 감소 타이머 불필요).
    /// FameSystem.FameChanged를 구독해 GameManager.WantedLevel을 갱신하고,
    /// 하락 시 TrackerManager가 초과 추격자를 제거(§5-5)하게 한다.
    /// </summary>
    public class WantedSystem : MonoBehaviour
    {
        [SerializeField] private FameSystem     fameSystem;
        [SerializeField] private GameManager    gameManager;
        [SerializeField] private TrackerManager trackerManager;
        [SerializeField] private SceneConfig    config;

        void OnEnable()
        {
            if (fameSystem != null) fameSystem.FameChanged += OnFameChanged;
            Recalculate();
        }

        void OnDisable()
        {
            if (fameSystem != null) fameSystem.FameChanged -= OnFameChanged;
        }

        void OnFameChanged(float fame) => Recalculate();

        /// <summary>현재 명성 → 수배도 레벨 환산 후 GameManager에 반영(§4-2).</summary>
        public void Recalculate()
        {
            if (config == null || fameSystem == null || gameManager == null) return;
            int newLevel = config.WantedLevelForFame(fameSystem.CurrentFame);
            int prev = gameManager.WantedLevel;
            if (newLevel == prev) return;

            gameManager.SetWantedLevel(newLevel);
            // 하락 시 노선당 상한 초과분 즉시 제거(§5-5)
            if (newLevel < prev && trackerManager != null) trackerManager.TrimToCaps();
        }
    }
}
