using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 세션 상태의 단일 소유자. 게임오버만 권위 있게 관리한다.
    /// 수배도는 WantedSystem으로 이전됨.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public bool IsGameOver { get; private set; }

        public event System.Action<string> GameOverOccurred;

        /// <summary>게임오버 처리(검문 실패 단일 조건 §8-3).</summary>
        public void TriggerGameOver(string reason)
        {
            if (IsGameOver) return;
            IsGameOver = true;
            Debug.Log($"[GameManager] 게임오버 — {reason}");
            GameOverOccurred?.Invoke(reason);
        }

        /// <summary>세션 재시작용 상태 초기화. 위치·추격자 리셋은 각 시스템이 담당.</summary>
        public void ResetSession()
        {
            IsGameOver = false;
        }
    }
}
