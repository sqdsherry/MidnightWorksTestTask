using System;

namespace AutoService.Domain.Supplies
{
    /// <summary>A box of consumables carried from the warehouse to a point (by the player or the storekeeper).</summary>
    public readonly struct SupplyBox : IEquatable<SupplyBox>
    {
        /// <summary>"No box": empty hands.</summary>
        public static readonly SupplyBox None = default;

        /// <summary>Creates a box.</summary>
        /// <param name="supplyTypeId">Id of the consumable (non-empty).</param>
        /// <param name="units">Units inside (&gt; 0).</param>
        /// <exception cref="ArgumentException">Thrown for an empty type id.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a non-positive amount.</exception>
        public SupplyBox(string supplyTypeId, int units)
        {
            if (string.IsNullOrWhiteSpace(supplyTypeId))
            {
                throw new ArgumentException("Supply type id must not be empty.", nameof(supplyTypeId));
            }

            if (units <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(units), units, "A box holds at least one unit.");
            }

            SupplyTypeId = supplyTypeId;
            Units = units;
        }

        /// <summary>Id of the consumable; null for <see cref="None"/>.</summary>
        public string SupplyTypeId { get; }

        /// <summary>Units inside; 0 for <see cref="None"/>.</summary>
        public int Units { get; }

        /// <summary>True for <see cref="None"/>.</summary>
        public bool IsNone => Units == 0;

        /// <inheritdoc />
        public bool Equals(SupplyBox other) =>
            Units == other.Units && string.Equals(SupplyTypeId, other.SupplyTypeId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is SupplyBox other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => ((SupplyTypeId?.GetHashCode() ?? 0) * 397) ^ Units;

        /// <inheritdoc />
        public override string ToString() => IsNone ? "no box" : SupplyTypeId + " x" + Units;
    }
}
