# 인수인계 (Gorail 프로토타입)

갱신: 2026-06-04 · 브랜치: **`feature/chase-slice`** (main 아님)

---

## 0. 한눈에 — 현재 상태

지하철 추격 생존 게임의 **핵심 루프가 코드·씬 배선·런타임까지 완결**됐다.

- **추격 슬라이스 ②~⑧**: 이동 · 추격 · 검문 · 승강장 · 활성노선 가시화 · 수배도
- **씬 시스템**(scene_system_spec): 명성→수배도 자동 환산, 멀티 씬 3공간(지하철/승강장/지상), 작품활동, §9-1 외부 복귀 검문
- **노선도 UX 추가 지시 D1~D11**: 활성색/회색, 줌 크기고정, 역명 최상위, 범례, 팝업·재오픈, 하차 흐름, 특별역 별 마커, 호버 이동 프리뷰
- **편의 H1·H6**: 상태 HUD(uGUI/TMP)+ChaseEvents 허브 / 연출(검문 깜빡임·최근접 헤일로·플레이어 글라이드)
- **검증**: EditMode 테스트 32개 통과 · Play로 전체 루프 한 바퀴 구동 · 콘솔 0 에러

> 전체 구현 기능 목록은 [FEATURES.md](FEATURES.md), 기획서 밖 추가 지시 누적은 [EXTRA_DIRECTIVES.md](EXTRA_DIRECTIVES.md).

---

## 1. 프로젝트 개요

- **장르/컨셉**: 서울 지하철 노선도 위에서 추격자를 따돌리며 작품활동으로 명성을 쌓는 2D 추격 생존 게임.
- **엔진**: Unity 6000.3.5f1, URP, 2D, uGUI + 일부 IMGUI(OnGUI) stub.
- **기획서** (`game_design_documents/`):
  - `chase_prototype_spec_v2_patched.docx` — 추격 슬라이스(이동·추격·검문·승강장·가시화). 본문에서 `§`로 인용.
  - `scene_system_spec.docx` — 승강장·지상 씬 + 명성·수배도. 본문에서 `scene§`로 인용.
  - ⚠️ **PDF는 한글 추출이 깨진다 → 반드시 docx로 읽을 것.**
- **추가 지시**: 기획서에 없는 사용자 지시는 [EXTRA_DIRECTIVES.md](EXTRA_DIRECTIVES.md)에 `D#`로 누적 기록(**매 명령마다 갱신**).

---

## 2. 아키텍처 (깨지 말 것)

- **멀티 씬 — 영속 베이스 + additive**
  `SubwayScene`이 모든 매니저·상태를 들고 항상 로드된 베이스. `PlatformScene`/`OutsideScene`은 그 위에 additive로 얹혔다 내려간다 → 상태가 파괴되지 않아 재바인딩 불필요. (`SpaceManager`)
- **런타임 로케이터 `GameCore`**
  additive 씬 컨트롤러는 `GameCore.Instance`로 베이스의 매니저(Game/Player/Fame/Trackers/Inspection/Space/Platform/TurnResolver/SceneConfig)에 접근. 직접 참조 배선 대신 이 통로를 쓴다.
- **경계 인터페이스로 단계 분리**
  `IChaseStubs.cs`의 `ITrackerStep`/`IInspection`/`IPlatform`. `TurnResolver`는 이 인터페이스에만 의존하고, 미구현 시 `Null*` 빈 구현으로 안전 동작. `ChaseSession`이 Awake에 실물을 `SetSystems`로 주입한다.
- **데이터 단일 소스(SO)**
  역 위치 = `StationData.mapPosition`. 게임플레이는 위치를 안 만지고 `Player`가 `PlayerLocationData`, `TrackerManager`가 `EnemyLocationData`(활성 노선 추격자만)에 현재 역을 써넣어 렌더러 마커를 재사용.
- **모든 튜닝값 = SO**
  추격은 `ChaseConfig`, 씬/명성/수배도는 `SceneConfig`. 코드에 수치 하드코딩 금지. 대부분 §16 [미확정] 자리표시자 → 플레이테스트로 확정.

---

## 3. 씬 구조

| 씬 | 역할 | 내용 |
|----|------|------|
| `SubwayScene` | **영속 베이스** | 매니저·상태 전부(`ChaseSystems` 루트) + 노선도 UI(`MapContent`). 카메라/라이트는 여기 것만. |
| `PlatformScene` | additive(승강장) | `PlatformSceneController`(IMGUI 4버튼). 카메라/라이트 없음. |
| `OutsideScene` | additive(지상) | `GroundSceneManager`(IMGUI 작품활동·체류). 카메라/라이트 없음. |
| `MainMenu` | 시작 | 시작 버튼 → SubwayScene 로드. |

승강장·지상 UI는 기획서대로 **IMGUI 버튼 stub** — 정식 UI(§13)는 별도 영역이라 침범하지 않는다.

---

## 4. 실행 · 검증 방법

### Play 전체 루프 체크리스트
`SubwayScene`을 Play → 좌상단 `DBG ▼` 디버그 패널 사용:
1. 노선도에서 **역 클릭**으로 이동(②) → 같은 노선 위 목적지까지 1역씩 전진. 다른 노선 역은 "환승 필요" 로그.
2. DBG **명성 슬라이더**↑ → 수배도↑ → **하차**(또는 DBG "스폰 갱신") 시 추격자 스폰(뒤 6~8역) → 이동마다 추격(⑤⑥), 활성 노선만 표시(⑦).
3. 추격자와 **같은 역** 도달 → 검문, 통과율대로 통과/게임오버(⑧).
4. **하차 버튼** → 승강장(4버튼): 재탑승·반대방향·환승(환승역만)·지상(특별역만).
5. 특별역에서 **지상** → 작품활동 상/중/하 성공 → 명성↑ → 수배도↑. 체류 후 **복귀** 시 체류분만큼 추격 전진 + 같은 역이면 검문(§9-1).
6. 무활동으로 명성 감소 → 수배도 자동 하강 → 추격자 솎임.
- DBG **공간 버튼**으로 이동 없이 승강장/지상 빠르게 진입 가능.

