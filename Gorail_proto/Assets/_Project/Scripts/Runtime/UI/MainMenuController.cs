using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    public class MainMenuController : MonoBehaviour
    {
        public void OnStartButtonClicked()
        {
            SceneManager.LoadScene("SubwayScene");
        }
    }
}
