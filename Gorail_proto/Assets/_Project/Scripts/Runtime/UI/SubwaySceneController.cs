using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class SubwaySceneController : MonoBehaviour
    {
        [SerializeField] private SubwayMapPopup subwayMapPopup;
        [Tooltip("노선도를 여는 버튼. 코드로 onClick을 연결한다(인스펙터 UnityEvent 배선 불필요).")]
        [SerializeField] private Button subwayMapButton;

        void Awake()
        {
            if (subwayMapButton != null)
                subwayMapButton.onClick.AddListener(OnSubwayMapButtonClicked);
        }

        void OnDestroy()
        {
            if (subwayMapButton != null)
                subwayMapButton.onClick.RemoveListener(OnSubwayMapButtonClicked);
        }

        public void OnSubwayMapButtonClicked()
        {
            if (subwayMapPopup != null) subwayMapPopup.Show();
        }
    }
}
