using System;
using AutoService.Domain.Common;

namespace AutoService.Domain.Economy
{
    /// <summary>
    /// Price formulas: the exponential cost curve used for upgrades, hires and other repeatable purchases,
    /// and the time-based fee of the parking entrances. Stateless pure functions.
    /// </summary>
    public static class PriceFormula
    {
        private static readonly Money OneDollar = new Money(1L);

        /// <summary>
        /// price = (basePrice + pricePerSecond * seconds) * multiplier, rounded to whole dollars (away from zero),
        /// saturated to <see cref="long.MaxValue"/> — the same rounding as <c>Money * double</c>.
        /// </summary>
        /// <param name="basePrice">Fixed part of the fee.</param>
        /// <param name="pricePerSecond">Dollars per second of stay; must be &gt;= 0.</param>
        /// <param name="seconds">Duration of the stay; must be &gt;= 0.</param>
        /// <param name="multiplier">Car type price multiplier; must be &gt;= 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a numeric argument is negative or NaN.</exception>
        public static Money TimeBased(Money basePrice, double pricePerSecond, double seconds, double multiplier)
        {
            RequireNonNegative(pricePerSecond, nameof(pricePerSecond));
            RequireNonNegative(seconds, nameof(seconds));
            RequireNonNegative(multiplier, nameof(multiplier));

            // Why: 0 * Infinity is NaN; a zero rate or multiplier must stay a clean zero term instead.
            double timePart = pricePerSecond > 0.0 ? pricePerSecond * seconds : 0.0;
            if (multiplier == 0.0)
            {
                return Money.Zero;
            }

            // Why: scaling one dollar reuses Money's rounding and overflow saturation instead of duplicating them.
            return OneDollar * ((basePrice.Amount + timePart) * multiplier);
        }

        /// <summary>
        /// cost = baseCost * growth^level, rounded to whole dollars (away from zero), saturated to <see cref="long.MaxValue"/>.
        /// </summary>
        /// <param name="baseCost">Cost at level 0.</param>
        /// <param name="growth">Multiplier per level; must be &gt;= 1 so prices never decrease.</param>
        /// <param name="level">Current level; must be &gt;= 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="growth"/> &lt; 1 (or NaN) or <paramref name="level"/> &lt; 0.</exception>
        public static Money Evaluate(Money baseCost, double growth, int level)
        {
            // Why: the negated comparison also rejects NaN.
            if (!(growth >= 1.0))
            {
                throw new ArgumentOutOfRangeException(nameof(growth), growth, "Growth must be >= 1.");
            }

            if (level < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Level must be >= 0.");
            }

            // Why: Math.Pow may return +Infinity for huge levels; Money's multiplication saturates it to MaxValue.
            return baseCost * Math.Pow(growth, level);
        }

        private static void RequireNonNegative(double value, string parameterName)
        {
            // Why: the negated comparison also rejects NaN.
            if (!(value >= 0.0))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be non-negative.");
            }
        }
    }
}
