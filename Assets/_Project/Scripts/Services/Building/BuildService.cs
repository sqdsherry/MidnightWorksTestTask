using System;
using System.Collections.Generic;
using AutoService.Domain.Building;
using AutoService.Services.Economy;
using AutoService.Services.Events;

namespace AutoService.Services.Building
{
    /// <summary>
    /// Default <see cref="IBuildService"/>: owns the <see cref="BuildPlot"/> entities, checks the unlock gate and the wallet,
    /// takes the payment and announces the construction (<see cref="IBuildService.Built"/> + <see cref="BuildCompletedEvent"/>).
    /// </summary>
    /// <remarks>
    /// The service knows nothing about points, parking or the scene: <c>BuildableBinder</c> (Presentation) listens to it
    /// and turns a built plot into a working point or slot. A new buildable is therefore just a config + scene markup.
    /// </remarks>
    public sealed class BuildService : IBuildService
    {
        private readonly IWalletService _wallet;
        private readonly IUnlockGate _gate;
        private readonly IEventBus _eventBus;
        private readonly List<BuildPlot> _plots = new List<BuildPlot>();
        private readonly Dictionary<string, BuildPlot> _plotsById = new Dictionary<string, BuildPlot>(StringComparer.Ordinal);
        private readonly List<string> _builtPlotIds = new List<string>();

        /// <summary>Creates an empty registry.</summary>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public BuildService(IWalletService wallet, IUnlockGate gate, IEventBus eventBus)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <inheritdoc />
        public event Action<BuildPlot> Built;

        /// <inheritdoc />
        public event Action<BuildPlot> BuiltRestored;

        /// <inheritdoc />
        public IReadOnlyList<BuildPlot> Plots => _plots;

        /// <inheritdoc />
        public IReadOnlyList<string> BuiltPlotIds => _builtPlotIds;

        /// <summary>Creates and registers an unbuilt plot. Called by the scene entry point while building the scene.</summary>
        /// <returns>The new plot entity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a plot with the same id is already registered.</exception>
        public BuildPlot Register(BuildPlotDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (_plotsById.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException("Build plot '" + definition.Id + "' is already registered.");
            }

            var plot = new BuildPlot(definition);
            _plots.Add(plot);
            _plotsById.Add(definition.Id, plot);
            return plot;
        }

        /// <inheritdoc />
        public bool TryGet(string plotId, out BuildPlot plot)
        {
            if (plotId == null)
            {
                plot = null;
                return false;
            }

            return _plotsById.TryGetValue(plotId, out plot);
        }

        /// <inheritdoc />
        public BuildAvailability GetAvailability(string plotId)
        {
            if (!TryGet(plotId, out BuildPlot plot))
            {
                throw new ArgumentException("Unknown build plot '" + plotId + "'.", nameof(plotId));
            }

            return AvailabilityOf(plot);
        }

        /// <inheritdoc />
        public bool TryBuild(string plotId)
        {
            if (!TryGet(plotId, out BuildPlot plot) || AvailabilityOf(plot) != BuildAvailability.Available)
            {
                return false;
            }

            // Why: affordability was just checked, but the wallet stays the single authority on spending.
            if (!_wallet.TrySpend(plot.Definition.Cost))
            {
                return false;
            }

            MarkBuilt(plot);
            Built?.Invoke(plot);

            BuildPlotDefinition definition = plot.Definition;
            _eventBus.Publish(new BuildCompletedEvent(definition.Id, definition.Kind, definition.TargetId));
            return true;
        }

        /// <inheritdoc />
        public void RestoreBuilt(IEnumerable<string> plotIds)
        {
            if (plotIds == null)
            {
                throw new ArgumentNullException(nameof(plotIds));
            }

            foreach (string plotId in plotIds)
            {
                if (!TryGet(plotId, out BuildPlot plot) || plot.IsBuilt)
                {
                    continue;
                }

                MarkBuilt(plot);
                BuiltRestored?.Invoke(plot);
            }
        }

        private BuildAvailability AvailabilityOf(BuildPlot plot)
        {
            if (plot.IsBuilt)
            {
                return BuildAvailability.Built;
            }

            if (!_gate.IsUnlocked(plot.Definition.RequiredLevel))
            {
                return BuildAvailability.Locked;
            }

            return _wallet.CanAfford(plot.Definition.Cost) ? BuildAvailability.Available : BuildAvailability.NotEnoughMoney;
        }

        private void MarkBuilt(BuildPlot plot)
        {
            plot.MarkBuilt();
            _builtPlotIds.Add(plot.Definition.Id);
        }
    }
}
