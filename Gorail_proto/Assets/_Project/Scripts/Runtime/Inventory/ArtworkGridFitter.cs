using UnityEngine;
using Game.Inventory;

namespace Game.UI
{
    /// <summary>
    /// 작품활동 패널의 두 격자(가방·실루엣)에 '같은 칸 크기'를 적용한다.
    /// 각 뷰가 자기 부모(GridArea)에 스프라이트(격자+패딩)까지 들어가는 최대 칸 크기를 알려주면,
    /// 둘 중 더 작은 값을 공통 칸 크기로 정해 양쪽에 똑같이 박는다.
    /// → 칸 크기 통일 + 두 격자 모두 부모를 넘지 않음.
    ///
    /// 두 뷰는 fitToParent를 끄고 Panel Anchor X=0.5(부모 중앙)로 둔다. 위치는 GridArea가 잡는다.
    /// ArtworkScreen.Open() 직후(레이아웃 확정 후) Apply()를 부른다.
    /// </summary>
    public class ArtworkGridFitter : MonoBehaviour
    {
        [SerializeField] private InventoryView bagView;
        [SerializeField] private InventoryView silhouetteView;

        [Tooltip("켜면 활성화될 때마다 자동으로 한 번 맞춘다.")]
        [SerializeField] private bool applyOnEnable = true;

        void OnEnable()
        {
            if (applyOnEnable) Apply();
        }

        /// <summary>두 뷰의 최대 칸 크기를 비교해 공통(작은) 값을 양쪽에 적용.</summary>
        public void Apply()
        {
            if (bagView == null || silhouetteView == null) return;

            float a = bagView.MaxCellForParent();
            float b = silhouetteView.MaxCellForParent();
            float common = Mathf.Min(a, b);

            bagView.SetCellSize(common);
            silhouetteView.SetCellSize(common);
            Debug.Log($"[ArtworkGridFitter] 공통 칸 크기 {common:F1} 적용 (가방 max={a:F1}, 실루엣 max={b:F1})");
        }
    }
}