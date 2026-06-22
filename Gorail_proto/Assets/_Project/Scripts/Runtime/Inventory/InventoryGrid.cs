using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// 마스크가 있는 격자 한 판. **가방 인벤토리와 IP 재료 배치가 같은 클래스를 쓴다.**
    /// 둘의 차이는 마스크(usable)뿐 — 가방 윤곽이냐 IP 실루엣이냐.
    /// 점유는 placementId(호출자가 부여하는 고유 번호) 단위로 추적하므로,
    /// UI 레이어는 어떤 아이템(ItemData)이 어떤 placementId인지만 따로 들고 있으면 된다.
    ///
    /// MonoBehaviour가 아닌 순수 C# — EditMode 테스트 대상(Tracker·MapGraph와 동일한 결).
    /// </summary>
    public class InventoryGrid
    {
        public int Width  { get; }
        public int Height { get; }

        private readonly bool[] _usable;    // true = 사용 가능 칸(마스크)
        private readonly int[]  _occupant;  // 점유 placementId, -1 = 빈칸

        /// <summary>마스크상 사용 가능한 전체 칸 수(= 재료 배치의 "전체 칸 수").</summary>
        public int UsableCellCount { get; }

        /// <summary>현재 점유된 칸 수(= "배치된 칸 수").</summary>
        public int FilledCellCount { get; private set; }

        /// <summary>채움 비율 = 점유 칸 / 사용 가능 칸. 완성도(하/중/상) 판정에 사용(외부IP §3-5).</summary>
        public float FillRatio => UsableCellCount > 0 ? (float)FilledCellCount / UsableCellCount : 0f;

        /// <param name="usableMask">행 우선(y*width+x). null이면 전체 칸 사용 가능(직사각형 격자).</param>
        public InventoryGrid(int width, int height, bool[] usableMask = null)
        {
            Width  = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);

            int n = Width * Height;
            _usable   = new bool[n];
            _occupant = new int[n];

            int usable = 0;
            for (int i = 0; i < n; i++)
            {
                _usable[i]   = usableMask == null || (i < usableMask.Length && usableMask[i]);
                _occupant[i] = -1;
                if (_usable[i]) usable++;
            }
            UsableCellCount = usable;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsUsable(int x, int y) => InBounds(x, y) && _usable[y * Width + x];
        public bool IsEmpty (int x, int y) => IsUsable(x, y) && _occupant[y * Width + x] < 0;
        public int  OccupantAt(int x, int y) => InBounds(x, y) ? _occupant[y * Width + x] : -1;

        /// <summary>offsets(정규화된 상대 좌표)를 origin에 놓을 수 있는가 — 마스크 안 + 빈칸일 때만.</summary>
        public bool CanPlace(IReadOnlyList<Vector2Int> offsets, Vector2Int origin)
        {
            if (offsets == null || offsets.Count == 0) return false;
            for (int i = 0; i < offsets.Count; i++)
                if (!IsEmpty(origin.x + offsets[i].x, origin.y + offsets[i].y))
                    return false;
            return true;
        }

        /// <summary>배치 성공 시 true. placementId는 호출자가 부여하는 고유 번호(0 이상).</summary>
        public bool TryPlace(int placementId, IReadOnlyList<Vector2Int> offsets, Vector2Int origin)
        {
            if (placementId < 0 || !CanPlace(offsets, origin)) return false;
            for (int i = 0; i < offsets.Count; i++)
            {
                int idx = (origin.y + offsets[i].y) * Width + (origin.x + offsets[i].x);
                _occupant[idx] = placementId;
            }
            FilledCellCount += offsets.Count;
            return true;
        }

        /// <summary>해당 placementId가 점유한 모든 칸을 비운다.</summary>
        public void Remove(int placementId)
        {
            if (placementId < 0) return;
            for (int i = 0; i < _occupant.Length; i++)
                if (_occupant[i] == placementId)
                {
                    _occupant[i] = -1;
                    FilledCellCount--;
                }
        }

        /// <summary>모든 점유 해제(마스크는 유지).</summary>
        public void Clear()
        {
            for (int i = 0; i < _occupant.Length; i++) _occupant[i] = -1;
            FilledCellCount = 0;
        }
    }
}