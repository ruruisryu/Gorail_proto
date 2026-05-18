using UnityEngine;

namespace Game.UI
{
    public class SubwaySceneController : MonoBehaviour
    {
        [SerializeField] private SubwayMapPopup subwayMapPopup;

        public void OnSubwayMapButtonClicked()
        {
            subwayMapPopup.Show();
        }
    }
}
