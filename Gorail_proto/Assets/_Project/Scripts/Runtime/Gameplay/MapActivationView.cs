using UnityEngine;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// [D1] 활성 노선 색 / 비활성 회색을 노선도에 반영하는 드라이버.
    /// Player의 활성 노선이 바뀔 때(초기화·환승) 렌더러에 재색칠을 요청한다.
    /// (D4 범례도 추후 이 신호에 함께 묶을 수 있다.)
    /// </summary>
    public class MapActivationView : MonoBehaviour
    {
        [SerializeField] private Player             player;
        [SerializeField] private SubwayMapRenderer  mapRenderer;

        void OnEnable()
        {
            if (player != null) player.ActiveLinesChanged += Refresh;
        }

        void OnDisable()
        {
            if (player != null) player.ActiveLinesChanged -= Refresh;
        }

        void Start() => Refresh(); // 초기 1회(시작 노선만 활성, 나머지 회색)

        public void Refresh()
        {
            if (mapRenderer != null && player != null)
                mapRenderer.ApplyActiveLineColors(player.ActiveLines);
        }
    }
}
