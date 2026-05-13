# Unity 프로젝트 폴더 구조 셋업 지시

당신은 Unity 프로젝트의 폴더 구조를 정립하는 작업을 수행합니다. 아래 지시사항을 정확히 그대로 따라 주세요.

## 0. 사전 확인
- 현재 작업 디렉토리가 Unity 프로젝트 루트(`Assets/`, `ProjectSettings/`, `Packages/` 폴더가 존재하는 위치)인지 먼저 확인합니다.
- 아닐 경우 작업을 중단하고 사용자에게 현재 경로를 보고한 뒤 지시를 다시 받습니다.
- 이미 동일 이름의 폴더가 존재하면 덮어쓰지 말고 건너뛴 뒤 마지막에 보고합니다.

## 1. 생성할 폴더 구조
`Assets/` 하위에 아래 트리를 정확히 생성하세요. 빈 폴더가 Git에서 유지되도록 각 최하위 폴더에 빈 `.gitkeep` 파일을 만들어 주세요.

```
Assets/
├── _Project/                  # 이 프로젝트 전용 에셋 루트 (밑줄로 항상 상단 정렬)
│   ├── Scripts/
│   │   ├── Runtime/
│   │   │   ├── Core/          # GameManager, 부트스트랩, 싱글톤
│   │   │   ├── Gameplay/      # 플레이어, 적, 인터랙션 등 게임플레이 로직
│   │   │   ├── UI/            # UI 컨트롤러, 뷰모델
│   │   │   ├── Data/          # ScriptableObject 클래스, 데이터 모델
│   │   │   └── Utils/         # 확장 메서드, 헬퍼
│   │   └── Editor/            # 에디터 전용 스크립트 (반드시 Editor 폴더에)
│   ├── Scenes/
│   │   ├── Main/              # 부트/메인 씬
│   │   ├── Levels/            # 실제 레벨 씬
│   │   └── Test/              # 디버그/테스트 씬
│   ├── Prefabs/
│   │   ├── Characters/
│   │   ├── Environment/
│   │   ├── UI/
│   │   └── VFX/
│   ├── Art/
│   │   ├── Models/            # .fbx 등 3D 모델
│   │   ├── Textures/
│   │   ├── Materials/
│   │   ├── Shaders/
│   │   ├── Sprites/           # 2D 에셋
│   │   ├── Animations/        # 클립, 애니메이터 컨트롤러
│   │   └── VFX/               # 파티클, VFX Graph
│   ├── Audio/
│   │   ├── Music/
│   │   ├── SFX/
│   │   └── Voice/
│   ├── Data/
│   │   ├── ScriptableObjects/ # SO 인스턴스 에셋
│   │   └── Resources/         # Resources.Load 전용 (꼭 필요한 경우만)
│   ├── Fonts/
│   ├── Settings/              # URP/HDRP 설정, Input Actions, 렌더 파이프라인 에셋
│   └── Localization/
├── Plugins/                   # DOTween, Odin 등 서드파티 플러그인
├── ThirdParty/                # 에셋스토어 패키지
└── StreamingAssets/           # 런타임에 그대로 로드되는 원본 파일
```

## 2. 컨벤션 규칙 (반드시 준수)
- **`_Project` 격리 원칙**: 우리가 만든 모든 에셋은 `_Project/` 하위에만 둡니다. 외부 에셋과 절대 섞지 않습니다. 이유: 서드파티 업데이트 시 충돌과 머지 지옥을 막기 위함입니다.
- **외부 에셋 분리**: 에셋스토어/플러그인은 `Plugins/` 또는 `ThirdParty/`로 분리합니다.
- **Resources 최소화**: `Resources/`는 빌드 크기·메모리·시작 시간에 악영향이 있으므로 동적 로딩이 정말 필요한 경우에만 사용합니다. 기본 권장은 직접 참조 또는 Addressables입니다.
- **Editor 폴더 규칙**: 에디터 전용 코드는 반드시 `Editor` 이름의 폴더에 두어 빌드에서 자동 제외되도록 합니다.
- **네이밍**: 폴더명은 PascalCase, 스크립트는 PascalCase, 에셋 파일은 `Category_Name` 패턴(예: `SFX_Jump`, `Tex_Player_Albedo`).

## 3. 추가 산출물
1. 프로젝트 루트(`Assets`와 같은 레벨)에 `FOLDER_STRUCTURE.md`를 생성하고, 각 폴더의 용도·사용 규칙을 한국어로 정리해 주세요.
2. 프로젝트 루트에 `.gitignore`가 없으면 Unity 표준 항목(`Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`, `*.user`, `*.unitypackage`, `.vs/`, `.idea/` 등)으로 생성해 주세요.
3. `_Project/Scripts/Runtime` 하위에 빈 어셈블리 정의 파일(`Game.Runtime.asmdef`), `Editor` 하위에 `Game.Editor.asmdef`를 생성해 주세요. asmdef를 사용하면 컴파일 속도와 의존성 관리가 좋아집니다.

## 4. 작업 완료 후 보고
- 최종 폴더 트리를 `tree` 형식으로 출력
- 건너뛴 항목(이미 존재했던 폴더 등)
- 사용자에게 추가로 확인이 필요한 사항(예: 2D/3D 여부에 따라 일부 폴더 제외 가능성)