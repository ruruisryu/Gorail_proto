using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Inventory
{
    /// <summary>
    /// 작품활동 화면: 가방(왼쪽)과 재료 배치(오른쪽) 패널을 함께 열고 닫는다.
    /// 위치는 화면 비율(anchor)로 잡아 해상도·화면비율에 안전하다.
    ///
    /// 지상 씬(GroundSceneManager)에서 ArtworkScreen.Instance.Open()으로 연다.
    /// 다른 씬(additive)에서 참조하므로 싱글톤으로 노출(프로젝트의 ScreenFader.Instance와 같은 방식).
    /// </summary>
    public class ArtworkScreen : MonoBehaviour
    {
        public static ArtworkScreen Instance { get; private set; }

        [Header("두 격자 패널")]
        [SerializeField] private InventoryView bagView;        // 가방(작품활동 땐 왼쪽)
        [SerializeField] private InventoryView silhouetteView; // 재료 배치(오른쪽)

        [Header("작품활동 때 가방 가로 위치(화면 비율). 실루엣 위치는 ArtworkView의 Panel Anchor X로.")]
        [Range(0f, 1f)]
        [SerializeField] private float bagArtworkAnchorX = 0.27f;

        [Header("진입 30% 룰")]
        [Tooltip("지상(작품활동) 진입에 필요한 최소 보유 재료 = IP 빈칸 × 이 값.")]
        [Range(0f, 1f)]
        [SerializeField] private float entryMaterialRatio = 0.30f;

        [Header("디버그용 토글 키 (실제 트리거는 지상 씬 버튼)")]
        [SerializeField] private bool useDebugKey = true;
        [SerializeField] private Key debugKey = Key.G;

        private InputAction _toggleAct, _escAct;
        private bool _open;

        public bool IsOpen => _open;

        /// <summary>보유 재료(가방 채운 칸)가 IP 빈칸의 entryMaterialRatio 이상인가 — 진입 30% 룰.</summary>
        public bool HasEnoughMaterials() => HasEnoughMaterials(out _, out _);

        /// <summary>have=보유 칸, need=필요 칸(IP 빈칸×비율). 정보가 없으면 막지 않는다(true).</summary>
        public bool HasEnoughMaterials(out int have, out int need)
        {
            var bag = bagView != null && bagView.Inventory != null ? bagView.Inventory.Grid : null;
            var ip  = silhouetteView != null && silhouetteView.Inventory != null ? silhouetteView.Inventory.Grid : null;
            have = bag != null ? bag.FilledCellCount : 0;
            need = ip  != null ? Mathf.CeilToInt(ip.UsableCellCount * entryMaterialRatio) : 0;
            if (bag == null || ip == null) return true;
            return have >= need;
        }

        void Awake()
        {
            Instance = this;

            if (useDebugKey)
            {
                _toggleAct = new InputAction("ArtworkToggle",
                    binding: $"<Keyboard>/{debugKey.ToString().ToLower()}");
                _toggleAct.performed += _ => Toggle();
                _toggleAct.Enable();
            }

            // Esc로 닫기(작품활동 안 하고 빠져나갈 때)
            _escAct = new InputAction("ArtworkEsc", binding: "<Keyboard>/escape");
            _escAct.performed += _ => { if (_open) Close(); };
            _escAct.Enable();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _toggleAct?.Dispose();
            _escAct?.Dispose();
        }

        public void Toggle() { if (_open) Close(); else Open(); }

        public void Open()
        {
            _open = true;
            if (bagView != null) bagView.SetPanelAnchor(bagArtworkAnchorX); // 가방 왼쪽 비율로
            bagView?.Open();
            silhouetteView?.Open();
        }

        public void Close()
        {
            _open = false;
            bagView?.Close();
            silhouetteView?.Close();
            if (bagView != null) bagView.SetPanelAnchor(0.5f);             // 다음 TAB은 중앙
        }
    }
}