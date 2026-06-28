using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;

namespace Game.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("게임 시작 시 재생할 BGM 클립 이름. 비우면 재생 안 함.")]
        [SerializeField] private string gameBgm = "Han river loop_1";

        public void OnStartButtonClicked()
        {
            // 타이틀이 아니라 '게임 시작' 시점에 브금 재생(SoundManager는 DontDestroyOnLoad라 씬 넘어가도 유지).
            if (!string.IsNullOrEmpty(gameBgm))
                SoundManager.Instance?.PlayBGM(gameBgm);

            SceneManager.LoadScene("SubwayScene");
        }

        // Exit 버튼 → 종료 확인 팝업(툴바 나가기와 동일한 ModalDialog 재사용)
        public void OnExitButtonClicked()
        {
            ModalDialog.Show("게임을 종료하시겠습니까?",
                ("예", () =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }),
                ("아니오", null));
        }
    }
}