using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Game.Inventory
{
    /// <summary>
    /// 격자 패널(가방/재료 배치 공용). 칸을 코드로 그리고, 아이템을 집어 옮긴다.
    /// 들고 있는 상태는 정적(static)이라 **다른 패널로도 옮길 수 있다**(가방 ↔ 재료 배치).
    /// - 좌클릭: 들기 / 놓기   - 우클릭: 90° 회전   - 호버: 초록(가능)/빨강(불가)
    /// 데이터 변경은 전부 InventorySystem에 위임하고, 여긴 읽어 그리기 + 입력만.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InventoryView : MonoBehaviour
    {
        [Header("연결 (비우면 자동으로 찾음)")]
        [SerializeField] private InventorySystem inventory;

        [Header("배경 스프라이트 (없으면 어두운 패널)")]
        [SerializeField] private Sprite bagSprite;

        [Header("그리드 크기 (아이템기획서: 칸 60, 간격 3)")]
        [SerializeField] private float cellSize = 60f;
        [SerializeField] private float cellGap  = 3f;
        [SerializeField] private float bagPadding = 80f;

        [Header("패널 위치/모드")]
        [Tooltip("패널 가로 위치 = 화면 비율(0=왼쪽끝, 0.5=중앙, 1=오른쪽끝). 해상도·화면비율에 안전.")]
        [Range(0f, 1f)]
        [SerializeField] private float panelAnchorX = 0.5f;
        [Tooltip("화면 전체를 덮는 어두운 배경(클릭 시 닫힘). 나란히 띄울 땐 끔.")]
        [SerializeField] private bool showOverlay = true;
        [Tooltip("이 패널이 단축키로 직접 열고 닫히는가. 작품활동 패널은 ArtworkScreen이 제어하므로 끔.")]
        [SerializeField] private bool useToggleKey = true;

        [Header("열기 단축키 (Use Toggle Key가 켜져 있을 때만)")]
        [SerializeField] private Key toggleKey = Key.Tab;

        [Header("색")]
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color cellColor    = new Color(1f, 1f, 1f, 0.10f);
        [SerializeField] private Color cellLine     = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color okColor      = new Color(0.30f, 0.90f, 0.40f, 0.55f);
        [SerializeField] private Color noColor      = new Color(0.90f, 0.30f, 0.30f, 0.55f);

        // 들고 있는 상태는 패널 사이 이동을 위해 정적(공유)
        static bool            s_holding;
        static InventorySystem s_src;        // 집어온 출처
        static ItemData        s_heldItem;
        static int             s_heldRot;
        static Vector2Int      s_origOrigin;
        static int             s_origRot;

        private RectTransform _self, _gridContainer, _bag;
        private Image _bagImg;   // 패널 배경(가방 스프라이트 또는 IP 배경)
        private bool _open;
        private InputAction _toggleAction;
        private Camera _cam;
        private bool _subscribed;
        private readonly List<GameObject> _dynamic = new();

        private Vector2Int _targetOrigin;
        private bool _canPlaceNow;
        private RectTransform _heldImg;
        private readonly List<RectTransform> _hl = new();

        float Step => cellSize + cellGap;

        public bool IsOpen => _open;
        public event System.Action<bool> OpenChanged;
        /// <summary>이 패널이 보고 있는 격자(ArtworkActivity 등이 같은 격자를 읽을 때 사용).</summary>
        public InventorySystem Inventory => inventory;

        void Awake()
        {
            _self = (RectTransform)transform;
            Stretch(_self);
            BuildStaticChrome();
            SetOpen(false);

            if (useToggleKey)
            {
                _toggleAction = new InputAction("InventoryToggle",
                    binding: $"<Keyboard>/{toggleKey.ToString().ToLower()}");
                _toggleAction.performed += _ => Toggle();
                _toggleAction.Enable();
            }
        }

        void OnDestroy()
        {
            _toggleAction?.Dispose();
            if (inventory != null) inventory.InventoryChanged -= Rebuild;
        }

        public void Toggle() => SetOpen(!_open);
        public void Open()   => SetOpen(true);
        public void Close()  => SetOpen(false);

        /// <summary>패널 가로 위치를 런타임에 바꾼다(화면 비율 0~1). 가방: TAB=0.5(중앙), 작품활동=왼쪽.</summary>
        public void SetPanelAnchor(float x01)
        {
            panelAnchorX = x01;
            if (_bag != null) _bag.anchorMin = _bag.anchorMax = new Vector2(x01, 0.5f);
        }

        void SetOpen(bool open)
        {
            if (!open && s_holding && inventory == s_src) // 닫을 때 들고 있던 건 출처로 복귀
            {
                s_src.AddPlacement(s_heldItem, s_origOrigin, s_origRot);
                ClearHeld();
            }
            _open = open;
            foreach (Transform c in transform) c.gameObject.SetActive(open);
            if (open) Rebuild();
            OpenChanged?.Invoke(_open);
        }

        // ── 입력 ────────────────────────────────────────────────
        void Update()
        {
            if (!_open) return;
            ResolveInventory();
            if (inventory?.Grid == null) return;
            var mouse = Mouse.current; if (mouse == null) return;

            bool onGrid = TryCursorCell(out int cx, out int cy);

            if (!s_holding)
            {
                HideHeldPreview();
                if (onGrid && mouse.leftButton.wasPressedThisFrame)
                {
                    int id = inventory.Grid.OccupantAt(cx, cy);
                    if (id >= 0 && inventory.RemovePlacement(id, out var it, out var o, out var r))
                    {
                        s_holding = true; s_src = inventory;
                        s_heldItem = it; s_heldRot = r;
                        s_origOrigin = o; s_origRot = r;
                    }
                }
                return;
            }

            // 들고 있는 중 — 커서가 이 그리드 위일 때만 이 뷰가 관여
            if (!onGrid) { HideHeldPreview(); return; }

            if (mouse.rightButton.wasPressedThisFrame) s_heldRot = (s_heldRot + 1) % 4;

            _targetOrigin = new Vector2Int(cx, cy);
            _canPlaceNow  = inventory.CanPlaceItemAt(s_heldItem, _targetOrigin, s_heldRot);
            ShowHeldPreview();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (_canPlaceNow)
                    inventory.AddPlacement(s_heldItem, _targetOrigin, s_heldRot);  // 이 격자에 놓기(다른 격자여도 OK)
                else
                    s_src.AddPlacement(s_heldItem, s_origOrigin, s_origRot);         // 못 놓으면 출처로 복귀
                ClearHeld();
                HideHeldPreview();
            }
        }

        static void ClearHeld() { s_holding = false; s_src = null; s_heldItem = null; }

        // ── 그리기 ──────────────────────────────────────────────
        void BuildStaticChrome()
        {
            if (showOverlay)
            {
                var overlay = NewUI("Overlay", _self);
                var oimg = overlay.gameObject.AddComponent<Image>();
                oimg.color = overlayColor;
                oimg.raycastTarget = true; // 뒤 배경 클릭은 막되, 클릭해도 닫지는 않음(키로 닫기)
                Stretch(overlay);
            }

            _bag = NewUI("Bag", _self);
            var bag = _bag;
            var bimg = bag.gameObject.AddComponent<Image>();
            bimg.sprite = bagSprite; bimg.preserveAspect = true; bimg.raycastTarget = true;
            bimg.color = bagSprite != null ? Color.white : new Color(0.12f, 0.12f, 0.14f, 0.92f);
            _bagImg = bimg;
            bag.pivot = new Vector2(0.5f, 0.5f);
            bag.anchorMin = bag.anchorMax = new Vector2(panelAnchorX, 0.5f);
            bag.anchoredPosition = Vector2.zero;

            _gridContainer = NewUI("Grid", bag);
            _gridContainer.anchorMin = _gridContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _gridContainer.pivot = new Vector2(0f, 1f);

            _heldImg = NewUI("HeldItem", _gridContainer);
            _heldImg.gameObject.AddComponent<Image>().raycastTarget = false;
            _heldImg.gameObject.SetActive(false);

            for (int i = 0; i < 4; i++)
            {
                var h = NewUI($"HL_{i}", _gridContainer);
                h.gameObject.AddComponent<Image>().raycastTarget = false;
                h.anchorMin = h.anchorMax = new Vector2(0f, 1f);
                h.pivot = new Vector2(0f, 1f);
                h.sizeDelta = new Vector2(cellSize, cellSize);
                h.gameObject.SetActive(false);
                _hl.Add(h);
            }
        }

        void Rebuild()
        {
            ResolveInventory();
            if (inventory?.Grid == null || _gridContainer == null) return;

            foreach (var go in _dynamic) Destroy(go);
            _dynamic.Clear();

            var g = inventory.Grid;
            float gw = g.Width  * cellSize + (g.Width  - 1) * cellGap;
            float gh = g.Height * cellSize + (g.Height - 1) * cellGap;

            _gridContainer.sizeDelta = new Vector2(gw, gh);
            _gridContainer.anchoredPosition = new Vector2(-gw / 2f, gh / 2f);
            var bag = (RectTransform)_gridContainer.parent;
            bag.sizeDelta = new Vector2(gw + bagPadding * 2f, gh + bagPadding * 2f);

            // 실루엣(작품활동) 뷰면 패널 배경을 그 IP 사진으로. 가방 뷰면 bagSprite 유지.
            if (_bagImg != null)
            {
                var bg = inventory.Canvas != null ? inventory.Canvas.background : bagSprite;
                if (bg != null) { _bagImg.sprite = bg; _bagImg.color = Color.white; }
            }

            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    if (!g.IsUsable(x, y)) continue;
                    var cell = NewUI($"Cell_{x}_{y}", _gridContainer);
                    var img = cell.gameObject.AddComponent<Image>();
                    img.color = cellColor; img.raycastTarget = false;
                    cell.anchorMin = cell.anchorMax = new Vector2(0f, 1f);
                    cell.pivot = new Vector2(0f, 1f);
                    cell.sizeDelta = new Vector2(cellSize, cellSize);
                    cell.anchoredPosition = CellTopLeft(x, y);
                    var ol = cell.gameObject.AddComponent<Outline>();
                    ol.effectColor = cellLine; ol.effectDistance = new Vector2(1f, -1f);
                    _dynamic.Add(cell.gameObject);
                }

            foreach (var p in inventory.Placements)
            {
                if (p.item == null) continue;
                var it = NewUI($"Item_{p.id}", _gridContainer);
                it.gameObject.AddComponent<Image>().raycastTarget = false;
                PlaceItemImage(it, p.item, p.origin, p.rotation, p.item.spriteOpacity);
                _dynamic.Add(it.gameObject);
            }

            foreach (var h in _hl) h.SetAsLastSibling();
            _heldImg.SetAsLastSibling();
        }

        void ShowHeldPreview()
        {
            if (s_heldItem == null) { HideHeldPreview(); return; }
            var cells = s_heldItem.shape.OccupiedOffsets(s_heldRot);
            Color c = _canPlaceNow ? okColor : noColor;

            for (int i = 0; i < _hl.Count; i++)
            {
                if (i < cells.Count)
                {
                    _hl[i].gameObject.SetActive(true);
                    _hl[i].anchoredPosition = CellTopLeft(_targetOrigin.x + cells[i].x,
                                                          _targetOrigin.y + cells[i].y);
                    _hl[i].GetComponent<Image>().color = c;
                    _hl[i].SetAsLastSibling();
                }
                else _hl[i].gameObject.SetActive(false);
            }

            _heldImg.gameObject.SetActive(true);
            PlaceItemImage(_heldImg, s_heldItem, _targetOrigin, s_heldRot, 1f);
            _heldImg.SetAsLastSibling();
        }

        void HideHeldPreview()
        {
            if (_heldImg != null) _heldImg.gameObject.SetActive(false);
            foreach (var h in _hl) if (h != null) h.gameObject.SetActive(false);
        }

        void PlaceItemImage(RectTransform it, ItemData item, Vector2Int origin, int rot, float alpha)
        {
            var rotated = item.shape.OccupiedOffsets(rot);
            var baseBox = item.shape.OccupiedOffsets(0);
            Bounds2(rotated, out int bw,  out int bh);
            Bounds2(baseBox, out int bw0, out int bh0);

            float footW = bw  * cellSize + (bw  - 1) * cellGap;
            float footH = bh  * cellSize + (bh  - 1) * cellGap;
            float artW  = bw0 * cellSize + (bw0 - 1) * cellGap;
            float artH  = bh0 * cellSize + (bh0 - 1) * cellGap;

            var img = it.GetComponent<Image>();
            img.sprite = item.sprite;
            img.color = item.sprite == null ? new Color(0.9f, 0.6f, 0.2f, alpha)
                                            : new Color(1f, 1f, 1f, alpha);
            img.preserveAspect = false;

            it.anchorMin = it.anchorMax = new Vector2(0f, 1f);
            it.pivot = new Vector2(0.5f, 0.5f);
            it.sizeDelta = new Vector2(artW, artH);
            it.anchoredPosition = new Vector2(origin.x * Step + footW / 2f,
                                              -(origin.y * Step) - footH / 2f);
            it.localEulerAngles = new Vector3(0f, 0f, -90f * rot);
        }

        // ── 헬퍼 ────────────────────────────────────────────────
        bool TryCursorCell(out int cx, out int cy)
        {
            cx = cy = -1;
            if (_cam == null && _gridContainer != null)
            {
                var root = GetComponentInParent<Canvas>()?.rootCanvas;
                _cam = (root != null && root.renderMode != RenderMode.ScreenSpaceOverlay)
                       ? root.worldCamera : null;
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _gridContainer, Mouse.current.position.ReadValue(), _cam, out var lp))
                return false;

            cx = Mathf.FloorToInt(lp.x / Step);
            cy = Mathf.FloorToInt(-lp.y / Step);
            var g = inventory.Grid;
            return cx >= 0 && cy >= 0 && cx < g.Width && cy < g.Height;
        }

        Vector2 CellTopLeft(int x, int y) => new Vector2(x * Step, -y * Step);

        void ResolveInventory()
        {
            if (inventory == null) inventory = FindFirstObjectByType<InventorySystem>();
            if (inventory != null && !_subscribed)
            {
                inventory.InventoryChanged += Rebuild;
                _subscribed = true;
            }
        }

        /// <summary>런타임에 다른 InventorySystem을 물린다(씬 간 참조용).
        /// OutsideScene의 드래그용 가방 뷰가 영구 가방 데이터(GameCore.BagInventory)를
        /// 가리킬 때 사용 — 인스펙터로는 다른 씬 오브젝트를 참조할 수 없으므로.</summary>
        public void SetInventory(InventorySystem inv)
        {
            if (inventory == inv) { ResolveInventory(); return; }
            if (inventory != null && _subscribed)
            {
                inventory.InventoryChanged -= Rebuild;
                _subscribed = false;
            }
            inventory = inv;
            ResolveInventory();   // 새 대상 구독
            Rebuild();
        }

        static void Bounds2(IReadOnlyList<Vector2Int> cells, out int w, out int h)
        {
            int mx = 0, my = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].x > mx) mx = cells[i].x;
                if (cells[i].y > my) my = cells[i].y;
            }
            w = mx + 1; h = my + 1;
        }

        static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}