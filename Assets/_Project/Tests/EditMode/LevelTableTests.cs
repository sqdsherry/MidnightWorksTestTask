using System;
using AutoService.Domain.Progression;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    public sealed class LevelTableTests
    {
        private LevelTable _table;

        [SetUp]
        public void SetUp()
        {
            _table = new LevelTable(new[] { 0, 15, 35, 70, 110 }, 50);
        }

        [Test]
        public void LevelForXp_ReturnsCorrectLevel()
        {
            Assert.AreEqual(1, _table.LevelForXp(0));
            Assert.AreEqual(1, _table.LevelForXp(14));
            Assert.AreEqual(2, _table.LevelForXp(15));
            Assert.AreEqual(4, _table.LevelForXp(109));
            Assert.AreEqual(5, _table.LevelForXp(110));
            Assert.AreEqual(6, _table.LevelForXp(160));
            Assert.AreEqual(7, _table.LevelForXp(210));
        }

        [Test]
        public void XpForLevel_ReturnsCorrectXp()
        {
            Assert.AreEqual(0, _table.XpForLevel(1));
            Assert.AreEqual(15, _table.XpForLevel(2));
            Assert.AreEqual(110, _table.XpForLevel(5));
            Assert.AreEqual(160, _table.XpForLevel(6));
            Assert.AreEqual(210, _table.XpForLevel(7));
        }

        [Test]
        public void Constructor_EmptyThresholds_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LevelTable(new int[0], 50));
        }

        [Test]
        public void Constructor_NotStartingWithZero_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LevelTable(new[] { 10, 20 }, 50));
        }

        [Test]
        public void Constructor_NotIncreasing_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LevelTable(new[] { 0, 15, 10 }, 50));
            Assert.Throws<ArgumentException>(() => new LevelTable(new[] { 0, 15, 15 }, 50));
        }

        [Test]
        public void Constructor_InvalidStep_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LevelTable(new[] { 0, 15 }, 0));
            Assert.Throws<ArgumentException>(() => new LevelTable(new[] { 0, 15 }, -10));
        }
    }
}
