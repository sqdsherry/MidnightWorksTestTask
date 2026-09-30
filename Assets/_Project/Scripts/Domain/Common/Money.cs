using System;
using System.Globalization;

namespace AutoService.Domain.Common
{
    /// <summary>
    /// Immutable amount of in-game currency in whole dollars.
    /// Invariant: the amount is never negative.
    /// </summary>
    /// <remarks>
    /// Addition and multiplication saturate at <see cref="long.MaxValue"/> instead of overflowing:
    /// an idle game can grow numbers indefinitely and a wrapped-around negative balance would be far worse than a capped one.
    /// Subtraction below zero is a programming error (callers must check affordability first) and throws.
    /// </remarks>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        /// <summary>Zero dollars.</summary>
        public static readonly Money Zero = new Money(0L);

        /// <summary>Maximum representable amount; results of saturating operations never exceed it.</summary>
        public static readonly Money MaxValue = new Money(long.MaxValue);

        // Why: long.MaxValue is not exactly representable as double; (double)long.MaxValue rounds up to 2^63,
        // so any double >= this bound would overflow a cast back to long.
        private const double LongOverflowBound = 9223372036854775808.0;

        /// <summary>Creates an amount of money.</summary>
        /// <param name="amount">Non-negative amount in whole dollars.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is negative.</exception>
        public Money(long amount)
        {
            if (amount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Money cannot be negative.");
            }

            Amount = amount;
        }

        /// <summary>Amount in whole dollars (always &gt;= 0).</summary>
        public long Amount { get; }

        /// <summary>Adds two amounts, saturating at <see cref="long.MaxValue"/> on overflow.</summary>
        public static Money operator +(Money left, Money right)
        {
            try
            {
                return new Money(checked(left.Amount + right.Amount));
            }
            catch (OverflowException)
            {
                return MaxValue;
            }
        }

        /// <summary>Subtracts <paramref name="right"/> from <paramref name="left"/>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the result would be negative.</exception>
        public static Money operator -(Money left, Money right)
        {
            if (right.Amount > left.Amount)
            {
                throw new InvalidOperationException(
                    "Cannot subtract " + right.ToString() + " from " + left.ToString() + ": result would be negative.");
            }

            return new Money(left.Amount - right.Amount);
        }

        /// <summary>
        /// Scales an amount by a non-negative multiplier. The result is rounded to whole dollars
        /// (<see cref="MidpointRounding.AwayFromZero"/>) and saturated at <see cref="long.MaxValue"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="multiplier"/> is negative or NaN.</exception>
        public static Money operator *(Money money, double multiplier)
        {
            // Why: the negated comparison also rejects NaN, which would otherwise silently produce garbage.
            if (!(multiplier >= 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "Multiplier must be non-negative.");
            }

            if (money.Amount == 0L)
            {
                // Why: 0 * +Infinity is NaN; zero money stays zero regardless of the multiplier.
                return Zero;
            }

            double scaled = Math.Round(money.Amount * multiplier, MidpointRounding.AwayFromZero);
            if (scaled >= LongOverflowBound)
            {
                return MaxValue;
            }

            return new Money((long)scaled);
        }

        /// <summary>Value equality.</summary>
        public static bool operator ==(Money left, Money right) => left.Amount == right.Amount;

        /// <summary>Value inequality.</summary>
        public static bool operator !=(Money left, Money right) => left.Amount != right.Amount;

        /// <summary>Strictly less than.</summary>
        public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

        /// <summary>Strictly greater than.</summary>
        public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

        /// <summary>Less than or equal.</summary>
        public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

        /// <summary>Greater than or equal.</summary>
        public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

        /// <inheritdoc />
        public bool Equals(Money other) => Amount == other.Amount;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Money other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Amount.GetHashCode();

        /// <inheritdoc />
        public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

        /// <summary>
        /// Raw amount in invariant culture, for debugging and logs only.
        /// Player-facing text must go through <c>MoneyFormatter</c>.
        /// </summary>
        public override string ToString() => Amount.ToString(CultureInfo.InvariantCulture);
    }
}
