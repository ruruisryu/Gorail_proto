using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// IP별 작품활동 캔버스 정의(외부IP §3-2 "IP별 빈칸").
    /// 디자이너가 IP(예: DDP)마다 하나씩 .asset으로 만든다.
    /// (Create ▸ Inventory ▸ IP Canvas Data)
    ///
    /// - silhouette : 재료를 배치할 빈칸 모양. 아이템과 동일하게 "칸을 칠해" 정의.
    /// - background : 재료 배치 뷰 뒤에 깔리는 IP 이미지.
    ///
    /// 새 IP가 늘면 이 에셋만 추가하면 된다(코드 수정 없음).
    /// "어느 역에서 이 IP를 쓸지"는 IP가 여러 개 될 때 StationData에서 연결(지금은 패널이 직접 참조).
    /// </summary>
    [CreateAssetMenu(fileName = "IpCanvas_", menuName = "Inventory/IP Canvas Data")]
    public class IpCanvasData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("내부 식별자. 예: ddp")]
        public string ipId;
        [Tooltip("표시 이름. 예: 동대문디자인플라자")]
        public string displayName;

        [Header("재료 배치 빈칸 — 칸을 칠해 IP 실루엣 모양 정의")]
        public GridShape silhouette = new GridShape(7, 6, null);

        [Header("표시")]
        [Tooltip("재료 배치 뷰 배경에 깔리는 IP 이미지.")]
        public Sprite background;

        // ────────────────────────────────────────────────────────────
        // [추후 확장] IP별 완성도/명성 보정, 드론뷰 지원 여부 등 — 필요해지면 여기에.
        // 완성도 계산식이나 패널은 안 건드리고 이 데이터만 늘리면 됨.
        // public float fameBonus;
        // public bool  supportsDroneView;
        // ────────────────────────────────────────────────────────────

        /// <summary>빈칸 전체 칸 수. 채움률·진입 차단(보유 칸수 &lt; 전체×30%) 계산에 사용.</summary>
        public int TotalCells => silhouette != null ? silhouette.CellCount() : 0;
    }
}