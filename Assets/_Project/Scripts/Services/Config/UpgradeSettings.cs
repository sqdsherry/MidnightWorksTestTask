using System;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Domain.Upgrades;

namespace AutoService.Services.Config
{
    /// <summary>
    /// Immutable settings of one point upgrade: texts, the cost curve and the effect formula.
    /// </summary>
    /// <remarks>
    /// Cost of the next level = <see cref="PriceFormula.Evaluate"/>(base cost, growth, current level).
    /// Speed: service time × (1 − effect)^level — e.g. 0.9^10 ≈ 0.35, it never reaches 0.
    /// Price: price × (1 + effect × level).
    /// </remarks>
    public sealed class UpgradeSettings
    {
        /// <summary>Creates and validates the settings.</summary>
        /// <param name="kind">What the upgrade improves.</param>
        /// <param name="displayName">Player-facing name (English).</param>
        /// <param name="effectFormat">Player-facing effect per level (English), e.g. "−10% service time".</param>
        /// <param name="baseCost">Cost of the first level.</param>
        /// <param name="growth">Cost multiplier per level (&gt;= 1).</param>
        /// <param name="maxLevel">Highest level (&gt;= 0).</param>
        /// <param name="effectPerLevel">Effect per level: speed 0..1 (exclusive), price &gt;= 0.</param>
        /// <param name="requiredLevel">Player level needed to buy any level (&gt;= 0).</param>
        /// <exception cref="ArgumentException">Thrown for out-of-range values.</exception>
        public UpgradeSettings(
            UpgradeKind kind,
            string displayName,
            string effectFormat,
            Money baseCost,
            double growth,
            int maxLevel,
            double effectPerLevel,
            int requiredLevel)
        {
            if (baseCost < Money.Zero)
            {
                throw new ArgumentException("Base cost must be non-negative, got " + baseCost + ".", nameof(baseCost));
            }

            // Why: the negated comparisons also reject NaN.
            if (!(growth >= 1.0) || double.IsInfinity(growth))
            {
                throw new ArgumentException("Growth must be finite and >= 1, got " + growth + ".", nameof(growth));
            }

            if (maxLevel < 0)
            {
                throw new ArgumentException("Max level must be non-negative, got " + maxLevel + ".", nameof(maxLevel));
            }

            bool validEffect = kind == UpgradeKind.Speed
                ? effectPerLevel >= 0.0 && effectPerLevel < 1.0
                : effectPerLevel >= 0.0 && !double.IsInfinity(effectPerLevel);
            if (!validEffect)
            {
                throw new ArgumentException(
                    "Effect per level of " + kind + " is out of range, got " + effectPerLevel + " (speed: 0 <= e < 1, price: e >= 0).",
                    nameof(effectPerLevel));
            }

            if (requiredLevel < 0)
            {
                throw new ArgumentException("Required level must be non-negative, got " + requiredLevel + ".", nameof(requiredLevel));
            }

            Kind = kind;
            DisplayName = displayName ?? string.Empty;
            EffectFormat = effectFormat ?? string.Empty;
            BaseCost = baseCost;
            Growth = growth;
            MaxLevel = maxLevel;
            EffectPerLevel = effectPerLevel;
            RequiredLevel = requiredLevel;
        }

        /// <summary>What the upgrade improves.</summary>
        public UpgradeKind Kind { get; }

        /// <summary>Player-facing name.</summary>
        public string DisplayName { get; }

        /// <summary>Player-facing effect per level.</summary>
        public string EffectFormat { get; }

        /// <summary>Cost of the first level.</summary>
        public Money BaseCost { get; }

        /// <summary>Cost multiplier per level.</summary>
        public double Growth { get; }

        /// <summary>Highest level.</summary>
        public int MaxLevel { get; }

        /// <summary>Effect per level.</summary>
        public double EffectPerLevel { get; }

        /// <summary>Player level needed to buy any level.</summary>
        public int RequiredLevel { get; }

        /// <summary>Price of going from <paramref name="currentLevel"/> to the next level.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative level.</exception>
        public Money CostAt(int currentLevel) => PriceFormula.Evaluate(BaseCost, Growth, currentLevel);

        /// <summary>Service time multiplier at <paramref name="level"/> (1 for a price upgrade).</summary>
        public double DurationMultiplierAt(int level) =>
            Kind == UpgradeKind.Speed ? Math.Pow(1.0 - EffectPerLevel, Math.Max(0, level)) : 1.0;

        /// <summary>Order price multiplier at <paramref name="level"/> (1 for a speed upgrade).</summary>
        public double PriceMultiplierAt(int level) =>
            Kind == UpgradeKind.Price ? 1.0 + EffectPerLevel * Math.Max(0, level) : 1.0;
    }
}
