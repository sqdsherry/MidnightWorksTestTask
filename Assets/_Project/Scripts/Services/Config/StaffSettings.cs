using System;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;

namespace AutoService.Services.Config
{
    /// <summary>Immutable location-wide staff settings: the storekeeper offer, how many can be hired and the restocking rule.</summary>
    public sealed class StaffSettings
    {
        /// <summary>Creates and validates the settings.</summary>
        /// <param name="storekeeperTitle">Player-facing title (English).</param>
        /// <param name="storekeeperDescription">Player-facing description of the offer (English).</param>
        /// <param name="storekeeperCost">Hiring price of the first storekeeper (&gt;= 0).</param>
        /// <param name="storekeeperRequiredLevel">Player level needed to hire (&gt;= 0).</param>
        /// <param name="restockAtOrBelow">A storekeeper goes for a box when a point holds this many units or fewer, boxes on their way included (&gt;= 0).</param>
        /// <param name="maxStorekeepers">Storekeepers per location (&gt;= 1).</param>
        /// <param name="storekeeperCostGrowth">Price multiplier per storekeeper already hired (&gt;= 1).</param>
        /// <exception cref="ArgumentException">Thrown for out-of-range values.</exception>
        public StaffSettings(
            string storekeeperTitle,
            string storekeeperDescription,
            Money storekeeperCost,
            int storekeeperRequiredLevel,
            int restockAtOrBelow,
            int maxStorekeepers,
            double storekeeperCostGrowth)
        {
            if (storekeeperCost < Money.Zero)
            {
                throw new ArgumentException("Storekeeper cost must be non-negative, got " + storekeeperCost + ".", nameof(storekeeperCost));
            }

            if (storekeeperRequiredLevel < 0)
            {
                throw new ArgumentException(
                    "Required level must be non-negative, got " + storekeeperRequiredLevel + ".", nameof(storekeeperRequiredLevel));
            }

            if (restockAtOrBelow < 0)
            {
                throw new ArgumentException("Restock level must be non-negative, got " + restockAtOrBelow + ".", nameof(restockAtOrBelow));
            }

            if (maxStorekeepers < 1)
            {
                throw new ArgumentException("At least one storekeeper must be allowed, got " + maxStorekeepers + ".", nameof(maxStorekeepers));
            }

            // Why: the negated comparison also rejects NaN.
            if (!(storekeeperCostGrowth >= 1.0) || double.IsInfinity(storekeeperCostGrowth))
            {
                throw new ArgumentException(
                    "Storekeeper cost growth must be finite and >= 1, got " + storekeeperCostGrowth + ".", nameof(storekeeperCostGrowth));
            }

            StorekeeperTitle = storekeeperTitle ?? string.Empty;
            StorekeeperDescription = storekeeperDescription ?? string.Empty;
            StorekeeperCost = storekeeperCost;
            StorekeeperRequiredLevel = storekeeperRequiredLevel;
            RestockAtOrBelow = restockAtOrBelow;
            MaxStorekeepers = maxStorekeepers;
            StorekeeperCostGrowth = storekeeperCostGrowth;
        }

        /// <summary>Player-facing title of the storekeeper.</summary>
        public string StorekeeperTitle { get; }

        /// <summary>Player-facing description of the storekeeper offer.</summary>
        public string StorekeeperDescription { get; }

        /// <summary>Hiring price of the first storekeeper.</summary>
        public Money StorekeeperCost { get; }

        /// <summary>Player level needed to hire a storekeeper.</summary>
        public int StorekeeperRequiredLevel { get; }

        /// <summary>A point holding this many units or fewer (boxes on their way included) gets restocked.</summary>
        public int RestockAtOrBelow { get; }

        /// <summary>Storekeepers per location.</summary>
        public int MaxStorekeepers { get; }

        /// <summary>Price multiplier per storekeeper already hired.</summary>
        public double StorekeeperCostGrowth { get; }

        /// <summary>Price of the next storekeeper when <paramref name="hired"/> are already hired: cost × growth^hired.</summary>
        public Money StorekeeperCostAt(int hired) => PriceFormula.Evaluate(StorekeeperCost, StorekeeperCostGrowth, Math.Max(0, hired));
    }
}
