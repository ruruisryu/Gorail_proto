using UnityEngine;
using Game.Subway;

namespace Game.Gameplay
{
    /// <summary>
    /// [D1] 활성 노선 색 / 비활성 회색을 노선도에 반영하는 드라이버.
    /// 재색칠 시점:
    ///   - 활성 노선 변경(초기화·환승) → ActiveLinesChanged
    ///   - 역 이동 → StateChanged (환승역 하이라이트 갱신)
    ///   - 지하철 공간 진입(맵이 (재)표시·(재)빌드된 직후)
    /// </summary>
    public class MapActivationView : MonoBehaviour
    {
        [SerializeField] private Player                 player;
        [SerializeField] private SubwayMapRenderer      mapRenderer;
        [SerializeField] private Game.Core.SpaceManager spaceManager;

        void OnEnable()
        {
            if (player != null)
            {
                player.ActiveLinesChanged += Refresh;         // 환승 등 활성 노선 변경
                player.StateChanged       += RefreshHighlight; // 역 이동 시 환승역 하이라이트 갱신
            }
            if (spaceManager != null) spaceManager.SpaceChanged += OnSpaceChanged;
        }

        void OnDisable()
        {
            if (player != null)
            {
                player.ActiveLinesChanged -= Refresh;
                player.StateChanged       -= RefreshHighlight;
            }
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
                mapRenderer.ApplyActiveLineColors(player.ActiveLines, player.CurrentLineId);
        }

        /// <summary>역 이동마다 환승역 하이라이트만 빠르게 갱신(전체 재색칠 없이).</summary>
        void RefreshHighlight()
        {
            if (mapRenderer != null && player != null)
                mapRenderer.RefreshLineHighlight(player.CurrentLineId);
        }
    }
}
