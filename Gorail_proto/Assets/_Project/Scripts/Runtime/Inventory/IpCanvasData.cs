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
        [Tooltip("재료 배치 뷰 배경에 깔리는 IP 이미지(픽셀 그림 등).")]
        public Sprite background;

        [Header("기본 뷰 (OutsideScene §1-1·§2)")]
        [Tooltip("기본 뷰에서 카메라가 좌우로 훑는 가로로 긴 배경(§1-1). 결함도 이 위에 뜬다.")]
        public Sprite outsideBackground;

        [Tooltip("이 IP의 결함 평소 스프라이트(§2).")]
        public Sprite defectSprite;
        [Tooltip("결함 호버 시 하이라이트 스프라이트(§2).")]
        public Sprite defectHighlightSprite;
        [Tooltip("결함을 둘 위치 — 가로 배경 기준 정규화 좌표(0~1). (0.5,0.5)=중앙.")]
        public Vector2 defectPosition = new Vector2(0.5f, 0.5f);
        [Tooltip("결함 크기 — 가로 배경 기준 정규화(0~1). 예: (0.1, 0.15). 에디터 헬퍼로 잡는 걸 권장.")]
        public Vector2 defectSize = new Vector2(0.1f, 0.12f);

        // ────────────────────────────────────────────────────────────
        // [추후 확장] IP별 완성도/명성 보정, 드론뷰 지원 여부 등 — 필요해지면 여기에.
        // 완성도 계산식이나 패널은 안 건드리고 이 데이터만 늘리면 됨.
        // public float fameBonus;
        // public bool  supportsDroneView;
        // ────────────────────────────────────────────────────────────

        /// <summary>빈칸 전체 칸 수. 채움률·진입 차단(보유 칸수 &lt; 전체×30%) 계산에 사용.</summary>
        public int TotalCells => silhouette != null ? silhouette.CellCount() : 0;

        [Header("외부 씬 3D 시야 한계(도, 정면=0 기준)")]
        [Tooltip("좌측 한계(음수). 예: -25")]
        public float viewYawLeft = -30f;
        [Tooltip("우측 한계(양수). 예: 40")]
        public float viewYawRight = 30f;

        [Tooltip("이 IP의 3D 모델 프리팹. 비우면 2D 배경 폴백.")]
        public GameObject model3D;
    }
}