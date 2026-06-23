using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// 격자 형태 정의 — 디자이너가 인스펙터에서 칸을 클릭해 칠해 만든다(아이템기획서 §2).
    /// 내부는 width×height bool 격자(행 우선, index = y*width + x).
    /// 배치 로직은 좌표를 직접 다루지 않고 <see cref="OccupiedOffsets"/>로 점유 칸의 상대 좌표만 가져간다.
    /// 좌표계: x→오른쪽, y→아래 (uGUI/화면 좌표와 동일).
    /// MonoBehaviour가 아닌 순수 C# — 회전 규칙은 EditMode 테스트로 검증한다.
    /// </summary>
    [System.Serializable]
    public class GridShape
    {
        [SerializeField, Min(1)] private int width = 1;
        [SerializeField, Min(1)] private int height = 1;

        [Tooltip("행 우선(y*width + x). true = 점유 칸. 커스텀 드로어로 칸을 칠해 편집.")]
        [SerializeField] private bool[] cells = new bool[] { true };

        public int Width => width;
        public int Height => height;

        /// <summary>인스펙터 편집용 기본 생성자.</summary>
        public GridShape() { }

        /// <summary>코드/테스트용 생성자.</summary>
        public GridShape(int width, int height, bool[] cells)
        {
            this.width  = Mathf.Max(1, width);
            this.height = Mathf.Max(1, height);
            this.cells  = cells ?? new bool[this.width * this.height];
        }

        public bool IsOccupied(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            int i = y * width + x;
            return cells != null && i < cells.Length && cells[i];
        }

        /// <summary>점유 칸 수(아이템이 차지하는 격자 칸 수).</summary>
        public int CellCount()
        {
            if (cells == null) return 0;
            int n = 0;
            for (int i = 0; i < cells.Length; i++) if (cells[i]) n++;
            return n;
        }

        /// <summary>
        /// 점유 칸의 상대 좌표 목록을 반환한다. 좌상단이 (0,0)이 되도록 정규화한다.
        /// </summary>
        /// <param name="rotation">시계방향 90도 회전 횟수(0~3). 뒤집기는 없음(외부IP §3-3).</param>
        public List<Vector2Int> OccupiedOffsets(int rotation = 0)
        {
            var list = new List<Vector2Int>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (IsOccupied(x, y))
                        list.Add(new Vector2Int(x, y));

            int r = ((rotation % 4) + 4) % 4;
            for (int k = 0; k < r; k++)
                for (int i = 0; i < list.Count; i++)
                    // y-down 좌표계의 시계방향 90도: (x, y) → (-y, x)
                    list[i] = new Vector2Int(-list[i].y, list[i].x);

            Normalize(list);
            return list;
        }

        /// <summary>min 좌표를 (0,0)으로 당겨 음수 좌표를 없앤다.</summary>
        static void Normalize(List<Vector2Int> list)
        {
            if (list.Count == 0) return;
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var c in list) { if (c.x < minX) minX = c.x; if (c.y < minY) minY = c.y; }
            var off = new Vector2Int(minX, minY);
            for (int i = 0; i < list.Count; i++) list[i] -= off;
        }
    }
}