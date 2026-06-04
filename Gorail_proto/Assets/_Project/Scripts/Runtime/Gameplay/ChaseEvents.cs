using UnityEngine;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// [H1] 추격 이벤트 허브. 여기저기 흩어진 시스템 이벤트(명성·수배도·검문·게임오버·이동·공간)를
    /// 한 곳에서 구독해 단일 지점으로 재발행한다. HUD(StatusHud)·연출(H6)이 시스템 각각이 아니라
    /// 이 허브 하나만 구독하면 되도록 해 결합도를 낮춘다.
    ///
    /// GameCore.Instance(영속 로케이터)가 준비된 뒤 Start에서 원본 이벤트에 연결한다.
    /// </summary>
    public class ChaseEvents : MonoBehaviour
    {
        public static ChaseEvents Instance { get; private set; }

        /// <summary>명성 변동 (현재 명성).</summary>
        public event System.Action<float> FameChanged;
        /// <summary>수배도 변동 (레벨 0~5).</summary>
        public event System.Action<int> WantedChanged;
        /// <summary>검문 판정 (역 ID, 통과 여부).</summary>
        public event System.Action<string, bool> InspectionResolved;
        /// <summary>게임오버 (사유).</summary>
        public event System.Action<string> GameOver;
        /// <summary>한 역 해소 (도달 역, 스텝 i, 총 k).</summary>
        public event System.Action<string, int, int> StepResolved;
        /// <summary>이동 종료 (마지막 역, 게임오버 여부).</summary>
        public event System.Action<string, bool> MoveCompleted;
        /// <summary>공간 전환 (지하철/승강장/지상).</summary>
        public event System.Action<Game.Core.Space> SpaceChanged;

        private GameCore _core;
        private bool _wired;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start() => TryWire();

        // GameCore가 같은 프레임 Awake 순서로 아직 안 잡혔을 수 있어 Start에서 한 번,
        // 실패하면 Update에서 준비될 때까지 재시도한다.
        void Update() { if (!_wired) TryWire(); }

        void TryWire()
        {
            var core = GameCore.Instance;
            if (core == null) return;
            _core = core;

            if (core.Fame != null)       core.Fame.FameChanged          += OnFame;
            if (core.Game != null)     { core.Game.WantedChanged        += OnWanted;
                                         core.Game.GameOverOccurred      += OnGameOver; }
            if (core.Inspection != null) core.Inspection.InspectionResolved += OnInspection;
            if (core.TurnResolver != null) { core.TurnResolver.StepResolved += OnStep;
                                             core.TurnResolver.MoveCompleted += OnMove; }
            if (core.Space != null)      core.Space.SpaceChanged         += OnSpace;

            _wired = true;
        }

        void OnDestroy()
        {
            if (_core == null) return;
            if (_core.Fame != null)       _core.Fame.FameChanged          -= OnFame;
            if (_core.Game != null)     { _core.Game.WantedChanged        -= OnWanted;
                                          _core.Game.GameOverOccurred      -= OnGameOver; }
            if (_core.Inspection != null) _core.Inspection.InspectionResolved -= OnInspection;
            if (_core.TurnResolver != null) { _core.TurnResolver.StepResolved -= OnStep;
                                              _core.TurnResolver.MoveCompleted -= OnMove; }
            if (_core.Space != null)      _core.Space.SpaceChanged         -= OnSpace;
            if (Instance == this) Instance = null;
        }

        // ── 원본 → 재발행 ────────────────────────────────────────────────
        void OnFame(float f)            => FameChanged?.Invoke(f);
        void OnWanted(int w)            => WantedChanged?.Invoke(w);
        void OnInspection(string s, bool p) => InspectionResolved?.Invoke(s, p);
        void OnGameOver(string r)       => GameOver?.Invoke(r);
        void OnStep(string s, int i, int k) => StepResolved?.Invoke(s, i, k);
        void OnMove(string s, bool go)  => MoveCompleted?.Invoke(s, go);
        void OnSpace(Game.Core.Space sp) => SpaceChanged?.Invoke(sp);
    }
}
