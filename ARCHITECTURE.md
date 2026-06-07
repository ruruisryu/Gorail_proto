# 구현 아키텍처 가이드 (인수자용)

이 문서는 **코드가 어떻게 도는지**를 설명한다. (상태 요약은 [HANDOFF.md](HANDOFF.md), 기능 목록은 [FEATURES.md](FEATURES.md), 기획서 밖 지시는 [EXTRA_DIRECTIVES.md](EXTRA_DIRECTIVES.md).)

- **엔진**: Unity 6000.3.5f1 · URP · 2D
- **UI**: uGUI(노선도·HUD) + TMP(텍스트) + IMGUI/OnGUI(디버그·승강장·지상 stub)
- **언어/패턴**: C# MonoBehaviour + ScriptableObject 데이터 + 이벤트 구동
- 코드 루트: `Gorail_proto/Assets/_Project/Scripts/`

---

## 1. 폴더 구조

```
Scripts/
├─ Runtime/
│  ├─ Core/        GameCore(로케이터) · GameManager(세션상태) · SpaceManager(씬전환)
│  ├─ Gameplay/    이동·추격·검문·승강장·명성/수배도 + 디버그/계측/이벤트
│  ├─ Subway/      노선도 데이터(SO)·그래프·렌더러·역 뷰
│  ├─ Data/        ChaseConfig·SceneConfig(튜닝 SO)
│  └─ UI/          HUD·승강장/지상 컨트롤러·노선도 줌/팝업/범례
├─ Editor/         맵 검증 툴·렌더러 인스펙터 버튼
└─ Tests/EditMode/ 순수 로직 테스트 32개

Assets/_Project/
├─ Data/Subway/    PlayerLocation·EnemyLocations(런타임 위치 SO)
├─ Data/ScriptableObjects/  ChaseConfig·SceneConfig(.asset)
├─ Scenes/         SubwayScene(베이스)·PlatformScene·OutsideScene·MainMenu
└─ Fonts/          Korail L/M/B SDF (한글 TMP)
```

---

## 2. 핵심 아키텍처 패턴 5가지 (먼저 이해할 것)

### ① 멀티 씬 — 영속 베이스 + additive
- **`SubwayScene`이 항상 떠 있는 베이스**다. 모든 매니저·상태가 여기 산다.
- 승강장/지상은 `PlatformScene`/`OutsideScene`을 **additive로 로드/언로드**(`SpaceManager`). 베이스가 안 죽으니 **상태 재바인딩이 불필요**하다.
- 왜? 상태(플레이어·추격자·명성)를 한 씬에 두고 공간만 갈아끼우면, 씬 전환마다 상태를 직렬화/복원할 필요가 없다.

### ② 서비스 로케이터 `GameCore`
- `GameCore.Instance`가 베이스의 매니저(Player/Trackers/Inspection/Fame/Game/Space/Platform/TurnResolver/Graph/SceneConfig)를 노출한다.
- additive 씬의 컨트롤러(승강장·지상)는 **직접 참조 배선 대신 `GameCore.Instance.XXX`** 로 접근한다. → additive 씬이 베이스 오브젝트를 인스펙터로 참조할 수 없는 문제를 해결.

### ③ 경계 인터페이스 + 주입 (`IChaseStubs` / `ChaseSession`)
- `TurnResolver`는 추격·검문·승강장을 **인터페이스로만** 안다: `ITrackerStep` / `IInspection` / `IPlatform`.
- 구현이 없으면 `Null*` 빈 구현으로 안전 동작하고, **`ChaseSession.Awake`가 실물(TrackerManager·InspectionSystem·PlatformController)을 `SetSystems`로 주입**한다.
- 왜? 단계별로 만들 때 서로를 직접 참조하지 않게 해 결합도를 낮춘다(테스트·교체 쉬움).

