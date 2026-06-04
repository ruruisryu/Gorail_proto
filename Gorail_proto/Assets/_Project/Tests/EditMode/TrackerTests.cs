using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Subway;
using Game.Gameplay;

namespace Game.Tests
{
    /// <summary>
    /// ⑤⑥ 추격자 로직 테스트 — Tracker의 최단경로 추격(1+2규칙)과
    /// TrackerManager의 체증 스텝 환산(소수 누적)을 자동 검증한다.
    /// </summary>
    public class TrackerTests
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

        // ── Tracker 추격 ─────────────────────────────────────────────────

        [Test]
        public void Chase_OneStep_MovesTowardPlayer()
        {
            var t = new Tracker("E", "L1");
            t.ChaseToward(_g, "A", 1);
            Assert.AreEqual("D", t.StationId);
        }

        [Test]
        public void Chase_MultiStep_AdvancesThatMany()
        {
            var t = new Tracker("E", "L1");
            t.ChaseToward(_g, "A", 3);   // E→D→C→B
            Assert.AreEqual("B", t.StationId);
        }

        [Test]
        public void Chase_StopsAtPlayerStation_NoOvershoot()
        {
            var t = new Tracker("E", "L1");
            t.ChaseToward(_g, "A", 99);  // 거리(4)보다 큰 스텝이어도 A에서 멈춤
            Assert.AreEqual("A", t.StationId);
        }

        [Test]
        public void Chase_AlreadyAtPlayer_DoesNotMove()
        {
            var t = new Tracker("A", "L1");
            t.ChaseToward(_g, "A", 5);
            Assert.AreEqual("A", t.StationId);
        }

        [Test]
        public void Chase_UpdatesLineIdFromTraversedEdge()
        {
            var t = new Tracker("E", "");
            t.ChaseToward(_g, "A", 1);
            Assert.AreEqual("L1", t.LineId);
        }

        // ── 체증 스텝 환산 (§4-2) ────────────────────────────────────────

        [Test]
        public void AdvanceSteps_UnitMultiplier_IsBaseTimesPlayerSteps()
        {
            float debt = 0f;
            int s = TrackerManager.ComputeAdvanceSteps(2f, 1f, 1, ref debt);
            Assert.AreEqual(2, s);
            Assert.AreEqual(0f, debt, 1e-5f);
        }

        [Test]
        public void AdvanceSteps_Congestion_BoostsChase()
        {
            float debt = 0f;
            int s = TrackerManager.ComputeAdvanceSteps(2f, 2f, 1, ref debt); // 2*2 = 4
            Assert.AreEqual(4, s);
        }

        [Test]
        public void AdvanceSteps_FractionalAccumulates_AcrossCalls()
        {
            float debt = 0f;
            int s1 = TrackerManager.ComputeAdvanceSteps(0.5f, 1f, 1, ref debt); // 0.5 → 0
            int s2 = TrackerManager.ComputeAdvanceSteps(0.5f, 1f, 1, ref debt); // 1.0 → 1
            Assert.AreEqual(0, s1);
            Assert.AreEqual(1, s2);
            Assert.AreEqual(0f, debt, 1e-5f);
        }

        [Test]
        public void AdvanceSteps_FractionalCongestion_NoChaseLostOverTime()
        {
            // base 1, mult 1.2를 5번 → 총 6.0 누적 → 정수 스텝 합 6, 잔여 0
            float debt = 0f;
            int total = 0;
            for (int i = 0; i < 5; i++)
                total += TrackerManager.ComputeAdvanceSteps(1f, 1.2f, 1, ref debt);
            Assert.AreEqual(6, total);
            Assert.AreEqual(0f, debt, 1e-4f);
        }
    }
}
