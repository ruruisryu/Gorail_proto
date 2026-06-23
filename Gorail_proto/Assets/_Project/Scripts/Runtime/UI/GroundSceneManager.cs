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
                // 추격자가 역에 있으므로 즉시 강제 복귀 → 복귀 후 검문 판정
                ReturnToSubway(true);
            }
            else
            {
                _artworkResult = succeeded
                    ? $"작품 완성 +{fameGain:0.0} 명성"
                    : "작품 실패";
            }
        }

        void OnGUI()
        {
            var core = GameCore.Instance;
            if (core == null) return;

            // [추가] 작품활동 uGUI 화면이 열려 있으면 IMGUI는 그리지 않는다(겹침 방지)
            if (ArtworkScreen.Instance != null && ArtworkScreen.Instance.IsOpen) return;

            var prevC = GUI.color;
            GUI.color = new Color(0.10f, 0.17f, 0.16f, 0.97f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevC;
            GUI.Label(new Rect(0, 24f, Screen.width, 30f), "■ 지상 (Ground)");

            GUILayout.BeginArea(new Rect(Screen.width / 2f - 190f, 70f, 380f, 460f), GUI.skin.box);
            GUILayout.Label($"=== 지상 @ {core.Space?.CurrentStationId} ===");
            GUILayout.Label($"명성 {core.Fame?.CurrentFame:0.0} · 수배도 {core.Wanted?.WantedLevel}");
            GUILayout.Label($"추격자 {core.Trackers?.Trackers.Count ?? 0}명");

            GUILayout.Space(6);

            var aw = Artwork;
            bool inProgress = aw != null && aw.IsActive;

            if (inProgress)
            {
                // 진행 중 표시
                GUILayout.Label($"작업 진행: {_artworkElapsed} / {_artworkTotal} 분");
                float ratio = _artworkTotal > 0 ? (float)_artworkElapsed / _artworkTotal : 0f;
                GUI.color = new Color(0.3f, 0.9f, 0.5f);
                GUILayout.Box(new string('█', Mathf.RoundToInt(ratio * 20)) +
                              new string('░', 20 - Mathf.RoundToInt(ratio * 20)));
                GUI.color = Color.white;
            }
            else
            {
                if (!string.IsNullOrEmpty(_artworkResult))
                    GUILayout.Label(_artworkResult);

                // [변경] 상/중/하 직접 선택 → 재료 배치 화면(완성도로 등급 결정)
                GUILayout.Label("작품활동 — 가방에서 재료를 배치해 완성도를 올리세요:");
                if (GUILayout.Button("작품활동 시작 (재료 배치)"))
                    ArtworkScreen.Instance?.Open();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("지하철로 복귀")) ReturnToSubway(false);

            GUILayout.EndArea();
        }

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