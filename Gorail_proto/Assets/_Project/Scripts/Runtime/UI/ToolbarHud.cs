using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 기타 UI — 우하단 상시 표시 (UI기획서 §2-3).
    /// 인스펙터에서 버튼과 종료 팝업을 직접 연결한다.
    /// 인벤토리(TAB) / 설정(ESC) / 나가기(종료 확인 팝업).
    /// </summary>
    public class ToolbarHud : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button inventoryButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button mapButton;
        [SerializeField] private SubwayMapPopup mapPopup;

        private bool _quitOpen;
        private InputAction _inventoryAction;
        private InputAction _settingsAction;

        void Awake()
        {
            _inventoryAction = new InputAction("Inventory", binding: "<Keyboard>/tab");
            _settingsAction  = new InputAction("Settings",  binding: "<Keyboard>/escape");

            _inventoryAction.performed += _ => OnInventoryClick();
            _settingsAction.performed  += _ => { if (!_quitOpen) OnSettingsClick(); };

            _inventoryAction.Enable();
            _settingsAction.Enable();
        }

        void Start()
        {
            if (inventoryButton != null) inventoryButton.onClick.AddListener(OnInventoryClick);
            if (settingsButton  != null) settingsButton.onClick.AddListener(OnSettingsClick);
            if (quitButton      != null) quitButton.onClick.AddListener(OnQuitClick);
            if (mapButton       != null) mapButton.onClick.AddListener(OnMapClick);
            
            GameCore.Instance.Space.SpaceChanged += OnSpaceChanged;
        }

        void OnDestroy()
        {
            _inventoryAction?.Dispose();
            _settingsAction?.Dispose();
            
            GameCore.Instance.Space.SpaceChanged -= OnSpaceChanged;
        }
        
        void OnSpaceChanged(GameSpace space)
        {
            mapButton.gameObject.SetActive(space != GameSpace.Subway);
        }

        void OnMapClick()
        {
            if (mapPopup == null) return;
            if (mapPopup.gameObject.activeSelf) mapPopup.Hide();
            else mapPopup.Show(2f / 3f, viewOnly: true);
        }

        void OnInventoryClick()
        {
            // TODO: 인벤토리 팝업 열기
            Debug.Log("[ToolbarHud] 인벤토리 열기 (미구현)");
        }

        void OnSettingsClick()
        {
            // TODO: 설정 팝업 열기
            Debug.Log("[ToolbarHud] 설정 열기 (미구현)");
        }

        void OnQuitClick()
        {
            _quitOpen = true;
            ModalDialog.Show("게임을 종료하시겠습니까?",
                ("예", () =>
                {
                    _quitOpen = false;
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }),
                ("아니오", () => { _quitOpen = false; }));
        }
    }
}
