using NUnit.Framework;
using UnityEngine;
using Game.Data;

namespace Game.Tests
{
    /// <summary>[S1] 명성→수배도 구간 환산(scene_system_spec §4) 정확성 검증.</summary>
    public class SceneConfigTests
    {
        SceneConfig _cfg;

        [SetUp]
        public void Setup()
        {
            _cfg = ScriptableObject.CreateInstance<SceneConfig>();
            _cfg.wantedFameThresholds = new[] { 5f, 25f, 45f, 75f, 200f };
        }

        [Test]
        public void Fame_BelowFirstThreshold_IsLevel0()
        {
            Assert.AreEqual(0, _cfg.WantedLevelForFame(0f));
            Assert.AreEqual(0, _cfg.WantedLevelForFame(4.9f));
        }

        [Test]
        public void Fame_AtThresholds_StepsLevel()
        {
            Assert.AreEqual(1, _cfg.WantedLevelForFame(5f));
            Assert.AreEqual(2, _cfg.WantedLevelForFame(25f));
            Assert.AreEqual(3, _cfg.WantedLevelForFame(45f));
            Assert.AreEqual(4, _cfg.WantedLevelForFame(75f));
            Assert.AreEqual(5, _cfg.WantedLevelForFame(200f));
        }

        [Test]
        public void Fame_BetweenThresholds_HoldsLowerLevel()
        {
            Assert.AreEqual(1, _cfg.WantedLevelForFame(24.9f));
            Assert.AreEqual(2, _cfg.WantedLevelForFame(44.9f));
            Assert.AreEqual(4, _cfg.WantedLevelForFame(199.9f));
        }

        [Test]
        public void Fame_FarAbove_CapsAtMaxLevel()
        {
            Assert.AreEqual(5, _cfg.WantedLevelForFame(99999f));
        }
    }
}
