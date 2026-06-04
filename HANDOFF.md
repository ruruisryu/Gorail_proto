# 인수인계 — 추격 슬라이스 ②~⑧ 구현 세션

작성: 2026-06-04 세션. 브랜치: **`feature/chase-slice`** (main 아님). 기획서: `game_design_documents/chase_prototype_spec_v2_patched.docx`(v0.1) + `scene_system_spec.docx`(승강장·지상·명성·수배도) — PDF는 한글 추출이 깨지니 docx로 읽어라. 기획서 밖 추가 지시는 루트 `EXTRA_DIRECTIVES.md`에 누적 기록.

---

## 0. 최신 상태 (후속 세션 — 추격 슬라이스 + 씬 시스템 전부 구현·Play 검증 완료)

**핵심 루프가 코드·씬배선·런타임까지 완결**됨 (EditMode 32 통과, Play로 한 바퀴 검증, 콘솔 0에러):
- **추격 슬라이스 ②~⑧**: 이동·추격·검문·승강장·가시화·수배도.
- **씬 시스템(scene_system_spec)**: 명성(Fame)→수배도 자동 환산, 멀티씬 3공간(지하철/승강장/지상), 작품활동, §9-1 외부 복귀.
- **노선도 UX(D1~D6)**: 활성=색/비활성=회색, 줌 크기고정, 역명 최상위, 범례, 팝업 버튼.

### 멀티씬 구조 (중요)
- **SubwayScene = 영속 베이스**(모든 매니저·상태 보유, 항상 로드). `PlatformScene`/`OutsideScene`은 **additive 로드/언로드**(상태 안 날아감).
- `GameCore`(런타임 로케이터, ChaseSystems) — additive 씬 컨트롤러가 `GameCore.Instance`로 매니저 접근.
- `SpaceManager` — 지하철↔승강장↔지상 전환 + 지하철 공간일 때만 노선도(SubwayMapPopup) 표시.
- 승강장/지상 UI는 **IMGUI(OnGUI) 버튼 stub**(기획서대로) — 씬엔 컨트롤러 GameObject만, 카메라/라이트는 SubwayScene 것 사용.

### 명성·수배도 (scene_system_spec)
- `FameSystem`: 작품활동 성공 +(상30±6/중20±4/하10±2), 무활동 시간감소. `WantedSystem`: 명성 구간(5/25/45/75/200)→수배도 0~5 자동. `SceneConfig.asset`에 수치.
- DebugPanel: **명성 슬라이더+작품 버튼**으로 수배도 통일(수동 슬라이더 폐지), **공간 강제전환 버튼** 추가.

### 🎮 저녁/다음 세션 — 전체 루프 직접 플레이 검증
Play(SubwayScene) → 좌상단 DBG:
1. 노선도에서 **역 클릭**으로 이동(②) → 도착 시 **승강장 씬**(4버튼) 뜨는가.
2. 승강장: **환승**(환승역만 노선목록)·**반대방향**·**재탑승** → 지하철 복귀.
3. 랜드마크역(시청·강남·홍대입구·잠실·여의도·명동·종로3가)에서 **지상으로** → **지상 씬 6버튼**.
4. 작품활동 **상/중/하 성공** → 명성↑ → 수배도↑(DBG 확인) → 이동 시 추격자 스폰·추격.
5. 지상 **체류**(강제도주 시간 짧게 SceneConfig에서 조정 가능) → **복귀** 시 추격자 전진·같은역이면 검문(§9-1).
6. 무활동으로 명성 감소 → 수배도 자동 하강.
- DBG **공간 버튼**으로 이동 없이 승강장/지상 빠르게 진입 가능.

### 알려진 나머지 / 보류
- **§9-1 검문 시점**: 현재 "복귀(승강장 진입) 즉시" 역에 추격자 있으면 검문. 기획 "탑승 즉시"와 사실상 동일하나 엄밀히는 재탑승 순간이 아님(필요 시 조정).
- **마스크/인벤토리(§8-4)**: 검문이 확률 게이트(§8-2)라 마스크 미사용 — 기능 훅 없음(보류).
- **시각 폴리싱**(부드러운 이동 연출 등 H6 그래픽)·**시작 시퀀스(§12)**·돈 경제·순찰자/경찰 등은 [추후/F].
- PlayerLocation.asset은 런타임에 현재 위치로 덮여써짐(시작역은 DebugMover.startStationId로 분리). 커밋 전 시청으로 복구하는 중.

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

## 4.5 Play 검증 결과 (이 세션에 직접 구동)
UnityMCP로 Play 구동·런타임 점검·수정을 반복해 **추격 루프 전 구간이 런타임에 동작함을 확인**(콘솔 예외 0):
이동→스폰(수배도별 상한)→추격→검문(통과/실패/중간역)→게임오버→환승(Line2·활성노선 누적)→순환선 이동→다른노선 클릭 거부→이동중 입력 무시→수배도 하락 제거.

**수정한 런타임 버그 3건(커밋 `17fa664`):**
1. `MapGraphProvider.Graph`가 Play 시작 시 null → **self-healing 지연 빌드**로 변경.
2. 시작역 id `'cityhall'`(미존재) → `'시청'`(실제 id). stationId는 **한글**임(예: 시청·종각·한양대).
3. 시작역 소스가 PlayerLocation SO였는데 이 SO가 플레이 중 현재 위치로 덮여써짐 → **DebugMover.startStationId**로 시작역 단일 소스 분리.

**알려진 이슈/피드백 (저녁에 논의):**
- ⚠️ **PlayerLocation.asset이 런타임에 현재 위치로 계속 덮여써져 에디터에서 persist됨**(매 플레이 후 에셋 dirty). 시작은 startStationId로 분리해 영향 없지만, 깔끔하려면 렌더러가 PlayerLocationData 대신 Player에서 현재 역을 읽게 리팩터(① 렌더러 영역) 권장. 지금은 플레이 후 수동 원복 중.
- 💬 추격자 여럿이 **같은 역에 겹침**(동일 최단경로·동일 속도라 뭉침) — 로직상 정상이나 시각적으로 1마리처럼 보임. 분산/지터를 줄지는 기획 판단(H6 또는 ⑥ 튜닝).
- 💬 검문 통과(어그로 해제)로 추격자를 없애도 **하차 스폰(§5-1)이 즉시 상한까지 재충원** — 압박이 금방 회복됨. 스펙대로지만 통과율과 함께 튜닝 포인트.

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
