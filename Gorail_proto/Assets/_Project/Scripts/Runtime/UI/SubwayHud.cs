using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 지하철 공간 HUD — "하차" 버튼.
    /// 지하철 공간 + 이동 중이 아닐 때만 표시.
    /// </summary>
    public class SubwayHud : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private Button      disembarkButton;
        [SerializeField] private TMP_Text    label;

        void Start()
        {
            if (disembarkButton != null)
                disembarkButton.onClick.AddListener(OnDisembarkClicked);
            StartCoroutine(LateInit());
        }

        IEnumerator LateInit()
        {
            yield return new WaitUntil(() =>
                GameCore.Instance != null &&
                !string.IsNullOrEmpty(GameCore.Instance.Player?.CurrentStationId));
            SubscribeEvents(true);
            Refresh();
        }

        void OnDestroy() => SubscribeEvents(false);

        void SubscribeEvents(bool subscribe)
        {
            var core = GameCore.Instance;
            if (core == null) return;

            if (core.Space != null)
            {
                if (subscribe) core.Space.SpaceChanged        += OnSpaceChanged;
                else           core.Space.SpaceChanged        -= OnSpaceChanged;
            }
            if (core.TurnResolver != null)
            {
                if (subscribe) { core.TurnResolver.MoveStarted  += Refresh; core.TurnResolver.MoveCompleted += OnMoveCompleted; }
                else           { core.TurnResolver.MoveStarted  -= Refresh; core.TurnResolver.MoveCompleted -= OnMoveCompleted; }
            }
        }

        void OnSpaceChanged(GameSpace _) => Refresh();
        void OnMoveCompleted(string _, bool __) => Refresh();

        void Refresh()
        {
            var core = GameCore.Instance;
            if (disembarkButton == null || core == null) return;

            bool inSubway   = core.Space?.Current == GameSpace.Subway;
            bool isMoving   = core.TurnResolver?.IsMoving ?? false;
            bool hasStation = !string.IsNullOrEmpty(core.Player?.CurrentStationId);

            disembarkButton.gameObject.SetActive(inSubway && !isMoving && hasStation);

            if (label != null && hasStation)
                label.text = $"{core.Player.CurrentStationId} 승강장";
        }

        void OnDisembarkClicked()
        {
            var core = GameCore.Instance;
            if (core == null) return;

            SoundManager.Instance?.PlaySFX("지하철_열림");
            string stationId = core.Player?.CurrentStationId;
            if (string.IsNullOrEmpty(stationId)) return;

            var fader = ScreenFader.Instance;
            if (fader != null)
                fader.Fade(onFadeOut: () =>
                {
                    SoundManager.Instance?.PlaySFX("지하철_닫힘");
                    core.Platform?.OpenAt(stationId);
                });
            else
                core.Platform?.OpenAt(stationId);
        }
    }
}
