using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 승강장 호선 선택 버튼 프리팹에 붙는 컴포넌트.
    /// PlatformSceneController가 Setup()으로 초기화한다.
    /// </summary>
    public class LineSelectorButtonUI : MonoBehaviour
    {
        [SerializeField] private Image    background;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button   button;

        public void Setup(Color lineColor, string labelText, Action onClick)
        {
            if (background     != null) background.color    = lineColor;
            if (label          != null) label.text          = labelText;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
