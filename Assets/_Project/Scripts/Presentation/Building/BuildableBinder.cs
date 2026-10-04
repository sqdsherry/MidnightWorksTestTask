using System;
using System.Collections.Generic;
using AutoService.Domain.Building;
using AutoService.Domain.Points;
using AutoService.Presentation.Points;
using AutoService.Presentation.Traffic;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Formatting;
using AutoService.Services.Traffic;
using AutoService.Services.Economy;
using AutoService.Services.Progression;
using AutoService.Domain.Common;

namespace AutoService.Presentation.Building
{
    /// <summary>
    /// Turns built plots of ONE location into working parts of the scene: switches the plot view to its real object,
    /// registers a built bay as a service point (point + view + presenter) and adds it to the traffic, opens a bought
    /// parking slot, and keeps the location's car flow multiplier at 1 + Σ flow bonus of its built plots.
    /// </summary>
    /// <remarks>
    /// Parking slots are offered one at a time, in slot order: the traffic only grows its lot (slots 0..N-1), so the
    /// ghost of slot 4 appears once slot 3 is bought. Built bays go through the same <see cref="PointRegistrar"/> as the
    /// points that work from the start, so they get the same view setup and presenter.
    /// </remarks>
    public sealed class BuildableBinder : IDisposable
    {
        private readonly IBuildService _build;
        private readonly IConfigProvider _config;
        private readonly PointRegistrar _registrar;
        private readonly LocationTraffic _traffic;
        private readonly LocationLayout _layout;
        private readonly IGameLogger _logger;
        private readonly IProgressionService _progression;
        private readonly IWalletService _wallet;

        private readonly Dictionary<string, BuildPlotView> _views = new Dictionary<string, BuildPlotView>(StringComparer.Ordinal);
        private readonly List<BuildPlotView> _parkingViews = new List<BuildPlotView>();
        private bool _disposed;

        /// <summary>
        /// Binds the layout's plots (already registered in <paramref name="build"/>), shows their current state and starts
        /// listening for constructions. Plots without a registered build plot are hidden.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public BuildableBinder(
            IBuildService build,
            IConfigProvider config,
            PointRegistrar registrar,
            LocationTraffic traffic,
            LocationLayout layout,
            IGameLogger logger,
            IProgressionService progression,
            IWalletService wallet)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
            _traffic = traffic ?? throw new ArgumentNullException(nameof(traffic));
            _layout = layout != null ? layout : throw new ArgumentNullException(nameof(layout));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

            CollectViews();

