using System;
using System.Collections.Generic;
using AutoService.Domain.Progression;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    public sealed class PlayerProgressTests
    {
        private LevelTable _table;
        private PlayerProgress _progress;

        [SetUp]
        public void SetUp()
        {
            _table = new LevelTable(new[] { 0, 15, 35, 70, 110 }, 50);
            _progress = new PlayerProgress(_table);
        }

        [Test]
        public void AddXp_ToLevel2_TriggersLeveledUpOnce()
        {
            var leveledUpLevels = new List<int>();
            _progress.LeveledUp += (p, level) => leveledUpLevels.Add(level);

            _progress.AddXp(15);

            Assert.AreEqual(2, _progress.Level);
            Assert.AreEqual(1, leveledUpLevels.Count);
            Assert.AreEqual(2, leveledUpLevels[0]);
        }

        [Test]
        public void AddXp_JumpTwoLevels_TriggersLeveledUpTwiceInOrder()
        {
            var leveledUpLevels = new List<int>();
            _progress.LeveledUp += (p, level) => leveledUpLevels.Add(level);

            _progress.AddXp(40); // Level 3 is 35

            Assert.AreEqual(3, _progress.Level);
            Assert.AreEqual(2, leveledUpLevels.Count);
            Assert.AreEqual(2, leveledUpLevels[0]);
            Assert.AreEqual(3, leveledUpLevels[1]);
        }

        [Test]
        public void Restore_SetsValuesWithoutLeveledUpEvent()
        {
            bool leveledUpCalled = false;
            bool changedCalled = false;
            _progress.LeveledUp += (p, l) => leveledUpCalled = true;
            _progress.Changed += (p) => changedCalled = true;

            _progress.Restore(70);

            Assert.AreEqual(70, _progress.Xp);
            Assert.AreEqual(4, _progress.Level);
            Assert.IsFalse(leveledUpCalled);
            Assert.IsTrue(changedCalled);
        }

        [Test]
        public void LevelProgress01_MidLevel_ReturnsHalf()
        {
            // Level 1 -> 2 is 0 -> 15. 7.5 XP is half.
            _progress.Restore(7);
            Assert.AreEqual(7f / 15f, _progress.LevelProgress01, 0.001f);
        }

        [Test]
        public void AddXp_ZeroOrNegative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _progress.AddXp(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _progress.AddXp(-10));
        }
    }
}
