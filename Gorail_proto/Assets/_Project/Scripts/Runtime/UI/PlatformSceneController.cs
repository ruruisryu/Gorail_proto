using UnityEngine;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// [S5] 승강장 씬 UI(scene_system_spec §2). 네 갈래 버튼을 그리고 역 속성에 따라 활성화한다.
    ///   재탑승·반대방향: 항상 / 환승: 환승역만 / 지상으로: 특별역만.
    /// 버튼 stub 방식(IMGUI) — 별도 Canvas 불필요. 로직은 GameCore.Platform(영속) 호출.
    /// </summary>
    public class PlatformSceneController : MonoBehaviour
    {
        void OnGUI()
        {
            var core = GameCore.Instance;
            if (core == null || core.Platform == null) return;
            var plat = core.Platform;
            var player = core.Player;

            GUILayout.BeginArea(new Rect(Screen.width / 2f - 160f, 70f, 320f, 360f), GUI.skin.box);
            GUILayout.Label($"=== 승강장 @ {plat.CurrentStation} ===");
            if (player != null)
                GUILayout.Label($"노선 {player.CurrentLineId} · 방향 {player.Direction} · 명성 {core.Fame?.CurrentFame:0} · 수배도 {core.Game?.WantedLevel}");

            GUILayout.Space(6);
            if (GUILayout.Button("가던 방향 재탑승")) plat.ContinueForward();
            if (GUILayout.Button("반대방향 탑승"))   plat.ReverseDirection();

            GUILayout.Space(6);
            if (plat.AvailableTransferLines.Count > 0)
            {
                GUILayout.Label("환승 (노선 선택):");
                foreach (var line in plat.AvailableTransferLines)
                    if (GUILayout.Button("→ " + line)) plat.Transfer(line);
            }
            else GUILayout.Label("환승 불가 (환승역 아님)");

            GUILayout.Space(6);
            bool canOut = plat.CanGoOutside;
            GUI.enabled = canOut;
            if (GUILayout.Button(canOut ? "지상으로 (작품활동)" : "지상 불가 (특별역 아님)")) plat.GoOutside();
            GUI.enabled = true;

            GUILayout.EndArea();
        }
    }
}
