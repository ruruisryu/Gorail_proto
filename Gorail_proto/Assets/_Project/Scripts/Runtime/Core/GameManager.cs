using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.UI;

namespace Game.Core
{
    /// <summary>
    /// 세션 상태의 단일 소유자. 게임오버만 권위 있게 관리한다.
    /// 수배도는 WantedSystem으로 이전됨.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("게임오버 연출")]
        [SerializeField] private TMP_Text gameOverText;
        [SerializeField] private string   startSceneName = "StartScene";
        [SerializeField] private float    typeInterval   = 0.07f;  // 글자당 간격(초)
        [SerializeField] private float    holdAfterType  = 1.5f;   // 타이핑 완료 후 대기(초)

        const string GameOverMessage = "내 예술가로서의 수명은 여기서 끝이 났다...";

        public bool IsGameOver { get; private set; }

        public event System.Action<string> GameOverOccurred;

        /// <summary>게임오버 처리(검문 실패 단일 조건 §8-3).</summary>
        public void TriggerGameOver(string reason)
        {
            if (IsGameOver) return;
            IsGameOver = true;
            SaveSystem.Delete();
            Debug.Log($"[GameManager] 게임오버 — {reason}");
            GameOverOccurred?.Invoke(reason);
            StartCoroutine(GameOverSequence());
        }

        IEnumerator GameOverSequence()
        {
            string currentSceneName = gameObject.scene.name;

            // 0. BGM 페이드아웃
            SoundManager.Instance?.StopBGM(fade: true);

            // 1. 페이드인 → 검은 화면
            bool fadeInDone = false;
            ScreenFader.Instance.FadeIn(1f, onComplete: () => fadeInDone = true);
            yield return new WaitUntil(() => fadeInDone);
            
            // 2. StartScene 어디티브 로드 (백그라운드)
            var loadOp = SceneManager.LoadSceneAsync(startSceneName, LoadSceneMode.Additive);
            loadOp.allowSceneActivation = false;
            yield return new WaitUntil(() => loadOp.progress >= 0.9f);


            // 3. 타이핑 연출
            if (gameOverText != null)
            {
                gameOverText.gameObject.SetActive(true);
                gameOverText.text = "";
                foreach (char c in GameOverMessage)
                {
                    gameOverText.text += c;
                    yield return new WaitForSecondsRealtime(typeInterval);
                }
            }

            yield return new WaitForSecondsRealtime(holdAfterType);

            // 4. BGM 재시작 + StartScene 활성화
            if (gameOverText != null) gameOverText.gameObject.SetActive(false);
            SoundManager.Instance?.PlayBGM("Han river loop_1", fade: true);
            loadOp.allowSceneActivation = true;
            yield return new WaitUntil(() => loadOp.isDone);

            // 5. 페이드아웃
            bool fadeOutDone = false;
            ScreenFader.Instance.FadeOut(1f, onComplete: () => fadeOutDone = true);
            yield return new WaitUntil(() => fadeOutDone);

            // 6. 현재 씬 언로드
            SceneManager.UnloadSceneAsync(currentSceneName);
        }

        /// <summary>세션 재시작용 상태 초기화. 위치·추격자 리셋은 각 시스템이 담당.</summary>
        public void ResetSession()
        {
            IsGameOver = false;
        }
    }
}
