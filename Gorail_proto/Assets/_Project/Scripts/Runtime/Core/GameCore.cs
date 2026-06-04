using UnityEngine;
using Game.Subway;
using Game.Gameplay;

namespace Game.Core
{
    /// <summary>
    /// [S3] 런타임 서비스 로케이터. additive로 로드되는 승강장·지상 씬의 컨트롤러가
    /// 영속 베이스(SubwayScene)의 매니저들에 접근하는 단일 통로.
    ///
    /// 멀티 씬 구조: SubwayScene이 항상 로드된 베이스(매니저·상태 보유)이고,
    /// PlatformScene/OutsideScene은 그 위에 additive로 얹혔다 내려간다 → 상태가 파괴되지 않아
    /// 씬 간 재바인딩이 불필요하다.
    /// </summary>
    public class GameCore : MonoBehaviour
    {
        public static GameCore Instance { get; private set; }

        [SerializeField] private GameManager      gameManager;
        [SerializeField] private Player           player;
        [SerializeField] private FameSystem       fameSystem;
        [SerializeField] private TrackerManager   trackerManager;
        [SerializeField] private InspectionSystem inspection;
        [SerializeField] private MapGraphProvider   graphProvider;
        [SerializeField] private SpaceManager       spaceManager;
        [SerializeField] private PlatformController platformController;
        [SerializeField] private TurnResolver       turnResolver;
        [SerializeField] private Game.Data.SceneConfig sceneConfig;

        public GameManager        Game       => gameManager;
        public Player             Player     => player;
        public FameSystem         Fame       => fameSystem;
        public TrackerManager     Trackers   => trackerManager;
        public InspectionSystem   Inspection => inspection;
        public MapGraphProvider   Graph      => graphProvider;
        public SpaceManager       Space      => spaceManager;
        public PlatformController Platform   => platformController;
        public TurnResolver       TurnResolver => turnResolver;
        public Game.Data.SceneConfig SceneConfig => sceneConfig;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
    }
}
