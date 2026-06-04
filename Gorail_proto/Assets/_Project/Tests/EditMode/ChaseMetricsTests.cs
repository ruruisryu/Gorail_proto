using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Subway;
using Game.Gameplay;

namespace Game.Tests
{
    /// <summary>
    /// [버퍼 H1] ChaseMetrics 테스트 — 최근접 추격자/거리/노선별 집계가 정확한지 검증.
    /// </summary>
    public class ChaseMetricsTests
    {
        MapGraph _g;

        static StationData Stn(string id)
        {
            var s = ScriptableObject.CreateInstance<StationData>();
            s.stationId = id; s.displayName = id; return s;
        }

        static LineData Line(string id, bool circular, params StationData[] stns)
        {
            var l = ScriptableObject.CreateInstance<LineData>();
            l.lineId = id; l.displayName = id;
            l.stations = new List<StationData>(stns); l.isCircular = circular; return l;
        }

        [SetUp]
        public void Setup()
        {
            var a = Stn("A"); var b = Stn("B"); var c = Stn("C"); var d = Stn("D"); var e = Stn("E");
            var net = ScriptableObject.CreateInstance<SubwayNetworkData>();
            net.lines = new List<LineData> { Line("L1", false, a, b, c, d, e) };
            _g = new MapGraph(net);
        }

        [Test]
        public void NearestDistance_PicksClosest()
        {
            var trackers = new List<Tracker> { new Tracker("E", "L1"), new Tracker("C", "L1") };
            // 플레이어 A 기준: E=4, C=2 → 최근접 2
            Assert.AreEqual(2, ChaseMetrics.NearestDistance(_g, trackers, "A"));
        }

        [Test]
        public void NearestTracker_ReturnsClosestInstance()
        {
            var near = new Tracker("B", "L1");
            var trackers = new List<Tracker> { new Tracker("E", "L1"), near };
            Assert.AreSame(near, ChaseMetrics.NearestTracker(_g, trackers, "A"));
        }

        [Test]
        public void NearestDistance_NoTrackers_IsMaxValue()
        {
            Assert.AreEqual(int.MaxValue, ChaseMetrics.NearestDistance(_g, new List<Tracker>(), "A"));
        }

        [Test]
        public void CountPerLine_Aggregates()
        {
            var trackers = new List<Tracker>
            {
                new Tracker("A", "L1"), new Tracker("B", "L1"), new Tracker("C", "L2"),
            };
            var counts = ChaseMetrics.CountPerLine(trackers);
            Assert.AreEqual(2, counts["L1"]);
            Assert.AreEqual(1, counts["L2"]);
        }

        [Test]
        public void AnyAtPlayer_DetectsSameStation()
        {
            var trackers = new List<Tracker> { new Tracker("D", "L1") };
            Assert.IsTrue(ChaseMetrics.AnyAtPlayer(trackers, "D"));
            Assert.IsFalse(ChaseMetrics.AnyAtPlayer(trackers, "A"));
        }
    }
}
