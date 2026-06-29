using UnityEngine;
using UnityEngine.EventSystems;
using Game.Subway;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 이 컴포넌트가 달린 UI에 마우스를 올리면 지정한 역 타입(환승역 / 랜드마크역)을
    /// 노선도에서 노란색 원으로 강조한다.
    /// </summary>
    public class StationTypeHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SubwayMapRenderer.StationHighlightType stationType;

        SubwayMapRenderer Renderer => GameCore.Instance?.MapRenderer;

        public void OnPointerEnter(PointerEventData _) => Renderer?.ShowStationTypeHighlight(stationType);
        public void OnPointerExit(PointerEventData _)  => Renderer?.ClearStationTypeHighlight();

        void OnDisable() => Renderer?.ClearStationTypeHighlight();
    }
}
