using UnityEngine;
using Game.Core;
using Game.Gameplay;
using Game.Inventory;   // [추가] ArtworkScreen 참조

namespace Game.UI
{
    /// <summary>
    /// [S4] 지상 씬(scene_system_spec §3). 작품활동을 5분 단위 틱으로 진행하며,
    /// 추격자가 현재 역에 도달하면 작품이 강제 실패되고 지하철로 복귀한다.
    /// 작품활동은 ArtworkScreen(가방+재료 배치 UI)에서 완성도로 등급이 정해진다.
    /// </summary>
    public class GroundSceneManager : MonoBehaviour
    {
        [Header("작품활동 팝업 (같은 OutsideScene 안의 ArtworkScreen 연결)")]
        [SerializeField] private ArtworkScreen artworkScreen;

        [Header("승강장 복귀 버튼 (실패 시 빨강 강조 §5-2)")]
        [Tooltip("복귀 버튼의 Image. 실패 시 빨강 스프라이트로 교체.")]
        [SerializeField] private UnityEngine.UI.Image returnButtonImage;
        [Tooltip("평상시 스프라이트(씬 진입 시 복원). 비우면 복원 안 함.")]
        [SerializeField] private Sprite returnNormalSprite;
        [Tooltip("실패 시 빨강 강조 스프라이트.")]
        [SerializeField] private Sprite returnFailSprite;
        [Tooltip("성공 시 파랑 강조 스프라이트(선택, §5-1). 비우면 평상시 유지.")]
        [SerializeField] private Sprite returnSuccessSprite;
        [Tooltip("복귀 버튼(작품활동 중 비활성화). 비우면 returnButtonImage에서 자동 탐색.")]
        [SerializeField] private UnityEngine.UI.Button returnButton;
        [Header("복귀 버튼 호버 하이라이트 (상태별 오버레이)")]
        [Tooltip("복귀 버튼의 ButtonHoverHighlight. 비우면 returnButtonImage에서 자동 탐색.")]
        [SerializeField] private ButtonHoverHighlight returnHover;
        [Tooltip("평상시 호버 하이라이트.")]
        [SerializeField] private Sprite returnNormalHighlight;
        [Tooltip("실패(빨강) 상태 호버 하이라이트.")]
        [SerializeField] private Sprite returnFailHighlight;
        [Tooltip("성공(파랑) 상태 호버 하이라이트.")]
        [SerializeField] private Sprite returnSuccessHighlight;
        [Tooltip("진행 게이지 뷰. 패널이 닫힐 때 복귀 버튼을 다시 켠다(로직보다 연출이 늦게 끝나므로).")]
        [SerializeField] private ArtworkProgressView progressView;

        private bool _returning;
        private int  _artworkElapsed;
        private int  _artworkTotal;
        private string _artworkResult = "";

        GameTimeSystem GameTime => GameCore.Instance?.GameTime;
        ArtworkSystem  Artwork  => GameCore.Instance?.Artwork;

        void Start()
        {
            _returning     = false;
            _artworkResult = "";

            // 복귀 버튼 자동 탐색(같은 오브젝트 → 부모 순) + 평상시 스프라이트/활성화 복원
            if (returnButton == null && returnButtonImage != null)
                returnButton = returnButtonImage.GetComponent<UnityEngine.UI.Button>()
                            ?? returnButtonImage.GetComponentInParent<UnityEngine.UI.Button>();
            if (returnButton == null)
                Debug.LogWarning("[Ground] Return Button 미연결 — 인스펙터에서 복귀 버튼(Button)을 연결하세요. (작품활동 중 비활성화가 안 됩니다)");

            if (returnHover == null && returnButtonImage != null)
                returnHover = returnButtonImage.GetComponent<ButtonHoverHighlight>()
                           ?? returnButtonImage.GetComponentInParent<ButtonHoverHighlight>();

            SetReturnVisual(returnNormalSprite, returnNormalHighlight);
            SetReturnInteractable(true);

            var aw = Artwork;
            if (aw != null)
            {
                aw.ArtworkStarted  += OnArtworkStarted;
                aw.ProgressTicked  += OnProgressTicked;
                aw.ArtworkFinished += OnArtworkFinished;
            }
            else Debug.LogWarning("[Ground] GameCore.Artwork 없음 — 작품활동 이벤트 구독 실패.");

            if (progressView != null) progressView.ProgressClosed += OnProgressClosed;
        }

        void OnDestroy()
        {
            var aw = Artwork;
            if (aw != null)
            {
                aw.ArtworkStarted  -= OnArtworkStarted;
                aw.ProgressTicked  -= OnProgressTicked;
                aw.ArtworkFinished -= OnArtworkFinished;
            }
            if (progressView != null) progressView.ProgressClosed -= OnProgressClosed;
        }

        // 게이지 패널이 완전히 닫혔을 때(연출 끝) 복귀 버튼 재활성화
        void OnProgressClosed() => SetReturnInteractable(true);

        // 작품활동 중에는 승강장 복귀 버튼 비활성화
        void OnArtworkStarted() => SetReturnInteractable(false);

        void SetReturnInteractable(bool on)
        {
            if (returnButton != null) returnButton.interactable = on;
        }

        void OnProgressTicked(int elapsed, int total)
        {
            _artworkElapsed = elapsed;
            _artworkTotal   = total;
        }

        void OnArtworkFinished(bool succeeded, float fameGain, bool interrupted)
        {
            bool failed = interrupted || !succeeded;

            // 버튼 재활성화는 연출이 끝났을 때(OnProgressClosed)에서 처리.
            if (failed)
            {
                // 실패(추격자 도달 등): 결함 그대로 유지(MarkArtworkDone 호출 안 함),
                // 자동 복귀 없음 — 빨강 복귀 버튼을 플레이어가 눌러 복귀(§5-2).
                _artworkResult = "추격자 도달 — 작품 실패!";
                SetReturnVisual(returnFailSprite, returnFailHighlight);
            }
            else
            {
                // 성공: 해당 역 작품완료 기록(쿨타임 → 결함 숨김), 파랑 강조(선택).
                _artworkResult = $"작품 완성 +{fameGain:0.0} 명성";
                GameCore.Instance?.Platform?.MarkArtworkDone();
                SetReturnVisual(returnSuccessSprite, returnSuccessHighlight);
            }
        }

        // 복귀 버튼 본체 스프라이트 + 호버 하이라이트(오버레이) 스프라이트를 함께 교체(상태 일치 유지)
        void SetReturnVisual(Sprite main, Sprite highlight)
        {
            if (returnButtonImage != null && main != null) returnButtonImage.sprite = main;
            if (returnHover != null && highlight != null) returnHover.SetHighlightSprite(highlight);
        }

        // 승강장 복귀 버튼(uGUI)에서 직접 연결: OnClick → ReturnToSubway(false)
        public void OnReturnButton() => ReturnToSubway(false);

        void ReturnToSubway(bool forced)
        {
            if (_returning) return;
            _returning = true;

            Artwork?.CancelArtwork();

            var core = GameCore.Instance;
            if (core == null) return;

            string station = core.Space?.CurrentStationId;

            ScreenFader.Instance?.Fade(onBlack: () =>
            {
                Debug.Log($"[Ground] 복귀({(forced ? "강제" : "자발")}) → 추격자 검문 판정");

                if (core.Platform != null) core.Platform.OpenAt(station);
                else if (core.Space != null) core.Space.EnterPlatform(station);

                if (core.Inspection != null && !string.IsNullOrEmpty(station))
                    core.Inspection.RequestInspection(station);   // 연출 → 끝나면 InspectionView가 판정 적용
            });
        }
    }
}