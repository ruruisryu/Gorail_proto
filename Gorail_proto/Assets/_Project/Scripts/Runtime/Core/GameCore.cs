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
        [SerializeField] private ArtworkSystem      artworkSystem;
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
        [SerializeField] private RngService         rngService;
        [SerializeField] private SubwayMapRenderer  mapRenderer;
        [SerializeField] private Game.Inventory.InventorySystem bagInventory;

        public GameManager        Game         => gameManager;
        public Player             Player       => player;
        public ArtworkSystem      Artwork      => artworkSystem;
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
        public RngService         Rng          => rngService;
        public SubwayMapRenderer  MapRenderer  => mapRenderer;

        /// <summary>영구 가방(보유 재료) 데이터. 씬을 넘나들며 유지되며, OutsideScene의
        /// 작품활동 팝업이 드래그 원본으로 런타임 참조한다(§외부IP 작품활동 UI).</summary>
        public Game.Inventory.InventorySystem BagInventory => bagInventory;

        [Header("동작 설정")]
        [SerializeField] private bool autoDisembark = true;
        /// <summary>목적지 도착 시 자동으로 승강장으로 진입할지 여부.</summary>
        public bool AutoDisembark { get => autoDisembark; set => autoDisembark = value; }

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
            if (gameTimeSystem != null) gameTimeSystem.DayEnded    += OnDayEnded;
            if (turnResolver   != null) turnResolver.MoveCompleted += OnMoveCompleted;

            if (PlayerPrefs.GetInt("LoadSave", 0) == 1)
            {
                PlayerPrefs.DeleteKey("LoadSave");
                LoadGame();
            }
        }

        void OnDestroy()
        {
            if (gameTimeSystem != null) gameTimeSystem.DayEnded    -= OnDayEnded;
            if (turnResolver   != null) turnResolver.MoveCompleted -= OnMoveCompleted;
        }

        void OnMoveCompleted(string _, bool __) => SaveGame();

        // ── 세이브 / 로드 ────────────────────────────────────────────────

        public void SaveGame()
        {
            if (player == null) return;
            if (gameManager?.IsGameOver == true) return;
            var d = new SaveData
            {
                stationId       = player.CurrentStationId,
                lineId          = player.CurrentLineId,
                direction       = player.Direction,
                directionLocked = player.DirectionLocked,
                activeLines     = new System.Collections.Generic.List<string>(player.ActiveLines),
                day             = gameTimeSystem?.Day    ?? 1,
                hour            = gameTimeSystem?.Hour   ?? 7,
                minute          = gameTimeSystem?.Minute ?? 0,
                money           = moneySystem?.CurrentMoney ?? 0,
                fame            = fameSystem?.CurrentFame   ?? 0f,
                fameLastArtworkMinutes = fameSystem?.LastArtworkMinutes ?? 0,
                famePrevMinutes        = fameSystem?.PrevTotalMinutes   ?? 0,
                trackerDebt = trackerManager?.ChaseDebt ?? 0f,
                artworkDoneStations = platformController != null
                    ? new System.Collections.Generic.List<string>(platformController.GetArtworkDoneStations())
                    : new System.Collections.Generic.List<string>(),
            };
            if (trackerManager != null)
                foreach (var t in trackerManager.Trackers)
                    d.trackers.Add(new SaveData.TrackerEntry { stationId = t.StationId, lineId = t.LineId });
            SaveSystem.Save(d);
        }

        public void LoadGame()
        {
            var d = SaveSystem.Load();
            if (d == null) return;
            gameTimeSystem?.Restore(d.day, d.hour, d.minute);
            moneySystem?.Restore(d.money);
            fameSystem?.Restore(d.fame, d.fameLastArtworkMinutes, d.famePrevMinutes);
            player?.Restore(d);
            trackerManager?.Restore(d.trackers, d.trackerDebt);
            platformController?.Restore(d.artworkDoneStations);
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
                    SaveGame();
                },
                onComplete: () =>
                {
                    DayTransitionComplete?.Invoke(gameTimeSystem?.Day ?? 0);
                }
            );
        }
    }
}