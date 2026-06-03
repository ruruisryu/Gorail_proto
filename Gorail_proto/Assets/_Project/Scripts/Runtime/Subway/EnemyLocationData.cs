using System.Collections.Generic;
using UnityEngine;

namespace Game.Subway
{
    [CreateAssetMenu(fileName = "EnemyLocations", menuName = "Subway/Enemy Locations")]
    public class EnemyLocationData : ScriptableObject
    {
        [Tooltip("적이 위치한 역의 stationId 목록. SubwayNetworkData에 등록된 stationId와 일치해야 함.")]
        public List<string> enemyStationIds = new List<string>();
    }
}
