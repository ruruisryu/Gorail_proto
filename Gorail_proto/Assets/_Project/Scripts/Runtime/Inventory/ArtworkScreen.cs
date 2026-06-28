using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Inventory
{
    /// <summary>
    /// 작품활동 화면: 가방(왼쪽)과 재료 배치(오른쪽) 패널을 함께 열고 닫는다.
    /// 위치는 화면 비율(anchor)로 잡아 해상도·화면비율에 안전하다.
    ///
    /// 지상 씬(OutsideScene)에 두고, 같은 씬의 GroundSceneManager가 직접 참조해
    /// 결함 클릭/버튼으로 Open()한다. 드래그용 가방 뷰는 Open 때 영구 가방 데이터
    /// (GameCore.BagInventory, SubwayScene)에 런타임으로 연결된다.
    /// </summary>
    public class ArtworkScreen : MonoBehaviour
    {
        [Header("팝업 패널 루트 (BG·타이틀·두 격자·완성도·버튼을 담은 한 패널)")]
        [SerializeField] private GameObject panelRoot;

        [Header("두 격자 패널 (위치는 각자의 RectTransform 앵커로 잡는다)")]
        [SerializeField] private InventoryView bagView;        // 가방(왼쪽 영역에 배치)
        [SerializeField] private InventoryView silhouetteView; // 재료 배치(오른쪽 영역에 배치)
        [Tooltip("두 격자에 같은 칸 크기를 박는 컨트롤러(선택).")]
        [SerializeField] private Game.UI.ArtworkGridFitter gridFitter;
        [Tooltip("역에 IP가 없을 때 실루엣 폴백 IP(예: 국중박/NMK). GroundBaseView의 Default Ip와 같게.")]
        [SerializeField] private IpCanvasData defaultIp;

        private InputAction _escAct;
        private bool _open;

        public bool IsOpen => _open;

        void Awake()
        {
            // Esc로 닫기(작품활동 안 하고 빠져나갈 때)
            _escAct = new InputAction("ArtworkEsc", binding: "<Keyboard>/escape");
            _escAct.performed += _ => { if (_open) Close(); };
            _escAct.Enable();
        }

        public void Toggle() { if (_open) Close(); else Open(); }

        public void Open()
        {
            if (bagView == null || silhouetteView == null)
            {
                Debug.LogWarning($"[Artwork] 팝업 뷰 미연결 — bagView={(bagView != null)}, " +
                                 $"silhouetteView={(silhouetteView != null)}. ArtworkScreen 인스펙터에서 두 뷰를 연결하세요.");
            }
            BindBagToPersistent();        // 드래그용 가방 뷰 → 영구 가방 데이터(다른 씬)
            LoadCurrentStationCanvas();   // 현재 역 IP로 실루엣 교체(다른 IP면 배치 초기화)
            _open = true;
            if (panelRoot != null) panelRoot.SetActive(true);
            bagView?.Open();
            silhouetteView?.Open();
            if (gridFitter != null) gridFitter.Apply();   // 두 격자 같은 칸 크기로
            Debug.Log("[Artwork] 작품활동 팝업 Open()");
        }

        /// <summary>OutsideScene의 가방 뷰가 SubwayScene에 영구로 있는 가방 데이터를 가리키게 한다.
        /// 인스펙터로는 씬을 가로질러 참조할 수 없으므로 런타임에 연결.</summary>
        void BindBagToPersistent()
        {
            var core = Game.Core.GameCore.Instance;
            if (bagView != null && core != null && core.BagInventory != null)
                bagView.SetInventory(core.BagInventory);
        }

        /// <summary>현재 역(SpaceManager)의 StationData.ipCanvas를 실루엣에 싣는다.
        /// 다른 IP면 LoadCanvas가 배치를 초기화하고, 같은 IP면 건드리지 않아 배치가 유지된다.
        /// IP 정보가 없으면(디버그로 지하철에서 G 등) 기존 실루엣을 그대로 둔다.</summary>
        private string _lastStationId;
        private bool   _suppressSave;       // 복원/로드 중 저장 피드백 방지
        private bool   _subscribed;
        private Game.Inventory.InventorySystem _watchedInv;

        void LoadCurrentStationCanvas()
        {
            var inv = silhouetteView != null ? silhouetteView.Inventory : null;
            if (inv == null) return;

            string stn = Game.Core.GameCore.Instance?.Space?.CurrentStationId;
            var canvas = ResolveCurrentStationCanvas();
            if (canvas == null) return;        // IP·기본IP 둘 다 없음 — 기존 유지

            _lastStationId = stn;

            // 배치 변경 저장을 잠시 끄고, 마스크 적용 + 저장된 배치 복원
            _suppressSave = true;
            inv.LoadCanvas(canvas);            // IP 마스크 적용(+ 기존 배치 초기화)

            var plat  = Game.Core.GameCore.Instance?.Platform;
            var saved = plat?.GetSilhouette(stn);
            if (saved != null)
                for (int i = 0; i < saved.Count; i++)
                    if (saved[i].item != null)
                        inv.AddPlacement(saved[i].item, saved[i].origin, saved[i].rotation);
            _suppressSave = false;

            // 이 실루엣 인벤토리의 변경을 구독해 역별로 저장(씬마다 인벤토리가 새로 생기므로 재구독)
            if (_watchedInv != inv)
            {
                if (_watchedInv != null) _watchedInv.InventoryChanged -= OnSilhouetteChanged;
                _watchedInv = inv;
                _watchedInv.InventoryChanged += OnSilhouetteChanged;
            }
        }

        void OnSilhouetteChanged()
        {
            if (_suppressSave) return;
            var plat = Game.Core.GameCore.Instance?.Platform;
            var inv  = _watchedInv;
            if (plat == null || inv == null) return;

            var list = new System.Collections.Generic.List<Game.Gameplay.PlatformController.SilhouettePlacement>();
            foreach (var p in inv.Placements)
                if (p.item != null)
                    list.Add(new Game.Gameplay.PlatformController.SilhouettePlacement
                    { item = p.item, origin = p.origin, rotation = p.rotation });

            plat.SaveSilhouette(_lastStationId, list);
        }

        void OnDestroy()
        {
            if (_watchedInv != null) _watchedInv.InventoryChanged -= OnSilhouetteChanged;
        }

        /// <summary>현재 역의 IP 실루엣 SO. 역에 IP가 없으면 기본 IP(국중박)로 폴백.</summary>
        IpCanvasData ResolveCurrentStationCanvas()
        {
            var core = Game.Core.GameCore.Instance;
            string stn = core?.Space?.CurrentStationId;
            if (string.IsNullOrEmpty(stn)) return defaultIp;
            var station = core?.Graph?.Graph?.GetStation(stn);
            var ip = station != null ? station.ipCanvas : null;
            return ip != null ? ip : defaultIp;
        }

        public void Close()
        {
            _open = false;
            bagView?.Close();
            silhouetteView?.Close();
            if (panelRoot != null) panelRoot.SetActive(false);
        }
    }
}