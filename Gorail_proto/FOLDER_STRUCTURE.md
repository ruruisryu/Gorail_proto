# 프로젝트 폴더 구조

## 개요

이 프로젝트는 서드파티 에셋과 프로젝트 고유 에셋을 명확히 분리하는 구조를 따릅니다.
우리가 만든 모든 에셋은 `Assets/_Project/` 하위에만 위치합니다.

---

## 폴더 구조

```
Assets/
├── _Project/                  # 프로젝트 전용 에셋 루트
│   ├── Scripts/
│   │   ├── Runtime/           # 빌드에 포함되는 런타임 스크립트 (Game.Runtime.asmdef)
│   │   │   ├── Core/          # GameManager, 부트스트랩, 싱글톤 등 핵심 시스템
│   │   │   ├── Gameplay/      # 플레이어, 적, 인터랙션 등 게임플레이 로직
│   │   │   ├── UI/            # UI 컨트롤러, 뷰모델
│   │   │   ├── Data/          # ScriptableObject 클래스 정의, 데이터 모델
│   │   │   └── Utils/         # 확장 메서드, 범용 헬퍼
│   │   └── Editor/            # 에디터 전용 스크립트 (Game.Editor.asmdef, 빌드 제외)
│   ├── Scenes/
│   │   ├── Main/              # 부트씬, 메인 메뉴 씬
│   │   ├── Levels/            # 실제 게임 레벨 씬
│   │   └── Test/              # 디버그 및 기능 테스트용 씬
│   ├── Prefabs/
│   │   ├── Characters/        # 플레이어, NPC, 적 프리팹
│   │   ├── Environment/       # 배경, 지형, 장애물 프리팹
│   │   ├── UI/                # UI 패널, 팝업, HUD 프리팹
│   │   └── VFX/               # 이펙트 프리팹
│   ├── Art/
│   │   ├── Models/            # .fbx 등 3D 모델 원본
│   │   ├── Textures/          # 텍스처 이미지
│   │   ├── Materials/         # 머티리얼 에셋
│   │   ├── Shaders/           # 커스텀 셰이더
│   │   ├── Sprites/           # 2D 스프라이트 에셋
│   │   ├── Animations/        # 애니메이션 클립, Animator Controller
│   │   └── VFX/               # 파티클 시스템, VFX Graph
│   ├── Audio/
│   │   ├── Music/             # 배경음악 (BGM)
│   │   ├── SFX/               # 효과음
│   │   └── Voice/             # 보이스 오버, 대사
│   ├── Data/
│   │   ├── ScriptableObjects/ # SO 인스턴스 에셋 (데이터 파일)
│   │   └── Resources/         # Resources.Load() 전용 — 꼭 필요한 경우에만 사용
│   ├── Fonts/                 # 폰트 에셋
│   ├── Settings/              # URP/HDRP 설정, Input Actions, 렌더 파이프라인 에셋
│   └── Localization/          # 다국어 지원 에셋
├── Plugins/                   # DOTween, Odin 등 서드파티 플러그인
├── ThirdParty/                # 에셋스토어 패키지
└── StreamingAssets/           # 런타임에 원본 그대로 로드되는 파일
```

---

## 컨벤션 규칙

### `_Project` 격리 원칙
우리가 만든 모든 에셋은 반드시 `_Project/` 하위에만 위치합니다.
외부 에셋(Plugins, ThirdParty)과 절대 섞지 않습니다.
> 이유: 서드파티 업데이트 시 충돌 및 머지 충돌 방지

### 외부 에셋 분리
- 에셋스토어 구매 패키지 → `ThirdParty/`
- 코드 플러그인(DOTween, Odin 등) → `Plugins/`

### Resources 최소화
`Resources/`는 빌드 크기, 메모리, 시작 시간에 악영향을 줍니다.
기본 권장: 직접 참조 또는 Addressables 사용. `Resources/`는 동적 로딩이 필수인 경우에만 사용.

### Editor 폴더 규칙
에디터 전용 코드는 반드시 `Editor` 이름의 폴더에 위치시켜야 빌드에서 자동 제외됩니다.

### 네이밍 규칙
| 대상 | 규칙 | 예시 |
|---|---|---|
| 폴더명 | PascalCase | `GameManager`, `PlayerController` |
| 스크립트 | PascalCase | `PlayerController.cs` |
| 텍스처 | `Tex_대상_속성` | `Tex_Player_Albedo` |
| 효과음 | `SFX_이름` | `SFX_Jump`, `SFX_Explosion` |
| 음악 | `BGM_이름` | `BGM_MainTheme` |
| 프리팹 | `PFB_이름` | `PFB_Player`, `PFB_Enemy` |

---

## 어셈블리 정의 파일 (asmdef)

| 파일 | 위치 | 용도 |
|---|---|---|
| `Game.Runtime.asmdef` | `_Project/Scripts/Runtime/` | 런타임 코드 어셈블리 |
| `Game.Editor.asmdef` | `_Project/Scripts/Editor/` | 에디터 전용 코드 어셈블리 |

asmdef를 사용하면 코드 변경 시 전체 재컴파일 대신 해당 어셈블리만 재컴파일되어 **컴파일 속도가 크게 향상**됩니다.
