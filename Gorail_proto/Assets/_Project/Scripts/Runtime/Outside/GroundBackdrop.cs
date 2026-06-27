using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// OutsideScene(지상) 기본뷰 배경(§1-1) — 현재 역의 실제 사진(StationData.outsidePhoto)을 깐다.
    /// 사진이 없으면 회색으로 대체. 전체화면을 채우는 한 장짜리 배경 레이어.
    ///
    /// ※ 재료배치 그리드 뒤에 깔리는 픽셀 배경은 이게 아니라 IpCanvasData.background다(InventoryView가 처리).
    ///
    /// 가장 낮은 Sort Order의 Canvas 위에 두면 다른 UI 뒤로 깔린다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GroundBackdrop : MonoBehaviour
    {
        [Tooltip("사진이 없을 때 깔리는 회색.")]
        [SerializeField] private Color fallbackColor = new Color(0.20f, 0.21f, 0.23f, 1f);

        [Tooltip("켜면 사진 비율 유지(레터박스), 끄면 화면을 꽉 채움(늘어남).")]
        [SerializeField] private bool preserveAspect = false;

        private Image _img;

        void OnEnable()  => Apply();
        void Start()     => Apply();   // 씬 로드 직후 역 정보가 준비된 시점 보장

        void Apply()
        {
            if (_img == null)
            {
                _img = GetComponent<Image>();
                if (_img == null) _img = gameObject.AddComponent<Image>();
                var rt = (RectTransform)transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                _img.raycastTarget = false;
            }

            var photo = ResolvePhoto();
            _img.preserveAspect = preserveAspect;
            if (photo != null) { _img.sprite = photo; _img.color = Color.white; }
            else               { _img.sprite = null;  _img.color = fallbackColor; }
        }

        Sprite ResolvePhoto()
        {
            var core = GameCore.Instance;
            var st = core?.Graph?.Graph?.GetStation(core?.Space?.CurrentStationId);
            return st != null ? st.outsidePhoto : null;
        }
    }
}