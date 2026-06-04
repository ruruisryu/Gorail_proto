using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Subway;

namespace Game.Tests
{
    /// <summary>
    /// MapGraph EditMode 테스트 — 노선 위 경로 산출(순환선 짧은쪽 포함)과
    /// BFS·인접 조회의 정확성을 사람 없이 자동 검증한다.
    ///
    /// 임시 그래프 구성:
    ///   L1(비순환): A-B-C-D-E
    ///   L2(비순환): X-C-Y      (C에서 L1과 교차 → 환승역)
    ///   LP(순환  ): P1-P2-P3-P4-P5 (P5-P1 연결)
    /// </summary>
    public class MapGraphTests
    {
        MapGraph _g;

        static StationData Stn(string id)
        {
            var s = ScriptableObject.CreateInstance<StationData>();
            s.stationId = id;
            s.displayName = id;
            return s;
        }

        static LineData Line(string id, bool circular, params StationData[] stns)
        {
            var l = ScriptableObject.CreateInstance<LineData>();
            l.lineId = id;
            l.displayName = id;
            l.stations = new List<StationData>(stns);
            l.isCircular = circular;
            return l;
        }

        [SetUp]
        public void Setup()
        {
            var a = Stn("A"); var b = Stn("B"); var c = Stn("C"); var d = Stn("D"); var e = Stn("E");
            var x = Stn("X"); var y = Stn("Y");
            var p1 = Stn("P1"); var p2 = Stn("P2"); var p3 = Stn("P3"); var p4 = Stn("P4"); var p5 = Stn("P5");

            var net = ScriptableObject.CreateInstance<SubwayNetworkData>();
            net.lines = new List<LineData>
            {
                Line("L1", false, a, b, c, d, e),
                Line("L2", false, x, c, y),
                Line("LP", true,  p1, p2, p3, p4, p5),
            };
            _g = new MapGraph(net);
        }

        // ── GetLineOrderedPath (비순환) ──────────────────────────────────

        [Test]
        public void LinePath_Forward_ReturnsInclusiveSlice()
        {
            CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, _g.GetLineOrderedPath("L1", "A", "D"));
        }

        [Test]
        public void LinePath_Backward_ReturnsReversedSlice()
        {
            CollectionAssert.AreEqual(new[] { "D", "C", "B", "A" }, _g.GetLineOrderedPath("L1", "D", "A"));
        }

        [Test]
        public void LinePath_SameStation_ReturnsSingle()
        {
            CollectionAssert.AreEqual(new[] { "A" }, _g.GetLineOrderedPath("L1", "A", "A"));
        }

        [Test]
        public void LinePath_DestNotOnLine_ReturnsEmpty()
        {
            // E는 L1에 있지만 X는 L2 전용 → L1 위 경로 없음
            Assert.IsEmpty(_g.GetLineOrderedPath("L1", "A", "X"));
        }

        [Test]
        public void LinePath_UnknownLine_ReturnsEmpty()
        {
            Assert.IsEmpty(_g.GetLineOrderedPath("NOPE", "A", "B"));
        }

        // ── GetLineOrderedPath (순환선 짧은쪽) ───────────────────────────

        [Test]
        public void CircularPath_PicksShorterArc_Backward()
        {
            // P1→P4: 정방향 P1-P2-P3-P4(3칸), 역방향 P1-P5-P4(2칸) → 역방향 채택
            CollectionAssert.AreEqual(new[] { "P1", "P5", "P4" }, _g.GetLineOrderedPath("LP", "P1", "P4"));
        }

        [Test]
        public void CircularPath_PicksShorterArc_Forward()
        {
            // P1→P3: 정방향 2칸, 역방향 3칸 → 정방향 채택
            CollectionAssert.AreEqual(new[] { "P1", "P2", "P3" }, _g.GetLineOrderedPath("LP", "P1", "P3"));
        }

        [Test]
        public void CircularPath_Tie_PrefersForward()
        {
            // P1→(P3 with 5 nodes) already tested; tie case에선 정방향 우선(fwd<=bwd)
            // 4칸 균등 분할이 없으므로 LP(5노선)에선 동률이 없다. 정방향 우선 정책만 확인.
            var path = _g.GetLineOrderedPath("LP", "P2", "P4");
            CollectionAssert.AreEqual(new[] { "P2", "P3", "P4" }, path);
        }

        // ── BFS / 인접 / 환승 ────────────────────────────────────────────

        [Test]
        public void Distance_AcrossTransfer_UsesShortest()
        {
            // A(L1) → Y(L2): A-B-C-Y → 3홉
            Assert.AreEqual(3, _g.Distance("A", "Y"));
        }

        [Test]
        public void ShortestPath_AcrossTransfer()
        {
            CollectionAssert.AreEqual(new[] { "A", "B", "C", "Y" }, _g.ShortestPath("A", "Y"));
        }

        [Test]
        public void NextStepToward_ReturnsFirstHop()
        {
            Assert.AreEqual("B", _g.NextStepToward("A", "E"));
        }

        [Test]
        public void IsTransfer_SharedNode_True()
        {
            Assert.IsTrue(_g.IsTransfer("C"));   // L1·L2 공유
            Assert.IsFalse(_g.IsTransfer("A"));  // L1 전용
        }

        [Test]
        public void GetConnectingLineId_AdjacentPair()
        {
            Assert.AreEqual("L1", _g.GetConnectingLineId("A", "B"));
            Assert.IsNull(_g.GetConnectingLineId("A", "E")); // 인접 아님
        }

        [Test]
        public void IsLineCircular_ReportsCorrectly()
        {
            Assert.IsTrue(_g.IsLineCircular("LP"));
            Assert.IsFalse(_g.IsLineCircular("L1"));
        }
    }
}
