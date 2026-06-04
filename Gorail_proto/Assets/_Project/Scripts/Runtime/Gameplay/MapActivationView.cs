using UnityEngine;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// [D1] 활성 노선 색 / 비활성 회색을 노선도에 반영하는 드라이버.
    /// 재색칠 시점: 활성 노선 변경(초기화·환승) + 지하철 공간 진입(맵이 (재)표시·(재)빌드된 직후).
    /// 렌더러가 활성 집합을 저장하므로(LineColorFor) 이후 어떤 재그리기에도 활성색이 유지된다.
    /// </summary>
    public class MapActivationView : MonoBehaviour
    {
        [SerializeField] private Player                 player;
        [SerializeField] private SubwayMapRenderer      mapRenderer;
        [SerializeField] private Game.Core.SpaceManager spaceManager;

        void OnEnable()
        {
            if (player != null) player.ActiveLinesChanged += Refresh;
            if (spaceManager != null) spaceManager.SpaceChanged += OnSpaceChanged;
        }

        void OnDisable()
        {
            if (player != null) player.ActiveLinesChanged -= Refresh;
            if (spaceManager != null) spaceManager.SpaceChanged -= OnSpaceChanged;
        }

        void Start() => Refresh(); // 초기 1회

        void OnSpaceChanged(Game.Core.Space s)
        {
            if (s == Game.Core.Space.Subway) Refresh(); // 지하철 진입 시 맵 빌드 후 재색칠 보장
        }

        public void Refresh()
        {
            if (mapRenderer != null && player != null)
                mapRenderer.ApplyActiveLineColors(player.ActiveLines);
        }
    }
}
