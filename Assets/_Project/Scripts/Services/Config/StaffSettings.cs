using System;
using AutoService.Domain.Common;

namespace AutoService.Services.Config
{
    /// <summary>Immutable location-wide staff settings: the storekeeper offer and its restocking rule.</summary>
    public sealed class StaffSettings
    {
        /// <summary>Creates and validates the settings.</summary>
        /// <param name="storekeeperTitle">Player-facing title (English).</param>
        /// <param name="storekeeperDescription">Player-facing description of the offer (English).</param>
        /// <param name="storekeeperCost">One-time hiring price (&gt;= 0).</param>
        /// <param name="storekeeperRequiredLevel">Player level needed to hire (&gt;= 0).</param>
        /// <param name="restockThreshold">The storekeeper goes for a box when a point's fill drops below this (0..1).</param>
        /// <exception cref="ArgumentException">Thrown for a negative cost or level, or a threshold outside 0..1.</exception>
        public StaffSettings(
            string storekeeperTitle,
            string storekeeperDescription,
            Money storekeeperCost,
            int storekeeperRequiredLevel,
            float restockThreshold)
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

            // Why: the negated comparison also rejects NaN.
            if (!(restockThreshold >= 0f && restockThreshold <= 1f))
            {
                throw new ArgumentException("Restock threshold must be within 0..1, got " + restockThreshold + ".", nameof(restockThreshold));
            }

            StorekeeperTitle = storekeeperTitle ?? string.Empty;
            StorekeeperDescription = storekeeperDescription ?? string.Empty;
            StorekeeperCost = storekeeperCost;
            StorekeeperRequiredLevel = storekeeperRequiredLevel;
            RestockThreshold = restockThreshold;
        }

        /// <summary>Player-facing title of the storekeeper.</summary>
        public string StorekeeperTitle { get; }

        /// <summary>Player-facing description of the storekeeper offer.</summary>
        public string StorekeeperDescription { get; }

        /// <summary>One-time hiring price of the storekeeper.</summary>
        public Money StorekeeperCost { get; }

        /// <summary>Player level needed to hire the storekeeper.</summary>
        public int StorekeeperRequiredLevel { get; }

        /// <summary>The storekeeper restocks a point whose fill (0..1) is below this.</summary>
        public float RestockThreshold { get; }
    }
}