### 테스트 / 컴파일 (UnityMCP)
- **EditMode 테스트**: `run_tests`(mode EditMode, assembly `Game.Tests.EditMode`). 현재 32/32 통과.
- **컴파일 루틴**: `refresh_unity`(compile=request, mode=force, scope=scripts, wait) → `refresh_unity`(none, if_dirty, wait) → `read_console`(errors).

### ⚠️ UnityMCP 주의점 (실전에서 자주 막힌 것)
- 컴파일(도메인 리로드) 후 **씬 오브젝트 instanceID가 바뀐다** → 배선 시 매번 `find_gameobjects`로 ID 재조회.
- `set_property`는 **Play 중 사용 불가** → 먼저 Play 정지.
- Play 모드 `Destroy()`는 **프레임 끝에 반영**(지연) → 같은 `execute_code` 안에서 파괴 결과를 검사하면 stale. 다음 호출(다음 프레임)에서 확인.
- **stationId는 한글**이다(예: 시청·강남·노량진). 영문 id(`cityhall` 등) 없음.
- 게임 화면 스크린샷은 노선도가 **Screen Space-Overlay**라 카메라 렌더(Main Camera) 캡처엔 안 잡힌다 → 구조 검증은 `execute_code`로 계층 확인.

---

## 5. 알려진 이슈 · 주의점

- **`PlayerLocation.asset` 런타임 덮어쓰기**: `Player`가 매 스텝 현재 역을 SO에 써, 플레이 후 에셋이 dirty로 남는다. 시작 역은 `DebugMover.startStationId`로 분리돼 게임엔 영향 없지만, **커밋 전 `시청`으로 복구**한다(또는 렌더러가 SO 대신 `Player`에서 읽도록 리팩터하면 근본 해결).
- **추격자 겹침**: 동일 최단경로·동일 속도라 여러 추격자가 한 역에 뭉쳐 1마리처럼 보인다. 로직상 정상 — 분산/지터는 기획 판단(H6 또는 ⑥ 튜닝).
- **스폰 갱신 규칙(정리됨)**: 추격자 수 갱신(활성 노선별 상한까지 충원)은 **하차·지상 복귀·환승** 시 발생하고, **세션 시작 시 0으로 리셋**된다. 수배도 상승만으로는 즉시 안 늘고 다음 승강장 행동에서 채워진다. 노선당 상한(§5-2): 수배도 0/1/2/3/4/5 → 제안 0/1/1~2/2~3/3~4/4~5명, 현재 구현은 상단 고정값 `{0,1,2,3,4,5}`. ⚠️ **환승 즉시 충원은 §5-3 의도 반영한 설계 판단** — 압박이 과하면 "다음 하차까지 대기"로 되돌릴 수 있음.
- **재충원 vs 통과**: 검문 통과로 추격자를 없애도 다음 갱신에 상한까지 재충원 → 통과율과 함께 튜닝 포인트.
- **§9-1 검문 시점**: 현재 "복귀(승강장 진입) 즉시" 같은 역 추격자면 검문. 기획 "탑승 즉시"와 사실상 동일하나 엄밀히는 재탑승 순간은 아님(필요 시 조정).

---

## 6. 미구현 · 보류

기획서에 있으나 이번 슬라이스 범위 밖(전부 [추후/F] 또는 다음 단계):
- **H6 연출 잔여** — 적 마커 글라이드(현재 플레이어만), 추격자 트레일/잔상, 비활성 노선 디밍, 둥근 선/글로우 등. (핵심 3종은 구현: 검문 깜빡임·최근접 헤일로·플레이어 글라이드)
- **H2/H3/H5** — 세션 통계+CSV, 거리 스파크라인, 배속·일시정지·config 프리셋.
- **시작 시퀀스(§12)**, **돈 경제 · 마스크/인벤토리(§8-4)**, **순찰자/경찰**, **승강장 랜덤 이벤트**, **완성도 미니게임** — 기획서 [추후] 지정.

---

## 7. 다음 작업 후보 · 작업 관례

### 다음 후보
1. **튜닝(§16)** — `ChaseConfig`/`SceneConfig`의 [미확정] 수치를 Play로 확정(n·m, 체증 곡선, **노선당 상한표(현재 단일값 → 범위/랜덤화 검토)**, 통과율, 첫스폰 범위, 명성 증감·감소).
2. **H6 연출 잔여** — 적 마커 글라이드(현재 플레이어만)·추격자 트레일·비활성 노선 디밍 등.
3. **시작 시퀀스(§12)** — 외부 랜드마크 시작 → 첫 작품활동 → 첫 추격(현재는 디버그 부트스트랩으로 지하철 시청 시작).

### 작업 관례
- 작업은 `feature/chase-slice` 브랜치. 단계별로 잘게 커밋(`feat:`/`fix:`/`docs:`).
- 기획서 밖 사용자 지시를 받으면 **즉시 [EXTRA_DIRECTIVES.md](EXTRA_DIRECTIVES.md) 갱신**.
- 커밋 전 `PlayerLocation.asset`을 `시청`으로 복구하고 작업트리 클린 확인.
