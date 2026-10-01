using System;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="PriceFormula"/>.</summary>
    public sealed class PriceFormulaTests
    {
        [Test]
        public void LevelZero_ReturnsBaseCost()
        {
            Assert.AreEqual(new Money(50), PriceFormula.Evaluate(new Money(50), 1.15, 0));
        }

        [TestCase(1, 115L)]
        [TestCase(2, 132L)] // 132.25
        [TestCase(5, 201L)] // 201.13...
        public void Level_GrowsExponentially(int level, long expected)
        {
            Assert.AreEqual(expected, PriceFormula.Evaluate(new Money(100), 1.15, level).Amount);
        }

        [Test]
        public void GrowthOfOne_KeepsCostFlat()
        {
            Assert.AreEqual(new Money(80), PriceFormula.Evaluate(new Money(80), 1.0, 10));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceFormula.Evaluate(new Money(10), 0.99, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceFormula.Evaluate(new Money(10), double.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceFormula.Evaluate(new Money(10), 1.1, -1));
        }

        [Test]
        public void HugeLevel_SaturatesToMaxValue()
        {
            Assert.AreEqual(long.MaxValue, PriceFormula.Evaluate(new Money(10), 2.0, 10_000).Amount);
        }

        [Test]
        public void ZeroBaseCost_StaysZeroEvenAtHugeLevel()
        {
            Assert.AreEqual(Money.Zero, PriceFormula.Evaluate(Money.Zero, 2.0, 10_000));
        }
    }
}
