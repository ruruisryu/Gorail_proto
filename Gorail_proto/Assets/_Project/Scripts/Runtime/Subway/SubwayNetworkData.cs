using System.Collections.Generic;
using UnityEngine;

namespace Game.Subway
{
    [CreateAssetMenu(fileName = "SubwayNetwork", menuName = "Subway/Network Data")]
    public class SubwayNetworkData : ScriptableObject
    {
        public List<LineData> lines;
    }
}