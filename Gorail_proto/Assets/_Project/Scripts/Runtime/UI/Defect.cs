using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// 기본 뷰의 결함(외부IP §2). Background Image의 자식 uGUI로 두어 배경과 함께 팬된다.
    /// 호버 → 하이라이트 스프라이트로 교체, 클릭 → 작품활동 팝업(ArtworkScreen) 열기.
    ///
    /// 한 OutsideScene이 역마다 배경만 바뀌므로, 각 결함은 자신이 속한 IP의 ipId를 갖고
    /// DefectController가 현재 역 IP와 일치하는 결함만 활성화한다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class Defect : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("이 결함이 속한 IP의 id (IpCanvasData.ipId와 일치). 현재 역 IP와 같을 때만 표시된다.")]
        public string ipId;

        [Tooltip("평소 스프라이트(비우면 현재 Image 스프라이트 사용).")]
        [SerializeField] private Sprite normalSprite;
        [Tooltip("마우스 호버 시 보여줄 하이라이트 스프라이트.")]
        [SerializeField] private Sprite highlightSprite;

        private Image _img;

        void Awake()
        {
            _img = GetComponent<Image>();
            _img.raycastTarget = true;
            if (normalSprite == null) normalSprite = _img.sprite;
            ApplyNormal();
        }

        /// <summary>IP 데이터로 결함을 구성(스프라이트·하이라이트·ipId). DefectController가 호출.</summary>
        public void Configure(Sprite normal, Sprite highlight, string id)
        {
            if (_img == null) _img = GetComponent<Image>();
            normalSprite = normal;
            highlightSprite = highlight;
            ipId = id;
            ApplyNormal();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (highlightSprite != null) _img.sprite = highlightSprite;
        }

        public void OnPointerExit(PointerEventData e) => ApplyNormal();

        public void OnPointerClick(PointerEventData e)
        {
            // 좌클릭만 (우클릭은 배경 팬용)
            if (e.button != PointerEventData.InputButton.Left) return;

            var screen = FindFirstObjectByType<Game.Inventory.ArtworkScreen>();
            if (screen != null) screen.Open();
            else Debug.LogWarning("[Defect] ArtworkScreen을 찾지 못함 — OutsideScene에 ArtworkScreen이 있는지 확인하세요.");
        }

        void ApplyNormal() { if (normalSprite != null) _img.sprite = normalSprite; }
    }
}