### ④ 데이터 단일 소스 = ScriptableObject
- **역 위치의 유일한 소스 = `StationData.mapPosition`**. 게임플레이 코드는 위치를 만들지 않고, 렌더러가 이 값을 읽어 그린다.
- 플레이어/적의 "현재 어느 역인가"는 `PlayerLocationData` / `EnemyLocationData`(SO)에 기록 → 렌더러가 그 SO를 보고 마커를 갱신.
- **모든 튜닝 수치는 `ChaseConfig`(추격) / `SceneConfig`(명성·수배도·씬)** 에. 코드에 수치 하드코딩 없음.

### ⑤ 이벤트 구동 (시스템 → `ChaseEvents` 허브 → HUD/연출)
- 각 시스템이 C# 이벤트를 쏜다(`FameChanged`·`WantedChanged`·`InspectionResolved`·`GameOverOccurred`·`StepResolved`…).
- **`ChaseEvents` 허브가 이를 한 곳에서 모아 재발행** → HUD(`StatusHud`)·연출(`ChaseFx`)은 허브 하나만 구독한다.

---

## 3. 데이터 흐름 — 한 바퀴 따라가기

```
[노선도에서 역 클릭]
   StationView(IPointerClick) → StationClicked 이벤트
   → StationClickRouter → TurnResolver.TryMoveTo(역)

[이동 해소]  TurnResolver.ResolveMove (코루틴, 역 1칸씩)
   매 역마다:  Player.StepTo → 렌더러 RefreshMarkers
              → ITrackerStep.Advance (추격 1스텝)
              → IInspection.ResolveAt (같은 역이면 검문)
   도착해도 지하철 유지 (자동 승강장 진입 안 함)

[하차]  SubwayHud '하차' 버튼 → Platform.OpenAt(역)
   → TrackerManager.OnPlayerDisembark (수배도 상한까지 스폰)
   → SpaceManager.EnterPlatform (PlatformScene additive 로드)

[승강장]  PlatformSceneController(IMGUI 4버튼)
   재탑승 / 반대방향 / 환승(환승역만) / 지상(특별역만) → Platform.* → SpaceManager

[지상]  GroundSceneManager — 작품활동(명성↑) → FameSystem
   → WantedSystem이 명성 구간으로 수배도 자동 환산 → GameManager.WantedLevel
   복귀 시: 체류분만큼 추격 전진(AdvanceAll) + Platform.OpenAt(스폰 갱신) + 검문(§9-1)

[표시]  명성/수배도/검문/게임오버 이벤트 → ChaseEvents → StatusHud(HUD)·ChaseFx(연출)
```

---

## 4. 서브시스템별 구현

### 맵 & 노선도 렌더링 — `Subway/`
- **데이터**: `SubwayNetworkData`(노선 목록) → `LineData`(역 순서·색·순환여부) → `StationData`(id·표시명·`mapPosition`·기능타입). 12노선·257역.
- **그래프**: `MapGraph`(+`MapGraphProvider`로 지연 빌드) — 최단경로 1스텝(`NextStepToward`)·거리·노선순서 경로(`GetLineOrderedPath`, 순환선 짧은쪽)·연결 노선. **추격·이동·프리뷰가 전부 이 그래프 하나에 의존.**
- **렌더러**: `SubwayMapRenderer` — `mapPosition`을 UI 좌표로 변환해 역·선·마커를 uGUI로 그린다. 에디터 워크플로(Build Map / Update Lines / Save Positions)는 `Editor/SubwayMapRendererEditor`.
- **역 뷰**: `StationView`(프리팹) — 클릭/호버 이벤트 발신, 환승역 점 복제, 특별역 별(★) 마커, 역명 라벨 항상 최상위(`Canvas.overrideSorting`).
- **마커 레이어 구조(중요)**: 렌더러는 `mapContainer` 아래에 이름표 컨테이너로 레이어를 나눈다 —
  `[Lines]` `[Stations]` `[Preview]`(호버 프리뷰) `[Fx]`(연출) `[Player]`(글라이드 마커). `RefreshMarkers`는 이 이름표 레이어들을 **건드리지 않고** 일반 적 마커만 재생성한다.

