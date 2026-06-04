namespace Game.Gameplay
{
    /// <summary>
    /// ②TurnResolver가 ⑤TrackerManager·⑧InspectionSystem·③PlatformController 없이도
    /// 컴파일·실행되도록 끊어두는 얇은 경계 인터페이스.
    ///
    /// 의도(코드만으로는 안 보이는 결정): 빌드 순서(§10-4)상 ②는 ⑤·⑧·③보다 먼저 완성된다.
    /// TurnResolver는 이 인터페이스에만 의존하고, 미구현 단계는 NullTrackerStep 등 빈 구현으로 대체한다.
    /// 나중에 TrackerManager/InspectionSystem/PlatformController가 이 인터페이스를 구현하면
    /// TurnResolver 코드를 고치지 않고 참조만 바꿔 끼우면 된다.
    /// </summary>

    /// <summary>추격 1스텝 갱신(§4-1 1규칙 + §4-2 체증). ⑤⑥에서 구현.</summary>
    public interface ITrackerStep
    {
        /// <param name="playerSteps">이번 해소 스텝에서 플레이어가 전진한 역 수(보통 1).</param>
        /// <param name="totalMoveStations">이번 1회 행동에 포함된 총 역 수 k(체증 보정용, §4-2).</param>
        void Advance(int playerSteps, int totalMoveStations);
    }

    /// <summary>같은 역 검문 판정(§8). ⑧에서 구현. true면 검문 결과가 게임오버.</summary>
    public interface IInspection
    {
        /// <returns>해당 역에서 검문이 발동했고 그 결과 게임오버이면 true.</returns>
        bool ResolveAt(string stationId);
    }

    /// <summary>도착역 승강장 진입(§7). ③PlatformController에서 구현.</summary>
    public interface IPlatform
    {
        void OpenAt(string stationId);
    }

    // ── 빈 구현(②단계용) ────────────────────────────────────────────────

    /// <summary>추격 미구현 동안 쓰는 무동작 구현.</summary>
    public sealed class NullTrackerStep : ITrackerStep
    {
        public void Advance(int playerSteps, int totalMoveStations) { }
    }

    /// <summary>검문 미구현 동안 쓰는 무동작 구현(절대 잡히지 않음).</summary>
    public sealed class NullInspection : IInspection
    {
        public bool ResolveAt(string stationId) => false;
    }

    /// <summary>승강장 미구현 동안 쓰는 무동작 구현.</summary>
    public sealed class NullPlatform : IPlatform
    {
        public void OpenAt(string stationId) { }
    }
}
