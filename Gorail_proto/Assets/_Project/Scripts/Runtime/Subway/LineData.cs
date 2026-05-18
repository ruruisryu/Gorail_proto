using System.Collections.Generic;
using UnityEngine;

namespace Game.Subway
{
    [CreateAssetMenu(fileName = "Line_", menuName = "Subway/Line Data")]
    public class LineData : ScriptableObject
    {
        public string lineId;
        public string displayName;
        public Color lineColor;
        [Tooltip("순서대로 인접 연결. 마지막-첫 역도 연결하면 isCircular 체크")]
        public List<StationData> stations;
        public bool isCircular;
    }
}