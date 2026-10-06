using System;
using AutoService.Domain.Building;
using AutoService.Domain.Common;

namespace AutoService.Services.Config
{
    /// <summary>
    /// Immutable settings of one buildable (a bay, an extra parking slot). Scene build plots reference it by <see cref="Id"/>.
    /// </summary>
    public sealed class BuildableSettings
    {
        /// <summary>Creates and validates the settings.</summary>
        /// <param name="id">Unique id referenced by scene plots; also the build plot id (stable for saves).</param>
        /// <param name="displayName">Player-facing name (English).</param>
        /// <param name="description">Player-facing description (English).</param>
        /// <param name="kind">What gets built.</param>
        /// <param name="targetId">Point id (service point) or slot index (parking slot).</param>
        /// <param name="cost">Construction price.</param>
        /// <param name="requiredLevel">Player level needed to build.</param>
        /// <param name="flowBonus">Added to the location's car flow multiplier once built.</param>
        /// <exception cref="ArgumentException">Thrown for invalid values (see <see cref="BuildPlotDefinition"/>).</exception>
        public BuildableSettings(
            string id,
            string displayName,
            string description,
            BuildableKind kind,
            string targetId,
            Money cost,
            int requiredLevel,
            double flowBonus)
        {
            // Why: the plot definition holds all the rules, so the settings are validated by building it once.
            PlotDefinition = new BuildPlotDefinition(id, kind, targetId, cost, requiredLevel, flowBonus);
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
        }

        /// <summary>Unique id.</summary>
        public string Id => PlotDefinition.Id;

        /// <summary>Player-facing name.</summary>
        public string DisplayName { get; }

        /// <summary>Player-facing description.</summary>
        public string Description { get; }

        /// <summary>What gets built.</summary>
        public BuildableKind Kind => PlotDefinition.Kind;

        /// <summary>Point id or slot index.</summary>
        public string TargetId => PlotDefinition.TargetId;

        /// <summary>Construction price.</summary>
        public Money Cost => PlotDefinition.Cost;

        /// <summary>Player level needed to build.</summary>
        public int RequiredLevel => PlotDefinition.RequiredLevel;

        /// <summary>Added to the location's car flow multiplier once built.</summary>
        public double FlowBonus => PlotDefinition.FlowBonus;

        /// <summary>Rules of the plot built from this buildable (immutable, safe to share).</summary>
        public BuildPlotDefinition PlotDefinition { get; }
    }
}
