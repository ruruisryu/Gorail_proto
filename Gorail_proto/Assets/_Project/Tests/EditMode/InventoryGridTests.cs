using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Inventory;

namespace Game.Tests
{
    /// <summary>
    /// 인벤토리 격자 순수 로직 검증(기존 Tracker·MapGraph 테스트와 동일한 EditMode 결).
    /// 배치/겹침/마스크/제거/채움률/회전을 못 박는다.
    /// </summary>
    public class InventoryGridTests
    {
        // 물감: 일자 2칸 (아이템기획서 §2)
        static List<Vector2Int> Bar2() => new() { new(0, 0), new(1, 0) };

        [Test]
        public void Place_updatesFillRatio()
        {
            var grid = new InventoryGrid(5, 5);                 // 25칸 전부 사용
            Assert.IsTrue(grid.TryPlace(1, Bar2(), new Vector2Int(0, 0)));
            Assert.AreEqual(2, grid.FilledCellCount);
            Assert.AreEqual(2f / 25f, grid.FillRatio, 1e-4f);
        }

        [Test]
        public void CannotPlace_onOverlap()
        {
            var grid = new InventoryGrid(5, 5);
            grid.TryPlace(1, Bar2(), new Vector2Int(0, 0));
            Assert.IsFalse(grid.CanPlace(Bar2(), new Vector2Int(0, 0))); // 겹침
            Assert.IsTrue (grid.CanPlace(Bar2(), new Vector2Int(0, 1))); // 아랫줄은 가능
        }

        [Test]
        public void CannotPlace_outsideMask()
        {
            // 2×2에서 좌상단 한 칸만 사용 가능 → 마스크 밖 배치는 거부
            var mask = new bool[] { true, false, false, false };
            var grid = new InventoryGrid(2, 2, mask);
            Assert.AreEqual(1, grid.UsableCellCount);
            Assert.IsFalse(grid.CanPlace(Bar2(), new Vector2Int(0, 0))); // (1,0)이 마스크 밖
        }

        [Test]
        public void Remove_freesCells()
        {
            var grid = new InventoryGrid(5, 5);
            grid.TryPlace(7, Bar2(), new Vector2Int(0, 0));
            grid.Remove(7);
            Assert.AreEqual(0, grid.FilledCellCount);
            Assert.IsTrue(grid.CanPlace(Bar2(), new Vector2Int(0, 0)));
        }

        [Test]
        public void Shape_rotate90CW_horizontalBarBecomesVertical()
        {
            var shape = new GridShape(2, 1, new bool[] { true, true }); // 가로 2칸
            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) },
                shape.OccupiedOffsets(0));
            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) },   // 90° CW → 세로 2칸
                shape.OccupiedOffsets(1));
        }

        [Test]
        public void Shape_cellCount_countsPaintedCells()
        {
            // T형(롤러) 4칸: 윗줄 3칸 + 가운데 아래 1칸
            var t = new GridShape(3, 2, new bool[] { true, true, true, false, true, false });
            Assert.AreEqual(4, t.CellCount());
            Assert.AreEqual(4, t.OccupiedOffsets().Count);
        }
    }
}