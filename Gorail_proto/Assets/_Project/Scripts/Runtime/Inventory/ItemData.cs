using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// 아이템 정의(아이템기획서 §2). 디자이너가 인스펙터에서 .asset으로 만든다.
    /// (Create ▸ Inventory ▸ Item Data) — 물감·붓·팔레트·롤러 등.
    ///
    /// shape는 좌표가 아니라 "칸을 클릭해 칠하는" 격자로 편집한다(GridShapeDrawer).
    /// 점유 칸 수는 shape에서 자동 계산되므로 따로 입력하지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "Inventory/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("내부 식별자. 중복 없이.")]
        public string itemId;
        public string displayName;

        [Header("형태")]
        [Tooltip("점유 격자 형태 — 아래 격자에서 칸을 클릭해 칠한다. (예: 물감=일자 2칸, 롤러=T형 4칸)")]
        public GridShape shape = new GridShape();

        [Header("표시")]
        public Sprite sprite;
        [Range(0f, 1f)]
        [Tooltip("스프라이트 반투명도. 기획서 기준 90%(=0.9). 배경 칸이 비치도록.")]
        public float spriteOpacity = 0.9f;

        // ────────────────────────────────────────────────────────────
        // [추후 확장] 아이템 개별 효과 — 6월 이후 별도 개발(아이템기획서 §1).
        // 현재는 "점유 칸 수"만 완성도에 반영. 아래 필드를 열어 가중치를 더하면
        // 완성도 계산식만 바꿔 끼우면 된다(InventoryGrid·UI는 그대로).
        //
        // [Header("효과 (추후)")]
        // [Tooltip("완성도 가중치. 1=기본. 칸 수 외 추가 영향.")]
        // public float completionWeight = 1f;
        // ────────────────────────────────────────────────────────────

        /// <summary>점유 칸 수(shape에서 계산). 진입 차단·채움률 계산에 사용.</summary>
        public int CellCount => shape != null ? shape.CellCount() : 0;
    }
}