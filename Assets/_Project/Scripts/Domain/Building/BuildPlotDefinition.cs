using System;
using System.Globalization;
using AutoService.Domain.Common;

namespace AutoService.Domain.Building
{
    /// <summary>Immutable rules of one fixed build plot: what it builds, where, for how much and from which level.</summary>
    public sealed class BuildPlotDefinition
    {
        /// <summary>Value of <see cref="ParkingSlotIndex"/> for plots that are not parking slots.</summary>
        public const int NoSlot = -1;

        /// <summary>Creates and validates the definition.</summary>
        /// <param name="id">Unique plot id (the buildable config id), stable for saves.</param>
        /// <param name="kind">What the plot builds.</param>
        /// <param name="targetId">
        /// Point id for <see cref="BuildableKind.ServicePoint"/>; non-negative slot index (e.g. "2") for
        /// <see cref="BuildableKind.ParkingSlot"/>.
        /// </param>
        /// <param name="cost">Price of the construction.</param>
        /// <param name="requiredLevel">Player level needed to build (&gt;= 0).</param>
        /// <param name="flowBonus">Added to the location's car flow multiplier once built (&gt;= 0).</param>
        /// <exception cref="ArgumentException">
        /// Thrown for empty ids, a slot index that is not a non-negative integer, a negative level or a negative/NaN bonus.
        /// </exception>
        public BuildPlotDefinition(string id, BuildableKind kind, string targetId, Money cost, int requiredLevel, double flowBonus)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Build plot id must not be empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException("Build plot '" + id + "' has no target.", nameof(targetId));
            }

            int slotIndex = NoSlot;
            if (kind == BuildableKind.ParkingSlot
                && !int.TryParse(targetId, NumberStyles.None, CultureInfo.InvariantCulture, out slotIndex))
            {
                throw new ArgumentException(
                    "Build plot '" + id + "': parking slot target must be a non-negative slot index, got '" + targetId + "'.",
                    nameof(targetId));
            }

            if (requiredLevel < 0)
            {
                throw new ArgumentException("Build plot '" + id + "': required level must be non-negative.", nameof(requiredLevel));
            }

            // Why: the negated comparison also rejects NaN.
            if (!(flowBonus >= 0.0) || double.IsInfinity(flowBonus))
            {
                throw new ArgumentException("Build plot '" + id + "': flow bonus must be a finite non-negative number.", nameof(flowBonus));
            }

            Id = id;
            Kind = kind;
            TargetId = targetId;
            ParkingSlotIndex = slotIndex;
            Cost = cost;
            RequiredLevel = requiredLevel;
            FlowBonus = flowBonus;
        }

        /// <summary>Unique plot id.</summary>
        public string Id { get; }

        /// <summary>What the plot builds.</summary>
        public BuildableKind Kind { get; }

        /// <summary>Point id or parking slot index (as text), depending on <see cref="Kind"/>.</summary>
        public string TargetId { get; }

        /// <summary>Parsed slot index for <see cref="BuildableKind.ParkingSlot"/>, otherwise <see cref="NoSlot"/>.</summary>
        public int ParkingSlotIndex { get; }

        /// <summary>Price of the construction.</summary>
        public Money Cost { get; }

        /// <summary>Player level needed to build.</summary>
        public int RequiredLevel { get; }

        /// <summary>Added to the location's car flow multiplier once built.</summary>
        public double FlowBonus { get; }
    }
}
