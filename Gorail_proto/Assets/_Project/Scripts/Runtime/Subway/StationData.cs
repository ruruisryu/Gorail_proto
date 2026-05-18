using UnityEngine;

namespace Game.Subway
{
    [CreateAssetMenu(fileName = "Station_", menuName = "Subway/Station Data")]
    public class StationData : ScriptableObject
    {
        public string stationId;
        public string displayName;
        [Tooltip("맵 패널 내 좌표. 기준 크기 860x550, 좌상단이 (0,0)")]
        public Vector2 mapPosition;
    }
}