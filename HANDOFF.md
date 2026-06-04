# 인수인계 — 다음 세션 Claude Code에게

작성: 2026-06-04 세션. 대상: 이어서 개발할 Claude Code.
이 문서를 먼저 읽고 시작해라. 프로젝트 전체 맥락은 `game_design_documents/chase_prototype_spec_v2_patched.pdf`(추격 슬라이스 기획서 v0.1)에 있다 — **반드시 정독**할 것.

---

## 1. 프로젝트 정체성
**지하철 추격 생존 게임** (Unity 6000.3.5f1, URP, 2D).
플레이어가 작품 활동으로 수배도를 올리면 Tracker(기자)가 지하철 노선망을 따라 추격 → 환승·방향전환으로 따돌리며 검문을 통과해 생존. 이번 프로토타입의 목표는 **"추격 루프 자체가 재미있는가"** 검증.

Unity 프로젝트 루트: `Gorail_proto/Gorail_proto/`. 코드는 `Assets/_Project/Scripts/`.

## 2. 기획서 빌드 순서 (§15)
①MapGraph+MapRenderer → ②TurnResolver(목적지→역단위 순차해소 코루틴) → ③PlatformController(승강장 4갈래) → ④DebugPanel(수배도 슬라이더) → ⑤TrackerManager 스폰 → ⑥Tracker 추격(1+2규칙+체증) → ⑦가시화(활성노선만) → ⑧InspectionSystem(검문). 모든 튜닝값은 **ChaseConfig ScriptableObject**로 뺀다.

## 3. 현재 상태 — ①단계 완료
맵 그래프 + **프리팹 기반 역 렌더링** + 에디터 편집 도구까지 완성. 다음은 ②TurnResolver.

### 이번 세션에 확정된 아키텍처 결정 (중요 — 깨지 말 것)
- **역 위치의 단일 소스 = `StationData.mapPosition`(SO)** (모델①). 프리팹/인스턴스 위치가 아니라 SO가 진짜 주소다.
- **역 시각요소는 `StationNode.prefab` 안에 존재**하고 `StationView.Configure(stationData, lineColors)`가 데이터로 설정만 한다. 공통 모양 변경 = 프리팹 1곳 수정. 점 개수·색만 데이터 주도로 인스턴스에 복제 생성(환승역=노선 수만큼).
- 원 스프라이트는 `Assets/_Project/Art/Sprites/Circle.png` 에셋(런타임 생성 아님).

### 핵심 파일
- `Scripts/Runtime/Subway/SubwayMapRenderer.cs` — 렌더러. `ApplyLayout()`(SO+Spread→화면 재배치, 모든 경로의 단일 통로), `BuildMap()`, `UpdateLines()`, `SavePositions()`.
- `Scripts/Runtime/Subway/StationView.cs` — 역 프리팹 자기구성 컴포넌트.
- `Scripts/Editor/SubwayMapRendererEditor.cs` — 인스펙터 버튼 + **Scene 개별 위치 편집 도구**(`StationSceneEditTool`, `SceneView.duringSceneGui` 전역 콜백).
- `Scripts/Runtime/Subway/MapGraph.cs` / `MapGraphProvider.cs` — ②단계용 그래프 인프라(BFS: Distance/ShortestPath/NextStepToward/GetNeighborsOnLine). **씬 미부착이지만 의도된 것, 지우지 말 것.**
- 데이터 SO: `Assets/_Project/Data/Subway/` (SubwayNetwork, 9개 노선, 257역, PlayerLocation, EnemyLocations).

### 에디터 워크플로 (기획자용)
인스펙터 버튼: **Build Map**(역+선 최초 생성) → **Apply Layout**(SO+Spread로 재배치) → **✋ Scene 위치 편집**(파란 점 드래그로 개별 역 이동) → **Update Lines** → **Save Positions**(SO 저장).

## 4. 함정 (반드시 인지)
- 맵 Canvas가 **Screen Space - Overlay** → Scene 뷰에서 역을 **일반 클릭으로 선택 불가**(오버레이가 기즈모를 덮고 좌표계가 어긋남). 위치 편집은 반드시 **✋ Scene 위치 편집 도구**(GUI 오버레이에 점 그림)로 한다.
- **stationSpread** 슬라이더는 이제 라이브 동작(OnValidate→ApplyLayout). 단 드래그 조정은 Spread 건드리기 전에 **Save Positions** 먼저.
- **프리팹(DotTemplate 등) 수정 후엔 Build Map 재실행**해야 생성된 역에 반영됨(생성된 점은 인스턴스 복제본).
- 역은 씬에 영구 저장 안 됨(Build/런타임 생성). StationView.stationData는 직렬화되어 인스턴스가 자기 역을 기억함.

## 5. 다음 작업 — ②TurnResolver (선행 포함)
선행: **Player 상태**(현재 역/노선/진행 방향, 누적 활성 노선) + **ChaseConfig SO**(역당 시간 X, n·m, 체증 곡선, 노선당 상한표, 통과확률 등 §15·§16 튜닝값).
②본체: 목적지 역 선택 → 출발~목적지를 **1역씩 코루틴 순차 해소**, 매 스텝 (1)플레이어 1역 전진+연출 (2)추격 1스텝 (3)같은 역 검문 판정. 목적지 도달 시 PlatformController로. 이동은 **현재 노선 위에서만**(노선 변경은 환승역 승강장에서만). `Scripts/Runtime/Gameplay/` 폴더가 비어 있으니 거기에 둔다.

## 6. 정리 완료 (이번 세션)
삭제: `Assets/_Recovery/`(크래시 복구본), `Assets/TutorialInfo/`+`Readme.asset`(URP 템플릿 샘플), `SubwaySceneSetup.cs`(1회성 씬생성 메뉴).
유지: MapGraph(C1), Outside/Platform 씬(C2, placeholder), Korail L 폰트, 빈 .gitkeep 폴더.

## 7. 미커밋 변경
이번 세션 작업물이 아직 커밋 안 됨(main 브랜치). 제안 커밋명:
`feat: 역 노드 프리팹화 + Scene 개별 위치 편집 도구 추가`. 정리 삭제분은 별도 커밋 권장: `chore: 레거시 에셋 정리(_Recovery·TutorialInfo·SubwaySceneSetup)`.

## 8. 툴링
UnityMCP 연결됨(MCP For Unity). 스크립트 수정 후 루틴: `validate_script`(standard) → `refresh_unity`(force/scripts/compile=request, wait) → `read_console`(errors). 프리팹/씬 조작·검증은 `execute_code`(메서드 본문, using 금지, 정규화 이름 사용)로 가능.
