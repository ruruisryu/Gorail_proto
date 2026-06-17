using UnityEngine;
using Game.Subway;
using Game.Gameplay;
using Game.UI;

namespace Game.Core
{
    /// <summary>
    /// [S3] 런타임 서비스 로케이터 + 하루 전환 조율자.
    ///
    /// 하루 전환 순서:
    ///   DayEnded(동기) → ForceStop → ScreenFader 시작
    ///   → [검은 화면] → DayTransitionOnBlack → Platform.OpenAt → BeginNextDay
    ///   → [페이드 아웃 완료] → DayTransitionComplete
    /// </summary>
    public class GameCore : MonoBehaviour
    {
        public static GameCore Instance { get; private set; }

        [SerializeField] private GameManager        gameManager;
        [SerializeField] private Player             player;
        [SerializeField] private FameSystem         fameSystem;
        [SerializeField] private WantedSystem       wantedSystem;
        [SerializeField] private TrackerManager     trackerManager;
        [SerializeField] private InspectionSystem   inspection;
        [SerializeField] private MapGraphProvider   graphProvider;
        [SerializeField] private SpaceManager       spaceManager;
        [SerializeField] private PlatformController platformController;
        [SerializeField] private TurnResolver       turnResolver;
        [SerializeField] private GameTimeSystem     gameTimeSystem;
        [SerializeField] private MoneySystem        moneySystem;
        [SerializeField] private Game.Data.SceneConfig sceneConfig;

        public GameManager        Game         => gameManager;
        public Player             Player       => player;
        public FameSystem         Fame         => fameSystem;
        public WantedSystem       Wanted       => wantedSystem;
        public TrackerManager     Trackers     => trackerManager;
        public InspectionSystem   Inspection   => inspection;
        public MapGraphProvider   Graph        => graphProvider;
        public SpaceManager       Space        => spaceManager;
        public PlatformController Platform     => platformController;
        public TurnResolver       TurnResolver => turnResolver;
        public GameTimeSystem     GameTime     => gameTimeSystem;
        public MoneySystem        Money        => moneySystem;
        public Game.Data.SceneConfig SceneConfig => sceneConfig;

        /// <summary>
        /// 화면이 완전히 검어졌을 때 발생. (다음 날 번호, 22:00 도달 역).
        /// 씬 전환·상태 초기화 등 플레이어에게 보이지 않아야 하는 작업에 구독한다.
        /// </summary>
        public event System.Action<int, string> DayTransitionOnBlack;

        /// <summary>
        /// 페이드 아웃이 끝나 화면이 완전히 밝아졌을 때 발생. (새 날 번호).
        /// 연출·UI 갱신 등 플레이어가 볼 수 있어야 하는 작업에 구독한다.
        /// </summary>
        public event System.Action<int> DayTransitionComplete;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            gameTimeSystem?.Initialize();
            moneySystem?.Initialize();
            fameSystem?.Initialize();
            if (gameTimeSystem != null) gameTimeSystem.DayEnded += OnDayEnded;
        }

        void OnDestroy()
        {
            if (gameTimeSystem != null) gameTimeSystem.DayEnded -= OnDayEnded;
        }

        void OnDayEnded(int endedDay)
        {
            // 동기 처리 — Advance() 리턴 전에 플래그가 세팅되어 코루틴이 즉시 중단됨
            turnResolver?.ForceStop();
            string station = player?.CurrentStationId;

            ScreenFader.Instance?.Fade(
                onBlack: () =>
                {
                    int nextDay = endedDay + 1;
                    DayTransitionOnBlack?.Invoke(nextDay, station);
                    platformController?.OpenAt(station);
                    gameTimeSystem?.BeginNextDay();
                },
                onComplete: () =>
                {
                    DayTransitionComplete?.Invoke(gameTimeSystem?.Day ?? 0);
                }
            );
        }
    }
}
