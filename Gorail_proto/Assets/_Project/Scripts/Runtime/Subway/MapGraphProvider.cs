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

        private MapGraph _graph;

        /// <summary>
        /// 그래프 접근점. 아직 안 만들어졌으면 첫 접근 때 빌드한다(self-healing).
        /// Awake 타이밍·실행 순서에 상관없이 어떤 소비자가 먼저 접근해도 항상 유효한 그래프를 받는다.
        /// </summary>
        public MapGraph Graph
        {
            get
            {
                if (_graph == null) Rebuild();
                return _graph;
            }
        }

        void Awake()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            if (networkData != null)
                _graph = new MapGraph(networkData);
            else
                Debug.LogWarning("[MapGraphProvider] NetworkData가 할당되지 않았습니다.");
        }
    }
}
