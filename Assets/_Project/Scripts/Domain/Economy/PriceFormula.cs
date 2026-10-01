using System;
using AutoService.Domain.Common;

namespace AutoService.Domain.Economy
{
    /// <summary>
    /// Exponential cost curve used for upgrades, hires and other repeatable purchases.
    /// Stateless pure function.
    /// </summary>
    public static class PriceFormula
    {
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
    }
}
