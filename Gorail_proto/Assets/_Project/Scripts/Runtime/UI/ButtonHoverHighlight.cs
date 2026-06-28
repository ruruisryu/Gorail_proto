using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 버튼 호버 시 하이라이트 이미지를 띄우는 컴포넌트 — ButtonHoverOutline(코드로 그린 테두리)의 "이미지" 버전.
    /// highlightImage(보통 버튼의 자식 오버레이 Image)를 호버 진입 시 켜고 이탈 시 끈다.
    /// 상태가 바뀌는 버튼(복귀 버튼: 기본/빨강/파랑)은 SetHighlightSprite로 하이라이트도 함께 교체한다.
    ///
    /// 사용: 버튼 오브젝트에 이 컴포넌트를 붙이고, 하이라이트 스프라이트를 넣은 자식 Image를 highlightImage에 연결.
    /// (자식 Image는 처음엔 꺼져 있어도 됨 — 컴포넌트가 알아서 켜고 끔.)
    /// </summary>
    public class ButtonHoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("호버 시 켜질 하이라이트 오버레이 이미지(보통 버튼의 자식).")]
        [SerializeField] private Image highlightImage;

        [Tooltip("비활성(interactable=false) 버튼에서는 하이라이트를 표시하지 않음.")]
        [SerializeField] private bool requireInteractable = true;

        private Selectable _selectable;
        private bool _hovering;

        void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (highlightImage != null)
            {
                highlightImage.raycastTarget = false;   // 클릭/호버를 가로채지 않도록
                highlightImage.gameObject.SetActive(false);
            }
        }

        void OnDisable()
        {
            _hovering = false;
            if (highlightImage != null) highlightImage.gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hovering = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hovering = false;
            Refresh();
        }

        void Refresh()
        {
            if (highlightImage == null) return;
            bool interactableOk = !requireInteractable || _selectable == null || _selectable.interactable;
            highlightImage.gameObject.SetActive(_hovering && interactableOk);
        }

        /// <summary>상태별 버튼용 — 하이라이트 스프라이트 교체(예: 기본/빨강/파랑). 현재 호버 중이면 즉시 반영.</summary>
        public void SetHighlightSprite(Sprite sprite)
        {
            if (highlightImage != null) highlightImage.sprite = sprite;
        }

        /// <summary>외부에서 상태가 바뀐 뒤 하이라이트 표시를 다시 계산하고 싶을 때(예: interactable 변경).</summary>
        public void RefreshState() => Refresh();
    }
}