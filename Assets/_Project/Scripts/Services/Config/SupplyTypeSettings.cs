using System;
using AutoService.Domain.Common;

namespace AutoService.Services.Config
{
    /// <summary>
    /// Immutable settings of one consumable (shampoo, oil, tires). Service types reference it by <see cref="Id"/>.
    /// </summary>
    public sealed class SupplyTypeSettings
    {
        /// <summary>Creates and validates the settings.</summary>
        /// <param name="id">Unique id referenced by service types.</param>
        /// <param name="displayName">Player-facing name (English).</param>
        /// <param name="boxPrice">Price of one box at the warehouse.</param>
        /// <param name="unitsPerBox">Units in one box (&gt; 0).</param>
        /// <exception cref="ArgumentException">Thrown for an empty id, a negative price or a non-positive box size.</exception>
        public SupplyTypeSettings(string id, string displayName, Money boxPrice, int unitsPerBox)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Supply type id must not be empty.", nameof(id));
            }

            if (boxPrice < Money.Zero)
            {
                throw new ArgumentException("Box price must be non-negative, got " + boxPrice + ".", nameof(boxPrice));
            }

            if (unitsPerBox <= 0)
            {
                throw new ArgumentException("Units per box must be positive, got " + unitsPerBox + ".", nameof(unitsPerBox));
            }

            Id = id;
            DisplayName = displayName ?? string.Empty;
            BoxPrice = boxPrice;
            UnitsPerBox = unitsPerBox;
        }

        /// <summary>Unique id.</summary>
        public string Id { get; }

        /// <summary>Player-facing name.</summary>
        public string DisplayName { get; }

        /// <summary>Price of one box at the warehouse.</summary>
        public Money BoxPrice { get; }

        /// <summary>Units in one box.</summary>
        public int UnitsPerBox { get; }
    }
}
