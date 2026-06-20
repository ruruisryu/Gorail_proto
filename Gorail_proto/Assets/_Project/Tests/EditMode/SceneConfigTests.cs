using NUnit.Framework;
using UnityEngine;
using Game.Gameplay;

namespace Game.Tests
{
    /// <summary>[S1] 명성→수배도 구간 환산(scene_system_spec §4) 정확성 검증.</summary>
    public class SceneConfigTests
    {
        WantedSystem _wanted;

        [SetUp]
        public void Setup()
        {
            var go = new GameObject();
            _wanted = go.AddComponent<WantedSystem>();

            // private 직렬화 필드를 리플렉션으로 설정
            var field = typeof(WantedSystem).GetField(
                "wantedFameThresholds",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_wanted, new float[] { 5f, 25f, 45f, 75f, 200f });
        }

        [TearDown]
        public void Teardown() => Object.DestroyImmediate(_wanted.gameObject);

        [Test]
        public void Fame_BelowFirstThreshold_IsLevel0()
        {
            Assert.AreEqual(0, _wanted.WantedLevelForFame(0f));
            Assert.AreEqual(0, _wanted.WantedLevelForFame(4.9f));
        }

        [Test]
        public void Fame_AtThresholds_StepsLevel()
        {
            Assert.AreEqual(1, _wanted.WantedLevelForFame(5f));
            Assert.AreEqual(2, _wanted.WantedLevelForFame(25f));
            Assert.AreEqual(3, _wanted.WantedLevelForFame(45f));
            Assert.AreEqual(4, _wanted.WantedLevelForFame(75f));
            Assert.AreEqual(5, _wanted.WantedLevelForFame(200f));
        }

        [Test]
        public void Fame_BetweenThresholds_HoldsLowerLevel()
        {
            Assert.AreEqual(1, _wanted.WantedLevelForFame(24.9f));
            Assert.AreEqual(2, _wanted.WantedLevelForFame(44.9f));
            Assert.AreEqual(4, _wanted.WantedLevelForFame(199.9f));
        }

        [Test]
        public void Fame_FarAbove_CapsAtMaxLevel()
        {
            Assert.AreEqual(5, _wanted.WantedLevelForFame(99999f));
        }
    }
}
