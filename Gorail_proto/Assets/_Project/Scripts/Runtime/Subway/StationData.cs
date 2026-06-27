using UnityEngine;
using Game.Inventory;

namespace Game.Subway
{
    /// <summary>역 기능(§10). 환승은 노선 속성(MapGraph.IsTransfer)이라 여기 두지 않고,
    /// 지도 위에 얹히는 기능 레이어만 둔다. 한 역은 한 번에 하나의 기능만 가진다.</summary>
    public enum StationFeature
    {
        General,   // 일반역 — 외부 진입 불가
        Landmark,  // 랜드마크역 — 외부에서 작품활동
        Shop,      // 상점역 — 외부에서 구매(이 슬라이스 내부는 stub)
        Safe       // 안전역 — 랜드마크·상점 미탑재, 체류 중 검문 미발동(작품활동 불가)
    }

    [CreateAssetMenu(fileName = "Station_", menuName = "Subway/Station Data")]
    public class StationData : ScriptableObject
    {
        public string stationId;
        public string displayName;

        [Tooltip("역 기능 레이어(§10). 환승 여부는 노선 구조로 별도 결정됨.")]
        public StationFeature featureType = StationFeature.General;

        [Tooltip("랜드마크역일 때 작품활동에 쓸 IP 실루엣(SO). 예: 동대문→IpCanvas_DDP. " +
                 "일반/상점/안전역은 비워둔다. IP가 여러 개여도 ArtworkInventory는 하나로, 입장 시 이걸로 갈아끼운다.")]
        public IpCanvasData ipCanvas;

        [Tooltip("OutsideScene(지상) 기본뷰에 깔리는 실제 사진 배경(§1-1). " +
                 "비워두면 회색으로 대체. ※ 재료배치 그리드 배경(픽셀 그림)은 IpCanvasData.background로 따로 둠.")]
        public Sprite outsidePhoto;

        [Tooltip("기본뷰에서 카메라가 좌우로 훑는 '가로로 긴' 배경 스프라이트(§1-1·§2). " +
                 "월드 공간 SpriteRenderer에 실리며, 카메라가 이 폭 안에서 우클릭 드래그로 팬한다. " +
                 "결함(2D)도 이 배경 위에 배치된다.")]
        public Sprite outsideBackground;

        /// <summary>외부(지상) 진입 가능 역인가 — 특별역(랜드마크/상점)만(§10·§9).</summary>
        public bool AllowsOutside => featureType == StationFeature.Landmark || featureType == StationFeature.Shop;

        /// <summary>외부에서 작품활동 가능한가 — 랜드마크만(상점·안전·일반 불가).</summary>
        public bool AllowsArtwork => featureType == StationFeature.Landmark;  // 현재 지상으로 나갈 수만 있으면 다 작품활동을 할 수 있게 되어있음
    }
}