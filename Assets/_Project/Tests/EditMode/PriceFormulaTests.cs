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

        [TestCase(0.0, 1.0, 2L)]    // base only
        [TestCase(10.0, 1.0, 7L)]   // 2 + 0.5 * 10
        [TestCase(10.0, 1.5, 11L)]  // (2 + 5) * 1.5 = 10.5 → away from zero
        [TestCase(7.0, 1.0, 6L)]    // 2 + 3.5 = 5.5 → 6
        [TestCase(6.9, 1.0, 5L)]    // 2 + 3.45 = 5.45 → 5
        public void TimeBased_AddsPerSecondPartThenScales(double seconds, double multiplier, long expected)
        {
            Assert.AreEqual(expected, PriceFormula.TimeBased(new Money(2), 0.5, seconds, multiplier).Amount);
        }

        [Test]
        public void TimeBased_GrowsWithTime()
        {
            Money shortStay = PriceFormula.TimeBased(new Money(2), 0.5, 4.0, 1.0);
            Money longStay = PriceFormula.TimeBased(new Money(2), 0.5, 20.0, 1.0);

            Assert.Less(shortStay.Amount, longStay.Amount);
        }

        [Test]
        public void TimeBased_ZeroRate_IsFlatEvenForInfiniteStay()
        {
            Assert.AreEqual(new Money(2), PriceFormula.TimeBased(new Money(2), 0.0, double.PositiveInfinity, 1.0));
        }

        [Test]
        public void TimeBased_ZeroMultiplier_IsFree()
        {
            Assert.AreEqual(Money.Zero, PriceFormula.TimeBased(new Money(2), 0.5, 100.0, 0.0));
        }

        [Test]
        public void TimeBased_HugeStay_SaturatesToMaxValue()
        {
            Assert.AreEqual(Money.MaxValue, PriceFormula.TimeBased(new Money(2), 1.0, double.MaxValue, 2.0));
        }

        [Test]
        public void TimeBased_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceFormula.TimeBased(Money.Zero, -0.1, 1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceFormula.TimeBased(Money.Zero, double.NaN, 1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceFormula.TimeBased(Money.Zero, 0.5, -1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceFormula.TimeBased(Money.Zero, 0.5, double.NaN, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceFormula.TimeBased(Money.Zero, 0.5, 1.0, -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceFormula.TimeBased(Money.Zero, 0.5, 1.0, double.NaN));
        }
    }
}
