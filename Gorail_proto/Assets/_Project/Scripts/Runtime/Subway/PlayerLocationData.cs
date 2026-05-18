using UnityEngine;

namespace Game.Subway
{
    [CreateAssetMenu(fileName = "PlayerLocation", menuName = "Subway/Player Location")]
    public class PlayerLocationData : ScriptableObject
    {
        [Tooltip("StationData의 stationId와 일치해야 함")]
        public string currentStationId;
    }
}