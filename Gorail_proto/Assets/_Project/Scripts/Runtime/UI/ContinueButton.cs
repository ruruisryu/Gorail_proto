using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 시작 씬의 "이어하기" 버튼. 세이브 파일이 없으면 비활성화된다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ContinueButton : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "SubwayScene";
        [SerializeField] private string gameBgm       = "Han river loop_1";

        private Button _button;

        void Awake()
        {
            _button = GetComponent<Button>();
            _button.interactable = SaveSystem.HasSave;
            _button.onClick.AddListener(OnContinueClicked);
        }

        private void OnContinueClicked()
        {
            if (!SaveSystem.HasSave) return;

            if (!string.IsNullOrEmpty(gameBgm))
                SoundManager.Instance?.PlayBGM(gameBgm);

            // 세이브 파일이 있음을 표시 — SubwayScene의 GameCore.Start 이후에 LoadGame 호출
            PlayerPrefs.SetInt("LoadSave", 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
