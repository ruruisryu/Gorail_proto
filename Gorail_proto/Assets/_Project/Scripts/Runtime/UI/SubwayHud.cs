using UnityEngine;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 지하철 공간 HUD — "하차" 버튼. 노선도에서 역을 찍어 도착하면 자동으로 승강장이 열리지만(§7-1),
    /// 지금 있는 역에서 바로 내리고 싶을 때를 위한 명시적 하차 버튼.
    /// 지하철 공간 + 이동 중이 아닐 때만 표시. IMGUI 버튼 stub(정식 UI는 §13).
    /// </summary>
    public class SubwayHud : MonoBehaviour
    {
        void OnGUI()
        {
            var core = GameCore.Instance;
            if (core == null || core.Space == null) return;
            if (core.Space.Current != Game.Core.Space.Subway) return; // 지하철 공간에서만
            if (core.TurnResolver != null && core.TurnResolver.IsMoving) return; // 이동 중엔 숨김
            if (core.Player == null || string.IsNullOrEmpty(core.Player.CurrentStationId)) return;

            float w = 240f, h = 48f;
            var rect = new Rect((Screen.width - w) / 2f, Screen.height - h - 24f, w, h);
            if (GUI.Button(rect, $"🚉 하차 — {core.Player.CurrentStationId} 승강장"))
                core.Platform?.OpenAt(core.Player.CurrentStationId);
        }
    }
}