            _build.Built += OnBuilt;
            _build.BuiltRestored += OnBuiltRestored;
            _progression.Changed += UpdateTags;
            _wallet.BalanceChanged += OnBalanceChanged;
            ShowCurrentState();
        }

        private void OnBalanceChanged(Money _) => UpdateTags();

        /// <summary>Stops listening to the build service. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _build.Built -= OnBuilt;
            _build.BuiltRestored -= OnBuiltRestored;
            _progression.Changed -= UpdateTags;
            _wallet.BalanceChanged -= OnBalanceChanged;
        }

        private void CollectViews()
        {
            BuildPlotView[] views = _layout.BuildPlots;
            for (int i = 0; i < views.Length; i++)
            {
                BuildPlotView view = views[i];
                if (view == null || !_build.TryGet(view.PlotId, out BuildPlot plot) || _views.ContainsKey(view.PlotId))
                {
                    // Why: LocationLayout.ValidateBuildPlots reports these on start; a broken plot must not be clickable.
                    if (view != null)
                    {
                        view.SetOffered(false);
                    }

                    continue;
                }

                _views.Add(view.PlotId, view);

                if (plot.Definition.Kind == BuildableKind.ParkingSlot)
                {
                    InsertBySlot(view, plot.Definition.ParkingSlotIndex);
                }
            }
        }

        private void InsertBySlot(BuildPlotView view, int slotIndex)
        {
            int index = 0;
            while (index < _parkingViews.Count && SlotOf(_parkingViews[index]) < slotIndex)
            {
                index++;
            }

            _parkingViews.Insert(index, view);
        }

        private void ShowCurrentState()
        {
            foreach (KeyValuePair<string, BuildPlotView> pair in _views)
            {
                _build.TryGet(pair.Key, out BuildPlot plot);
                if (plot.IsBuilt)
                {
                    Apply(plot, pair.Value, animate: false);
                }
                else
                {
                    pair.Value.ShowUnbuilt();
                }
            }

            RefreshParkingOffers();
            RefreshFlow();
            UpdateTags();
        }

        private void OnBuilt(BuildPlot plot) => OnPlotBuilt(plot, animate: true);

        private void OnBuiltRestored(BuildPlot plot) => OnPlotBuilt(plot, animate: false);

        private void OnPlotBuilt(BuildPlot plot, bool animate)
        {
            // Why: the build service is shared by all locations; plots of another location are not ours.
            if (!_views.TryGetValue(plot.Definition.Id, out BuildPlotView view))
            {
                return;
            }

            Apply(plot, view, animate);
            RefreshParkingOffers();
            RefreshFlow();
        }

        private void Apply(BuildPlot plot, BuildPlotView view, bool animate)
        {
            view.SetBuilt(animate);
            BuildPlotDefinition definition = plot.Definition;
            switch (definition.Kind)
            {
                case BuildableKind.ServicePoint:
                    ActivatePoint(view, definition.TargetId);
                    break;
                case BuildableKind.ParkingSlot:
                    int capacity = definition.ParkingSlotIndex + 1;
                    if (capacity > _traffic.ParkingCapacity)
                    {
                        _traffic.SetParkingCapacity(capacity);
                    }

                    break;
            }
        }

        private void ActivatePoint(BuildPlotView plotView, string pointId)
        {
            ServicePointView view = FindPointView(plotView, pointId);
            if (view == null)
            {
                _logger.Error("[Build] Plot '" + plotView.PlotId + "': no ServicePointView '" + pointId + "' inside its Target.");
                return;
            }

            // The registrar logs why a point could not be registered.
            if (!_registrar.TryRegister(view, _layout.LocationId, PointKind.Service, out ServicePoint point))
            {
                return;
            }

            try
            {
                _traffic.AddServicePoint(point, view.BufferSlots.Length);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                _logger.Error("[Build] Point '" + pointId + "' could not join the traffic: " + exception.Message);
            }
        }

        // Why: called once per construction, never per frame, so the component lookup is fine here.
        private static ServicePointView FindPointView(BuildPlotView plotView, string pointId)
        {
            if (plotView.Target == null)
            {
                return null;
            }

            ServicePointView[] views = plotView.Target.GetComponentsInChildren<ServicePointView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (string.Equals(views[i].PointId, pointId, StringComparison.Ordinal))
                {
                    return views[i];
                }
            }

            return null;
        }

        private void RefreshParkingOffers()
        {
            bool nextOffered = false;
            for (int i = 0; i < _parkingViews.Count; i++)
            {
                BuildPlotView view = _parkingViews[i];
                if (view.IsBuilt)
                {
                    continue;
                }

                view.SetOffered(!nextOffered);
                nextOffered = true;
            }
        }

        private void RefreshFlow()
        {
            double multiplier = 1.0;
            foreach (KeyValuePair<string, BuildPlotView> pair in _views)
            {
                if (_build.TryGet(pair.Key, out BuildPlot plot) && plot.IsBuilt)
                {
                    multiplier += plot.Definition.FlowBonus;
                }
            }

            _traffic.SetFlowMultiplier(multiplier);
        }

        private int SlotOf(BuildPlotView view)
        {
            _build.TryGet(view.PlotId, out BuildPlot plot);
            return plot.Definition.ParkingSlotIndex;
        }

        private void UpdateTags()
        {
            foreach (KeyValuePair<string, BuildPlotView> pair in _views)
            {
                if (!_build.TryGet(pair.Key, out BuildPlot plot) || plot.IsBuilt || !_config.TryGetBuildable(pair.Key, out BuildableSettings settings))
                {
                    continue;
                }

                BuildAvailability availability = _build.GetAvailability(pair.Key);
                string text = MoneyFormatter.Format(settings.Cost);
                
                if (availability == BuildAvailability.Locked)
                {
                    text = $"<color=#FF4D4D>Lv {plot.Definition.RequiredLevel}</color>";
                }
                else if (availability == BuildAvailability.NotEnoughMoney)
                {
                    text = $"<color=#FF4D4D>{text}</color>";
                }

                pair.Value.SetPriceTag(settings.DisplayName, text);
            }
        }
    }
}
