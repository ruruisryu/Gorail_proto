using UnityEngine;
using UnityEngine.UI;

namespace Game.Subway
{
    /// <summary>
    /// 두 역을 잇는 노선 구간 컴포넌트. 같은 GameObject에 붙은 Image를 색 제어에 사용한다.
    /// stationA·stationB·lineId로 SubwayMapRenderer의 세그먼트 맵에 등록된다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIBezierLine : MonoBehaviour
    {
        [Header("연결 정보")]
        public string stationA;
        public string stationB;
        public string lineId;

        private Image _image;
        Image Img => _image != null ? _image : (_image = GetComponent<Image>());

        public Color color
        {
            get => Img.color;
            set => Img.color = value;
        }
    }
}
