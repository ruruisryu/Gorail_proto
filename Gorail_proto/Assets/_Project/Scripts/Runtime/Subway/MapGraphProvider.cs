using UnityEngine;

namespace Game.Subway
{
    /// <summary>
    /// SubwayNetworkData로부터 MapGraph를 빌드하고 씬에 노출한다.
    /// 다른 시스템(TurnResolver, TrackerManager 등)은 이 컴포넌트를 통해 그래프에 접근한다.
    /// </summary>
    public class MapGraphProvider : MonoBehaviour
    {
        [SerializeField] private SubwayNetworkData networkData;

        public MapGraph Graph { get; private set; }

        void Awake()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            if (networkData != null)
                Graph = new MapGraph(networkData);
            else
                Debug.LogWarning("[MapGraphProvider] NetworkData가 할당되지 않았습니다.");
        }
    }
}
