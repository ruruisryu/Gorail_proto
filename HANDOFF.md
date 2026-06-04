# 인수인계 — 추격 슬라이스 ②~⑧ 구현 세션

작성: 2026-06-04 세션. 브랜치: **`feature/chase-slice`** (main 아님). 기획서: `game_design_documents/chase_prototype_spec_v2_patched.docx`(v0.1) — PDF는 한글 추출이 깨지니 docx로 읽어라.

---

## 1. 이번 세션에 한 일 — ②~⑧ + ④⑦ 코드·씬배선 완료
기획서 §10-4 빌드 순서 ①~⑧ 중 **①(이전 세션 완료)에 이어 ②~⑧을 전부 구현**했다.
**모두 컴파일 0에러 · EditMode 테스트 23개 통과 · SubwayScene 배선 완료.**
단 **Play 모드 시각검증은 아직 안 함**(자율 범위가 "씬 배선까지"였음) → **저녁에 함께 할 일**(§4).

### 단계별 커밋 (브랜치 `feature/chase-slice`)
- `docs:` ① 인수인계 문서
- `feat: 추격 기반` — ChaseConfig·Player·GameManager·경계 인터페이스 + MapGraph 확장
- `feat: ②TurnResolver` — 역단위 순차 해소 + 디버그/역클릭 입력
- `feat: ⑤⑥ Tracker·TrackerManager` — 스폰·추격·체증
- `feat: ⑧ InspectionSystem` — 확률 게이트 검문
- `feat: ③ PlatformController` — 승강장 4갈래
- `feat: ④DebugPanel + ⑦가시화`
- `feat: 추격 루프 통합 배선` (이 커밋)

## 2. 아키텍처 (깨지 말 것)
- **경계 인터페이스로 단계 분리**: `IChaseStubs.cs`의 `ITrackerStep`/`IInspection`/`IPlatform`. TurnResolver는 이 인터페이스에만 의존하고, 미구현 땐 `Null*` 빈 구현으로 동작. **`ChaseSession`이 런타임(Awake)에 실물(TrackerManager·InspectionSystem·PlatformController)을 `SetSystems`로 주입**하고, `TurnResolver.MoveCompleted`(도착)→`TrackerManager.OnPlayerDisembark`(스폰 §5-1)를 잇는다.
- **역 위치 단일 소스 = `StationData.mapPosition`(SO)** — 이전 세션 결정 유지. 게임플레이는 위치를 안 만지고 `SubwayMapRenderer.RefreshMarkers()`만 호출.
- **플레이어 마커 재사용**: `Player`가 매 스텝 `PlayerLocationData.currentStationId`를 갱신 → 기존 렌더러 마커 그대로. 추격자는 `TrackerManager`가 `EnemyLocationData.enemyStationIds`를 갱신(⑦: 활성 노선 추격자만).
- **모든 튜닝값 = `ChaseConfig`(SO)** — `Assets/_Project/Data/ScriptableObjects/ChaseConfig.asset`. 대부분 §16 [미확정] 자리표시자.

### 핵심 파일 (`Scripts/Runtime/`)
- `Gameplay/TurnResolver.cs` — ② 목적지→1역씩 코루틴 해소(매 스텝 전진+연출/추격/검문, 도착 시 승강장).
- `Gameplay/Player.cs` — 현재 역·노선·방향·누적 활성노선(§5-3), `ReverseDirection`.
- `Gameplay/Tracker.cs`·`TrackerManager.cs` — ⑤⑥ 1+2규칙 추격, 체증(§4-2), 노선당 상한 스폰(§5-2~5-5), `ITrackerStep`.
- `Gameplay/InspectionSystem.cs` — ⑧ 같은 역 확률 게이트(§8-2), `IInspection`.
- `Gameplay/PlatformController.cs` — ③ 승강장 4갈래(§7-2), `IPlatform`.
- `Gameplay/DebugPanel.cs` — ④ IMGUI 디버그 패널(수배도·통과율·승강장·리셋).
- `Gameplay/DebugMover.cs` — ② 디버그 입력 + 세션 부트스트랩(시작역=PlayerLocation, 시작노선=그 역 첫 노선).
- `Gameplay/StationClickRouter.cs` + `Subway/StationView.cs`(클릭 이벤트) — 역 클릭 이동 입력.
- `Gameplay/ChaseSession.cs` — 통합 배선.
- `Core/GameManager.cs` — 수배도(0~5)·게임오버 세션 상태.
- `Subway/MapGraph.cs` — `GetLineOrderedPath`(순환선 짧은쪽)·`GetConnectingLineId` 등 추가.
- `Tests/EditMode/` — MapGraphTests(14)·TrackerTests(9). `Game.Tests.EditMode.asmdef`.

