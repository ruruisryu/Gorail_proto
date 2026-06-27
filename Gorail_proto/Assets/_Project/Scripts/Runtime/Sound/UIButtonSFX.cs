using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core
{
    [RequireComponent(typeof(Button))]
    public class UIButtonSFX : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        private Button _button;
        private void Start()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(()=>SoundManager.Instance.PlaySFX("ui_click"));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_button.interactable)
                SoundManager.Instance.PlaySFX("ui_click_disabled");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SoundManager.Instance.PlaySFX("ui_hover");
        }
    }
}
