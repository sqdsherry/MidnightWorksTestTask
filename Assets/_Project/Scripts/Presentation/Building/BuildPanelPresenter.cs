using System;
using System.Collections.Generic;
using System.Globalization;
using AutoService.Domain.Building;
using AutoService.Domain.Common;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Panels;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Formatting;
using UnityEngine;

namespace AutoService.Presentation.Building
{
    /// <summary>
    /// "Walk up, stand, build": ticks the dwell of every plot, opens the build panel next to the plot the
    /// character has stood at long enough, keeps it on screen next to the plot, and builds on the button.
    /// The panel is the shared <see cref="OfferPanelView"/>.
    /// </summary>
    /// <remarks>
    /// Closes when the character leaves, on Close / Esc (registered in the <see cref="EscapeRouter"/> while open), and when
    /// the plot gets built.
    /// The button label is re-evaluated only when the balance changes, and its two texts are formatted once per opening,
    /// so the per-frame work is the dwell timers plus one <see cref="Camera.WorldToScreenPoint(Vector3)"/> — no allocations.
    /// </remarks>
    public sealed class BuildPanelPresenter : ITickable, IEscapeHandler, IDisposable
    {
        private readonly IBuildService _build;
        private readonly IConfigProvider _config;
        private readonly IWalletService _wallet;
        private readonly OfferPanelView _view;
        private readonly Camera _camera;
        private readonly IReadOnlyList<BuildPlotView> _plots;
        private readonly EscapeRouter _escape;

        private BuildPlotView _openPlot;
        private string _buildLabel;
        private string _needLabel;
        private BuildAvailability _shownAvailability;
        private bool _disposed;

        /// <summary>Creates the presenter, hides the panel and subscribes to the plots, the panel and the services.</summary>
        /// <param name="build">Build service.</param>
        /// <param name="config">Buildable texts and prices.</param>
        /// <param name="wallet">Balance, for the Build / Need label.</param>
        /// <param name="view">The scene's build panel.</param>
        /// <param name="camera">Gameplay camera, to place the panel next to the plot.</param>
        /// <param name="plots">Plots whose dwell this presenter drives.</param>
        /// <param name="escape">The scene's Esc router; may be null (no Esc then).</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        public BuildPanelPresenter(
            IBuildService build,
            IConfigProvider config,
            IWalletService wallet,
            OfferPanelView view,
            Camera camera,
            IReadOnlyList<BuildPlotView> plots,
            EscapeRouter escape)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _plots = plots ?? throw new ArgumentNullException(nameof(plots));
            _escape = escape;

            for (int i = 0; i < _plots.Count; i++)
            {
                BuildPlotView plot = _plots[i];
                if (plot != null)
                {
                    plot.DwellCompleted += OnDwellCompleted;
                    plot.Left += OnPlotLeft;
                }
            }

            _view.ActionClicked += OnBuildClicked;
            _view.CloseClicked += Close;
            _build.Built += OnPlotBuilt;
            _build.BuiltRestored += OnPlotBuilt;
            _wallet.BalanceChanged += OnBalanceChanged;

            _view.Hide();
        }

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            // Why: a panel hidden because its object is behind the camera stays open but invisible; Esc must not be
            // swallowed by something the player cannot see — it goes on to the pause menu.
            if (_openPlot == null || !_view.IsVisible)
            {
                return false;
            }

            Close();
            return true;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // Why: every plot is ticked, not only the one the character stands at — a left plot's ring keeps draining.
            for (int i = 0; i < _plots.Count; i++)
            {
                BuildPlotView plot = _plots[i];
                if (plot != null)
                {
                    plot.TickDwell(deltaTime);
                }
            }

            if (_openPlot != null)
            {
                FollowPlot();
            }
        }

        /// <summary>Unsubscribes from everything and hides the panel. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = 0; i < _plots.Count; i++)
            {
                BuildPlotView plot = _plots[i];

                // Why: Unity's == — plots may already be destroyed while the scene unloads.
                if (plot != null)
                {
                    plot.DwellCompleted -= OnDwellCompleted;
                    plot.Left -= OnPlotLeft;
                }
            }

            if (_view != null)
            {
                _view.ActionClicked -= OnBuildClicked;
                _view.CloseClicked -= Close;
            }

            _build.Built -= OnPlotBuilt;
            _build.BuiltRestored -= OnPlotBuilt;
            _wallet.BalanceChanged -= OnBalanceChanged;
            _escape?.Remove(this);

            _openPlot = null;
        }

        private void OnDwellCompleted(BuildPlotView plot)
        {
            if (!_build.TryGet(plot.PlotId, out BuildPlot buildPlot) || buildPlot.IsBuilt
                || !_config.TryGetBuildable(plot.PlotId, out BuildableSettings settings))
            {
                return;
            }

            Open(plot, settings);
        }

        private void Open(BuildPlotView plot, BuildableSettings settings)
        {
            _openPlot = plot;
            _escape?.Push(this);
            string cost = MoneyFormatter.Format(settings.Cost);

            // Why: formatted once per opening; the balance listener only picks one of them.
            _buildLabel = string.Format(_view.ActionLabelFormat, cost);
            _needLabel = string.Format(_view.NeedLabelFormat, cost);

            BuildAvailability availability = _build.GetAvailability(plot.PlotId);
            string requirement = availability == BuildAvailability.Locked
                ? string.Format(_view.RequirementFormat, settings.RequiredLevel.ToString(CultureInfo.InvariantCulture))
                : null;

            _view.Show(settings.DisplayName, settings.Description, cost, requirement);
            ShowAvailability(availability);
            FollowPlot();
        }

        private void Close()
        {
            if (_openPlot == null)
            {
                return;
            }

            _openPlot = null;
            _escape?.Remove(this);
            if (_view != null)
            {
                _view.Hide();
            }
        }

        private void FollowPlot()
        {
            _view.Follow(_camera, _openPlot.PanelAnchor.position);
        }

        private void ShowAvailability(BuildAvailability availability)
        {
            _shownAvailability = availability;
            switch (availability)
            {
                case BuildAvailability.Available:
                    _view.SetAffordable(true, _buildLabel);
                    break;
                case BuildAvailability.NotEnoughMoney:
                    _view.SetAffordable(false, _needLabel);
                    break;
                case BuildAvailability.Locked:
                    _view.SetAffordable(false, _view.LockedLabel);
                    break;
                default:
                    Close();
                    break;
            }
        }

        private void OnBalanceChanged(Money balance)
        {
            if (_openPlot == null)
            {
                return;
            }

            BuildAvailability availability = _build.GetAvailability(_openPlot.PlotId);
            if (availability != _shownAvailability)
            {
                ShowAvailability(availability);
            }
        }

        private void OnBuildClicked()
        {
            if (_openPlot == null)
            {
                return;
            }

            // Why: on success the Built event closes the panel; on failure (e.g. the money was spent meanwhile) refresh it.
            if (!_build.TryBuild(_openPlot.PlotId) && _openPlot != null)
            {
                ShowAvailability(_build.GetAvailability(_openPlot.PlotId));
            }
        }

        private void OnPlotLeft(BuildPlotView plot)
        {
            if (plot == _openPlot)
            {
                Close();
            }
        }

        private void OnPlotBuilt(BuildPlot plot)
        {
            if (_openPlot != null && string.Equals(_openPlot.PlotId, plot.Definition.Id, StringComparison.Ordinal))
            {
                Close();
            }
        }
    }
}