### 씬 배선 (`SubwayScene`)
루트 **`ChaseSystems`** GameObject에 위 컴포넌트 전부 부착 + 참조 배선 완료. 맵 렌더러는 `SubwaySceneUI/…/MapContent`(기존).

## 3. 검증 방법
- EditMode 테스트: UnityMCP `run_tests`(mode EditMode, assembly `Game.Tests.EditMode`). 현재 23/23 통과.
- 컴파일 루틴: `refresh_unity`(force/scripts/compile=request, wait) → 한 번 더 wait → `read_console`(errors). **컴파일 후 씬 오브젝트 instanceID가 바뀌니, 배선 시 매번 `find_gameobjects`로 ID를 새로 잡을 것.**

## 4. ⚠️ 저녁에 함께 할 일 — Play 시각검증 체크리스트 (미검증 영역)
씬 배선은 했지만 **실제로 화면에 도는지는 미확인**. Play 눌러 아래 순서로 확인:
1. **맵이 보이는가** — `SubwayScene`의 맵은 `MapContent`(SubwaySceneUI 캔버스 아래). 팝업(`SubwayMapPopup`)으로 가려/꺼져 있을 수 있음 → 노선도가 안 보이면 팝업을 열거나 MapContent를 활성화. (이게 1순위 확인)
2. **DBG 패널** — 좌상단 `DBG ▼/▲` 버튼(IMGUI). 안 보이면 DebugPanel 동작 확인.
3. **세션 시작** — 콘솔에 `[DebugMover] 세션 시작 — 역:cityhall 노선:…` 로그 확인. 없으면 PlayerLocation/노선 데이터 점검.
4. **이동** — DBG에서 수배도 0 상태로 역 클릭(또는 DebugMover.destination 입력 후 Move) → 플레이어 마커가 **같은 노선 위 목적지까지 1역씩** 이동하는가(②). 다른 노선 역 클릭 시 "환승 필요" 로그.
5. **추격** — DBG로 수배도 1~3 올리고 → 이동(도착) → 추격자 스폰(플레이어 뒤 6~8역) → 다시 이동마다 추격자가 따라붙는가(⑤⑥). 마커가 활성 노선만 보이는가(⑦).
6. **검문** — 추격자와 같은 역 도달 시 검문 발동 → 통과율(DBG 슬라이더)대로 통과/게임오버 로그(⑧).
7. **승강장** — 도착 시 DBG "승강장" 섹션에 환승/반대방향 버튼 → 환승 시 노선 바뀌고 새 노선 추격자 드러나는가(③).
8. **튜닝** — 통과율·연출속도·수배도 곡선을 DBG/ChaseConfig에서 만지며 "추격이 재밌는가"(§0) 감각 확인.

**가능성 있는 이슈**: 맵이 Screen Space-Overlay 팝업이라 런타임에 꺼져 있을 수 있음 / 역 클릭이 팝업 열려야 먹힘 / 시작 노선이 의도와 다를 수 있음(DebugMover.startLineIdOverride로 지정 가능).

## 5. 버퍼 백로그 (기획서 밖 편의) — 일부 완료, 나머지 저녁/다음
사용자와 합의한 버퍼. 전부 디버그 플래그 뒤, §13 정식 UI 불침범.

**✅ 완료(검증됨):**
- **H4 맵 검증 도구** — `Tools/Subway/Validate Map Data` 메뉴 + `MapValidator.BuildReport`. 실제 데이터 검증: 12노선·257역·환승 19.8%·1컴포넌트·오류0(건전).
- **H1 계측 핵심** — `ChaseMetrics`(최근접 거리·노선별 수, 순수·테스트됨), `RngService`(시드 고정, TrackerManager·InspectionSystem 연결).

**⏳ 남음(대부분 시각/플레이 의존 → 저녁 Play 검증과 함께 권장):**
- **H6 연출·그래픽** [미착수]: 비활성 노선 디밍, 추격자 트레일, 최근접 강조, 검문 깜빡임, 이동경로 하이라이트, 거리 색밴드, 현재노선 강조, 마커 펄스 + 그래픽 품질(부드러운 글라이드★·둥근 선·글로우·바운스·페이드·팔레트). ← **화면 확인 필요라 자율 보류**. ChaseMetrics.NearestTracker 재사용 가능.
- **H1 잔여** [미착수]: ChaseEvents 이벤트 허브, 상태 HUD(TMP 오버레이) — HUD는 시각이라 저녁.
- **H2/H3/H5** [미착수]: 세션통계+CSV / 거리 스파크라인 / 배속·일시정지·치트·config 프리셋. (치트 일부는 DebugPanel에 이미 있음: 세션리셋·스폰갱신·승강장·수배도.)

## 6. 튜닝값은 전부 [미확정] (§16, D단계)
n·m, 체증 곡선, 노선당 상한표, 통과율, 첫스폰 범위 등 ChaseConfig 기본값은 제안치. 플레이테스트로 확정.
