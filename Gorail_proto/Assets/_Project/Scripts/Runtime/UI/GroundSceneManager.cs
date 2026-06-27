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

        private bool _returning;
        private int  _artworkElapsed;
        private int  _artworkTotal;
        private string _artworkResult = "";

        GameTimeSystem GameTime => GameCore.Instance?.GameTime;
        ArtworkSystem  Artwork  => GameCore.Instance?.Artwork;

        void OnEnable()
        {
            _returning     = false;
            _artworkResult = "";

            var aw = Artwork;
            if (aw != null)
            {
                aw.ProgressTicked  += OnProgressTicked;
                aw.ArtworkFinished += OnArtworkFinished;
            }
        }

        void OnDisable()
        {
            var aw = Artwork;
            if (aw != null)
            {
                aw.ProgressTicked  -= OnProgressTicked;
                aw.ArtworkFinished -= OnArtworkFinished;
            }
        }

        void OnProgressTicked(int elapsed, int total)
        {
            _artworkElapsed = elapsed;
            _artworkTotal   = total;
        }

        void OnArtworkFinished(bool succeeded, float fameGain, bool interrupted)
        {
            if (interrupted)
            {
                _artworkResult = "추격자 도달 — 작품 실패!";
                var plat = GameCore.Instance?.Platform;
                plat?.MarkArtworkDone();
                ReturnToSubway(true);
            }
            else
            {
                _artworkResult = succeeded
                    ? $"작품 완성 +{fameGain:0.0} 명성"
                    : "작품 실패";
                // 성공·실패 모두 작품활동을 시도했으므로 나가는 곳 비활성
                var plat = GameCore.Instance?.Platform;
                plat?.MarkArtworkDone();
            }
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
                    core.Inspection.ResolveAt(station);
            });
        }
    }
}