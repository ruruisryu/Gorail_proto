using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 세션 상태의 단일 소유자(§15 GameManager). 이 슬라이스에서는 수배도 레벨과
    /// 게임오버만 권위 있게 들고 있고, 자원(명성·돈·시간)은 디버그 stub이다(§1-1).
    ///
    /// 수배도(0~5)는 ⑤스폰 상한(§5-2)·⑤⑤감소 제거(§5-5)를 구동하는 핵심 손잡이라
    /// 자동 변동(§11)이 들어오기 전까지 DebugPanel이 직접 세팅한다.
    /// 게임오버는 이 슬라이스에서 검문 실패 단일 조건(§8-3).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const int MaxWantedLevel = 5;

        [SerializeField] private int wantedLevel = 0;

        /// <summary>현재 수배도 레벨(0~5). 변경 시 OnWantedChanged 발생.</summary>
        public int WantedLevel => wantedLevel;

        public bool IsGameOver { get; private set; }

        public event System.Action<int> WantedChanged;
        public event System.Action<string> GameOverOccurred; // reason

        /// <summary>수배도를 0~5로 설정(디버그 슬라이더·자동 변동 공통 진입점).</summary>
        public void SetWantedLevel(int level)
        {
            int clamped = Mathf.Clamp(level, 0, MaxWantedLevel);
            if (clamped == wantedLevel) return;
            wantedLevel = clamped;
            WantedChanged?.Invoke(wantedLevel);
        }

        /// <summary>게임오버 처리(이 슬라이스: 검문 실패 단일 조건 §8-3).</summary>
        public void TriggerGameOver(string reason)
        {
            if (IsGameOver) return;
            IsGameOver = true;
            Debug.Log($"[GameManager] 게임오버 — {reason}");
            GameOverOccurred?.Invoke(reason);
        }

        /// <summary>세션 재시작용 상태 초기화(게임오버 해제). 위치·추격자 리셋은 각 시스템이 담당.</summary>
        public void ResetSession()
        {
            IsGameOver = false;
        }
    }
}
