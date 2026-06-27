using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// 인벤토리(가방) 상태 관리 부품. 외부 IP역 작품활동에 쓸 아이템을 보유한다.
    /// 프로토타입: 세션 시작 시 자동으로 아이템을 채운다(아이템기획서 §3, 임시).
    ///   → 나중에 상점/특정역 획득으로 바꿀 땐 AutoFill만 교체하면 되고 나머진 그대로.
    /// 이 부품은 "데이터"만 들고 있고, 화면(InventoryView)이 이 상태를 읽어 따로 그린다.
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        [Header("가방 모양 — 칸을 칠해 정의(비워두면 꽉 찬 직사각형으로 동작)")]
        [SerializeField] private GridShape bagShape = new GridShape(6, 8, null); // 기본 6×8

        [Header("자동 채움 아이템(임시) — 넣은 순서대로 가방에 배치")]
        [Tooltip("지금은 세션 시작 시 이 목록으로 채움. 나중에 상점/획득으로 대체.")]
        [SerializeField] private List<ItemData> autoFillItems = new();

        [Header("작품활동 캔버스로 쓸 때만 — IP 실루엣을 격자 모양으로 사용 (가방이면 비워둠)")]
        [Tooltip("설정하면 bagShape 대신 이 IP의 silhouette를 격자 마스크로 쓴다.")]
        [SerializeField] private IpCanvasData canvasData;

        /// <summary>가방 격자(점유/채움률 계산). UI가 이걸 읽어 그린다.</summary>
        public InventoryGrid Grid { get; private set; }

        /// <summary>배치된 아이템 1개의 정보(화면에 그릴 때 필요).</summary>
        public class Placement
        {
            public int        id;        // 격자 점유 식별 번호
            public ItemData   item;      // 어떤 아이템인지
            public Vector2Int origin;    // 좌상단 기준 놓인 칸
            public int        rotation;  // 0~3 (시계 90°)
        }

        private readonly List<Placement> _placements = new();
        public IReadOnlyList<Placement> Placements => _placements;

        /// <summary>인벤토리 내용이 바뀔 때 발생(추가/제거/이동). UI가 구독해 갱신한다.</summary>
        public event System.Action InventoryChanged;

        private int _nextId;

        void Start()
        {
            BuildBag();
            AutoFill();
            Debug.Log($"[Inventory] 가방 {Grid.UsableCellCount}칸 / 아이템 {_placements.Count}개 배치 " +
                      $"(채움 {Grid.FillRatio:P0})");
            InventoryChanged?.Invoke();
        }

        /// <summary>격자를 만든다. canvasData가 있으면 IP 실루엣을, 없으면 가방 모양을 마스크로 쓴다.
        /// 모양을 안 칠했으면 전체 직사각형으로 동작.</summary>
        void BuildBag()
        {
            GridShape shape = canvasData != null ? canvasData.silhouette : bagShape;
            int w = shape.Width, h = shape.Height;
            bool painted = shape.CellCount() > 0;

            var mask = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    mask[y * w + x] = painted ? shape.IsOccupied(x, y) : true;

            Grid = new InventoryGrid(w, h, mask);
            _placements.Clear();
            _nextId = 0;
        }

        void AutoFill()
        {
            foreach (var item in autoFillItems)
                if (item != null) TryAutoPlace(item);
        }

        /// <summary>빈 자리를 위→아래·좌→우로 훑어 처음 들어가는 칸·회전에 배치한다.</summary>
        public bool TryAutoPlace(ItemData item)
        {
            if (item == null || Grid == null) return false;

            for (int y = 0; y < Grid.Height; y++)
                for (int x = 0; x < Grid.Width; x++)
                    for (int rot = 0; rot < 4; rot++)
                    {
                        var offsets = item.shape.OccupiedOffsets(rot);
                        var origin  = new Vector2Int(x, y);
                        if (Grid.TryPlace(_nextId, offsets, origin))
                        {
                            _placements.Add(new Placement
                            {
                                id = _nextId, item = item, origin = origin, rotation = rot
                            });
                            _nextId++;
                            return true;
                        }
                    }

            Debug.LogWarning($"[Inventory] '{item.displayName}' 넣을 자리 없음 " +
                             $"— 가방이 꽉 찼거나 아이템 모양이 안 칠해졌을 수 있음");
            return false;
        }

        // ── 드래그용 조작 (InventoryView가 호출) ─────────────────────

        Placement Find(int id)
        {
            for (int i = 0; i < _placements.Count; i++)
                if (_placements[i].id == id) return _placements[i];
            return null;
        }

        /// <summary>배치 정보 조회.</summary>
        public bool TryGetPlacement(int id, out ItemData item, out Vector2Int origin, out int rotation)
        {
            var p = Find(id);
            if (p == null) { item = null; origin = default; rotation = 0; return false; }
            item = p.item; origin = p.origin; rotation = p.rotation;
            return true;
        }

        /// <summary>아이템을 "손에 든" 상태로 — 격자에서 칸만 비운다(목록엔 남김).</summary>
        public void LiftFromGrid(int id)
        {
            if (Grid == null) return;
            Grid.Remove(id);
            InventoryChanged?.Invoke();
        }

        /// <summary>해당 자리·회전에 놓을 수 있는지(격자는 안 건드림).</summary>
        public bool CanPlaceAt(int id, Vector2Int origin, int rotation)
        {
            var p = Find(id);
            if (p == null || Grid == null) return false;
            return Grid.CanPlace(p.item.shape.OccupiedOffsets(rotation), origin);
        }

        /// <summary>해당 자리·회전에 실제로 놓는다. 성공 시 배치 갱신.</summary>
        public bool TryPlaceAt(int id, Vector2Int origin, int rotation)
        {
            var p = Find(id);
            if (p == null || Grid == null) return false;
            if (!Grid.TryPlace(id, p.item.shape.OccupiedOffsets(rotation), origin)) return false;
            p.origin = origin; p.rotation = rotation;
            InventoryChanged?.Invoke();
            return true;
        }

        // ── 격자 사이 이동(가방 ↔ 재료 배치)용 ──────────────────────

        /// <summary>배치를 통째로 들어낸다(격자 비우고 목록에서 제거). 들어낸 아이템 정보 반환.</summary>
        public bool RemovePlacement(int id, out ItemData item, out Vector2Int origin, out int rotation)
        {
            var p = Find(id);
            if (p == null) { item = null; origin = default; rotation = 0; return false; }
            item = p.item; origin = p.origin; rotation = p.rotation;
            Grid.Remove(id);
            _placements.Remove(p);
            InventoryChanged?.Invoke();
            return true;
        }

        /// <summary>아이템(배치 아님)을 해당 자리·회전에 놓을 수 있는지.</summary>
        public bool CanPlaceItemAt(ItemData item, Vector2Int origin, int rotation)
        {
            if (item == null || Grid == null) return false;
            return Grid.CanPlace(item.shape.OccupiedOffsets(rotation), origin);
        }

        /// <summary>아이템을 새 배치로 추가한다. 성공 시 새 id, 실패 시 -1.</summary>
        public int AddPlacement(ItemData item, Vector2Int origin, int rotation)
        {
            if (item == null || Grid == null) return -1;
            if (!Grid.TryPlace(_nextId, item.shape.OccupiedOffsets(rotation), origin)) return -1;
            _placements.Add(new Placement { id = _nextId, item = item, origin = origin, rotation = rotation });
            int id = _nextId; _nextId++;
            InventoryChanged?.Invoke();
            return id;
        }

        /// <summary>보유 아이템 총 점유 칸 수(진입 차단 30% 룰 등에 사용 예정).</summary>
        public int TotalOccupiedCells => Grid?.FilledCellCount ?? 0;

        /// <summary>현재 작품활동 캔버스(IP 실루엣). 가방이면 null.</summary>
        public IpCanvasData Canvas => canvasData;

        /// <summary>
        /// 작품활동 캔버스(IP 실루엣)를 갈아끼우고 배치를 초기화한다.
        /// 새 IP역에 들어갈 때 호출 — 격자 모양이 새 IP로 바뀌고 이전 배치는 모두 비워진다.
        /// (같은 IP면 호출부에서 걸러 호출하지 않으면 됨.)
        /// </summary>
        public void LoadCanvas(IpCanvasData canvas)
        {
            canvasData = canvas;
            BuildBag();              // 새 실루엣으로 격자 재생성 + _placements/_nextId 초기화
            InventoryChanged?.Invoke();
            Debug.Log($"[Inventory] 캔버스 교체 → {(canvas != null ? canvas.displayName : "없음")} " +
                      $"({Grid.UsableCellCount}칸), 배치 초기화");
        }
    }
}