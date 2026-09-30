using System;
using AutoService.Domain.Common;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="Money"/>.</summary>
    public sealed class MoneyTests
    {
        [Test]
        public void Constructor_NegativeAmount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-1));
        }

        [Test]
        public void Zero_HasZeroAmount()
        {
            Assert.AreEqual(0L, Money.Zero.Amount);
        }

        [Test]
        public void Addition_SumsAmounts()
        {
            Assert.AreEqual(new Money(150), new Money(100) + new Money(50));
        }

        [Test]
        public void Addition_Overflow_SaturatesToMaxValue()
        {
            Money result = new Money(long.MaxValue - 1) + new Money(10);

            Assert.AreEqual(long.MaxValue, result.Amount);
        }

        [Test]
        public void Subtraction_SubtractsAmounts()
        {
            Assert.AreEqual(new Money(30), new Money(100) - new Money(70));
        }

        [Test]
        public void Subtraction_ToExactlyZero_IsAllowed()
        {
            Assert.AreEqual(Money.Zero, new Money(5) - new Money(5));
        }

        [Test]
        public void Subtraction_BelowZero_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => { Money _ = new Money(5) - new Money(6); });
        }

        [TestCase(100L, 1.5, 150L)]
        [TestCase(3L, 0.5, 2L)]   // 1.5 -> 2 (midpoint away from zero)
        [TestCase(5L, 0.5, 3L)]   // 2.5 -> 3 (not banker's rounding to 2)
        [TestCase(10L, 0.24, 2L)] // 2.4 -> 2
        [TestCase(10L, 0.0, 0L)]
        public void Multiplication_RoundsAwayFromZero(long amount, double multiplier, long expected)
        {
            Assert.AreEqual(expected, (new Money(amount) * multiplier).Amount);
        }

        [Test]
        public void Multiplication_Overflow_SaturatesToMaxValue()
        {
            Assert.AreEqual(long.MaxValue, (new Money(long.MaxValue / 2) * 3.0).Amount);
            Assert.AreEqual(long.MaxValue, (new Money(1) * double.PositiveInfinity).Amount);
        }

        [Test]
        public void Multiplication_ZeroByInfinity_IsZero()
        {
            Assert.AreEqual(Money.Zero, Money.Zero * double.PositiveInfinity);
        }

        [Test]
        public void Multiplication_NegativeOrNaN_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => { Money _ = new Money(10) * -0.1; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { Money _ = new Money(10) * double.NaN; });
        }

        [Test]
        public void Comparisons_FollowAmounts()
        {
            var small = new Money(1);
            var big = new Money(2);

            Assert.IsTrue(small < big);
            Assert.IsTrue(big > small);
            Assert.IsTrue(small <= new Money(1));
            Assert.IsTrue(big >= new Money(2));
            Assert.IsFalse(small > big);
            Assert.Less(small.CompareTo(big), 0);
            Assert.AreEqual(0, big.CompareTo(new Money(2)));
        }

        [Test]
        public void Equality_IsByValue()
        {
            Assert.IsTrue(new Money(7) == new Money(7));
            Assert.IsTrue(new Money(7) != new Money(8));
            Assert.IsTrue(new Money(7).Equals(new Money(7)));
            Assert.IsTrue(new Money(7).Equals((object)new Money(7)));
            Assert.IsFalse(new Money(7).Equals((object)7L));
            Assert.AreEqual(new Money(7).GetHashCode(), new Money(7).GetHashCode());
        }

        [Test]
        public void ToString_IsRawInvariantAmount()
        {
            Assert.AreEqual("1234567", new Money(1234567).ToString());
        }
    }
}
