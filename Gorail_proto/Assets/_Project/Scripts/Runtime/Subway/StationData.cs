using UnityEngine;

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
        [Tooltip("맵 패널 내 좌표. 기준 크기 860x550, 좌상단이 (0,0)")]
        public Vector2 mapPosition;

        [Tooltip("역 기능 레이어(§10). 환승 여부는 노선 구조로 별도 결정됨.")]
        public StationFeature featureType = StationFeature.General;

        /// <summary>외부(지상) 진입 가능 역인가 — 특별역(랜드마크/상점)만(§10·§9).</summary>
        public bool AllowsOutside => featureType == StationFeature.Landmark || featureType == StationFeature.Shop;

        /// <summary>외부에서 작품활동 가능한가 — 랜드마크만(상점·안전·일반 불가).</summary>
        public bool AllowsArtwork => featureType == StationFeature.Landmark;
    }
}