### 이동 해소 — `Gameplay/TurnResolver.cs`
- 기획 핵심: **"입력은 크게, 해소는 잘게"**. 목적지 1번 클릭(입력 단위) → 출발~목적지를 **1역씩 코루틴으로 순차 해소**(해소 단위).
- 매 역: 전진 → `Advance` → 검문, 순서 엄수. 도착역은 항상 검문, 중간역은 config 토글.
- `Player`가 권위 상태(현재 역·노선·방향·누적 활성노선)를 들고, 매 스텝 `PlayerLocationData`에 동기화.

### 추격자 — `Gameplay/Tracker.cs`·`TrackerManager.cs`
- `Tracker`(순수 클래스): **1규칙(속도, m역 전진) + 2규칙(경로, 최단경로 방향·노선 비제약)** 을 `ChaseToward(graph, 목표, steps)`로 수행.
- `TrackerManager`: 스폰(노선당 상한, **하차 시 갱신**)·추격(체증 `AnimationCurve`로 다량이동 가중)·수배도 하락 시 거리밴드 비례 제거. `ITrackerStep`으로 주입.
- 가시화(§6): 활성 노선 위 추격자만 `EnemyLocationData`에 써 표시(안 가본 노선의 추격자는 숨김).

### 검문 — `Gameplay/InspectionSystem.cs`
- 같은 역에서 추격자와 만나면 **확률 게이트**(`ChaseConfig.inspectionPassRate`). 통과=해당 추격자 제거, 실패=`GameManager.TriggerGameOver`. `IInspection`으로 주입.

### 명성·수배도 — `Gameplay/FameSystem.cs`·`WantedSystem.cs`
- `FameSystem`: 작품활동 성공 시 가산(완성도 상/중/하), 무활동 시 시간 감소.
- `WantedSystem`: **수배도는 독립 자원이 아니라 명성 구간으로 실시간 환산**(`SceneConfig.WantedLevelForFame`). 하락 시 `TrackerManager.TrimToCaps`.

### 공간 전환 — `Core/SpaceManager.cs` + 컨트롤러
- 3공간(지하철/승강장/지상). `EnterPlatform`/`EnterSubway`/`EnterGround`가 additive 로드·언로드 + 지하철일 때만 노선도 표시.
- 승강장 UI = `PlatformSceneController`, 지상 UI = `GroundSceneManager` (둘 다 IMGUI stub, `GameCore.Instance`로 로직 호출).

### HUD & 연출 — `Gameplay/ChaseEvents.cs`·`UI/StatusHud.cs`·`Gameplay/ChaseFx.cs`
- `ChaseEvents`: 이벤트 허브(2번 패턴 ⑤).
- `StatusHud`: **uGUI/TMP 오버레이를 코드로 생성**(폰트만 인스펙터 주입). 상태 상시 표시 + 검문 토스트 + 게임오버 배너.
- `ChaseFx`: `[Fx]` 오버레이에 검문 깜빡임·최근접 추격자 헤일로. 플레이어 마커 글라이드는 렌더러의 `[Player]` 영속 컨테이너 + `Update` 보간.

---

## 5. 씬 & 컴포넌트 배선

- **`SubwayScene` / `ChaseSystems` GameObject** 에 매니저가 모여 있다:
  `GameCore` · `GameManager` · `Player` · `FameSystem` · `WantedSystem` · `TrackerManager` · `InspectionSystem` · `MapGraphProvider` · `SpaceManager` · `PlatformController` · `TurnResolver` · `ChaseSession`(주입) · `DebugPanel` · `DebugMover` · `StationClickRouter` · `MapActivationView` · `ChasePreview` · `ChaseEvents` · `ChaseFx` · `StatusHud`.
