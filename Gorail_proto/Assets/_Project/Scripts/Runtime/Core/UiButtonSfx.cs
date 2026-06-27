using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 버튼에 붙이면 호버/클릭 효과음을 낸다.
    /// 같은 오브젝트(또는 지정한)의 Selectable(Button 등) interactable로
    /// 활성/비활성 클릭을 구분한다.
    ///
    /// 비활성 버튼도 IPointerClickHandler는 받으므로 "비활성 클릭" 소리가 난다.
    /// (단, CanvasGroup.blocksRaycasts=false 등으로 레이캐스트가 막히면 안 들어온다.)
    /// </summary>
    [DisallowMultipleComponent]
    public class UiButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Tooltip("활성/비활성 판정에 쓸 Selectable. 비우면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private Selectable target;
        [Tooltip("호버 효과음 사용.")]
        [SerializeField] private bool playHover = true;
        [Tooltip("비활성 상태에서도 호버음을 낼지.")]
        [SerializeField] private bool hoverWhenDisabled = false;

        void Awake() { if (target == null) target = GetComponent<Selectable>(); }

        bool Interactable => target == null || target.interactable;

        public void OnPointerEnter(PointerEventData e)
        {
            if (!playHover) return;
            if (!Interactable && !hoverWhenDisabled) return;
            Sfx.UiHover();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (Interactable) Sfx.UiClick();
            else              Sfx.UiClickDisabled();
        }
    }
}