- **노선도 렌더러**(`SubwayMapRenderer`)는 `SubwaySceneUI` 캔버스 아래 `MapContent`에 있다.
- 컴포넌트 간 참조는 인스펙터 SerializeField로 배선(같은 씬), additive 씬은 `GameCore`로 접근.
- ⚠️ **컴파일(도메인 리로드) 후 씬 오브젝트 instanceID가 바뀐다** → 배선 작업 시 매번 이름으로 다시 찾을 것.

---

## 6. 사용한 Unity 기능/기법

| 기법 | 어디에 |
|---|---|
| ScriptableObject 데이터/설정 | 노선망·역·위치·튜닝 전부 |
| additive `SceneManager` 로드/언로드 | 3공간 전환(`SpaceManager`) |
| 코루틴 | 역단위 순차 이동 해소(`TurnResolver`) |
| `AnimationCurve` | 다량이동 체증 가중(`ChaseConfig.congestionCurve`) |
| 절차적 스프라이트(`Texture2D`) | 원형 마커·특별역 별(★) |
| `Canvas.overrideSorting` | 역명 라벨 항상 최상위 |
| uGUI `RectTransform.anchoredPosition` | 모든 역·선·마커 배치 |
| 프레임 독립 보간(`1-exp(-k·dt)`) | 플레이어 마커 글라이드 |
| TMP(SDF 한글 폰트) | HUD 텍스트 |
| IMGUI(`OnGUI`) | 디버그 패널·승강장·지상 stub UI |
| C# 이벤트 | 시스템 → `ChaseEvents` → HUD/연출 |

---

## 7. 바꾸려면 어디를?

- **밸런스 수치**(추격 속도 m, 체증 곡선, 노선당 상한표, 검문 통과율, 첫스폰 범위, 명성 증감·수배도 경계) → `ChaseConfig`·`SceneConfig`(.asset). 코드 수정 불필요.
- **맵(노선·역·위치)** → `SubwayNetworkData`/`LineData`/`StationData` SO. 위치는 렌더러의 Save Positions로 저장.
- **역 기능**(랜드마크/상점/안전역) → `StationData.featureType`.
- **연출 느낌**(글라이드 속도·헤일로·깜빡임 색) → `SubwayMapRenderer.PlayerGlideSharpness`, `ChaseFx` 상수.
- **시작 역/노선** → `DebugMover.startStationId` / `startLineIdOverride`.

---

## 8. 함정 & 관례 (반드시 숙지)

- **stationId는 한글**이다(시청·강남·노량진). 영문 id 없음.
- **`PlayerLocation.asset`은 플레이 중 현재 위치로 덮여쓰인다** → 커밋 전 `시청`으로 복구. (시작역은 `DebugMover.startStationId`로 분리되어 게임엔 영향 없음.)
- **`EnemyLocationData`는 "표시용 마커 데이터"** 이지 런타임 추격자 리스트가 아니다. 진짜 추격자는 `TrackerManager.Trackers`. 둘을 혼동하면 "보이는데 검문 안 됨/안 움직임" 류 버그가 난다. (세션 시작 시 `ResetAll`로 동기화.)
- **추격자 스폰은 "승강장 도착(하차/지상복귀/환승)" 시점에만** 갱신된다. 수배도만 올린다고 즉시 안 늘어난다.
- **마커는 매 `RefreshMarkers`마다 재생성**된다. 애니메이션/지속 표시가 필요한 건 `[Preview]`/`[Fx]`/`[Player]` 오버레이 레이어에 따로 그린다(재생성에 안 지워짐).
- **`Space`는 `Game.Core.Space`와 `UnityEngine.Space`가 충돌**한다 → 항상 `Game.Core.Space`로 정규화.
- 플레이 모드 `Destroy()`는 **프레임 끝에 반영**(지연). 같은 프레임에 파괴 결과 검사 금지.
- 작업 관례: 브랜치 `feature/chase-slice`, 단계별 잘게 커밋, 기획서 밖 지시는 `EXTRA_DIRECTIVES.md`에 누적.
