using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AutoService.Domain.Common;
using AutoService.Domain.Points;
using AutoService.Domain.Staff;
using AutoService.Domain.Supplies;
using AutoService.Domain.Upgrades;
using AutoService.Presentation.Controls;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Formatting;
using AutoService.Services.Points;
using AutoService.Services.Staff;
using AutoService.Services.Upgrades;
using UnityEngine;

namespace AutoService.Presentation.Points.Panel
{
    /// <summary>
    /// "Walk onto the blue pad, stand, manage": ticks the dwell of every point pad, opens the <see cref="PointPanelView"/>
    /// next to the pad the character has stood on long enough, and buys upgrades / hires the worker on its buttons.
    /// </summary>
    /// <remarks>
    /// Closes when the character leaves the pad, on Close / Esc (and hides while the pad is behind the camera).
    /// Texts are rebuilt only on events (balance, upgrade, hire, the point's stock, the unlock gate); per frame the presenter
    /// only ticks the dwell timers and moves the panel, and once a second writes the income with a non-allocating SetText.
    /// </remarks>
    public sealed class PointPanelPresenter : ITickable, IDisposable
    {
        private const float IncomeRefreshSeconds = 1f;

        private readonly IServicePointService _points;
        private readonly IUpgradeService _upgrades;
        private readonly IStaffService _staff;
        private readonly IWalletService _wallet;
        private readonly IUnlockGate _gate;
        private readonly IConfigProvider _config;
        private readonly PointIncomeTracker _income;
        private readonly PointPanelView _view;
        private readonly Camera _camera;
        private readonly IReadOnlyList<ManagePadView> _pads;
        private readonly GameplayInput _input;
        private readonly StringBuilder _status = new StringBuilder(64);

        private ManagePadView _openPad;
        private ServicePoint _openPoint;
        private float _incomeTimer;
        private bool _disposed;

        /// <summary>Creates the presenter, hides the panel and subscribes to the pads, the panel and the services.</summary>
        /// <param name="points">Point registry (the pads' targets).</param>
        /// <param name="upgrades">Upgrade levels and purchases.</param>
        /// <param name="staff">Worker hiring.</param>
        /// <param name="wallet">Balance, for the button labels.</param>
        /// <param name="gate">Level requirements; its changes refresh the buttons.</param>
        /// <param name="config">Names and worker titles.</param>
        /// <param name="income">Income per minute of the points.</param>
        /// <param name="view">The scene's point panel.</param>
        /// <param name="camera">Gameplay camera, to place the panel next to the pad.</param>
        /// <param name="pads">Pads of the points (warehouse pads are ignored).</param>
        /// <param name="input">Gameplay input for Esc; may be null (no Esc then).</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        public PointPanelPresenter(
            IServicePointService points,
            IUpgradeService upgrades,
            IStaffService staff,
            IWalletService wallet,
            IUnlockGate gate,
            IConfigProvider config,
            PointIncomeTracker income,
            PointPanelView view,
            Camera camera,
            IReadOnlyList<ManagePadView> pads,
            GameplayInput input)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
            _staff = staff ?? throw new ArgumentNullException(nameof(staff));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _income = income ?? throw new ArgumentNullException(nameof(income));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _pads = pads ?? throw new ArgumentNullException(nameof(pads));
            _input = input;

            for (int i = 0; i < _pads.Count; i++)
            {
                ManagePadView pad = _pads[i];
                if (pad != null && pad.Target == ManagePadTarget.ServicePoint)
                {
                    pad.DwellCompleted += OnDwellCompleted;
                    pad.Left += OnPadLeft;
                }
            }

            _view.UpgradeClicked += OnUpgradeClicked;
            _view.HireClicked += OnHireClicked;
            _view.CloseClicked += Close;
            _upgrades.Upgraded += OnUpgraded;
            _staff.Hired += OnHired;
            _wallet.BalanceChanged += OnBalanceChanged;
            _gate.Changed += Refresh;
            if (_input != null)
            {
                _input.CancelPressed += Close;
            }

            _view.Hide();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // Why: every pad is ticked, not only the one the character stands on — a left pad's ring keeps draining.
            for (int i = 0; i < _pads.Count; i++)
            {
                ManagePadView pad = _pads[i];
                if (pad != null && pad.Target == ManagePadTarget.ServicePoint)
                {
                    pad.TickDwell(deltaTime);
                }
            }

            if (_openPad == null)
            {
                return;
            }

            _view.Follow(_camera, _openPad.PanelAnchor.position);
            _incomeTimer -= deltaTime;
            if (_incomeTimer <= 0f)
            {
                ShowIncome();
            }
        }

        /// <summary>Unsubscribes from everything. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = 0; i < _pads.Count; i++)
            {
                ManagePadView pad = _pads[i];

                // Why: Unity's == — pads may already be destroyed while the scene unloads.
                if (pad != null)
                {
                    pad.DwellCompleted -= OnDwellCompleted;
                    pad.Left -= OnPadLeft;
                }
            }

            if (_view != null)
            {
                _view.UpgradeClicked -= OnUpgradeClicked;
                _view.HireClicked -= OnHireClicked;
                _view.CloseClicked -= Close;
            }

            _upgrades.Upgraded -= OnUpgraded;
            _staff.Hired -= OnHired;
            _wallet.BalanceChanged -= OnBalanceChanged;
            _gate.Changed -= Refresh;
            if (_input != null)
            {
                _input.CancelPressed -= Close;
            }

            ReleasePoint();
            _openPad = null;
        }

        private void OnDwellCompleted(ManagePadView pad)
        {
            if (!_points.TryGet(pad.TargetId, out ServicePoint point))
            {
                return;
            }

            ReleasePoint();
            _openPad = pad;
            _openPoint = point;
            if (point.Supply != null)
            {
                point.Supply.Changed += OnSupplyChanged;
            }

            _view.SetTitle(_config.TryGetServiceType(point.Definition.ServiceTypeId, out ServiceTypeSettings type)
                ? type.DisplayName
                : point.Definition.Id);
            Refresh();
            ShowIncome();
            _view.Show();
            _view.Follow(_camera, pad.PanelAnchor.position);
        }

        private void Close()
        {
            if (_openPad == null)
            {
                return;
            }

            ReleasePoint();
            _openPad = null;
            if (_view != null)
            {
                _view.Hide();
            }
        }

        private void ReleasePoint()
        {
            if (_openPoint != null && _openPoint.Supply != null)
            {
                _openPoint.Supply.Changed -= OnSupplyChanged;
            }

            _openPoint = null;
        }

        private void Refresh()
        {
            if (_openPoint == null)
            {
                return;
            }

            string pointId = _openPoint.Definition.Id;
            _config.TryGetServiceType(_openPoint.Definition.ServiceTypeId, out ServiceTypeSettings type);
            PointWorkerSettings worker = type?.Worker;

            _view.SetStatus(BuildStatus(worker, _staff.HasWorker(pointId)));
            ShowUpgrade(pointId, UpgradeKind.Speed);
            ShowUpgrade(pointId, UpgradeKind.Price);
            ShowHire(pointId, worker);
        }

        private string BuildStatus(PointWorkerSettings worker, bool hired)
        {
            _status.Clear();
            SupplyStock supply = _openPoint.Supply;
            if (supply != null)
            {
                _status.AppendFormat(CultureInfo.InvariantCulture, _view.SupplyFormat, supply.Current, supply.Capacity);
            }

            if (worker != null)
            {
                if (_status.Length > 0)
                {
                    _status.Append(_view.StatusSeparator);
                }

                _status.AppendFormat(_view.WorkerFormat, worker.Title, hired ? _view.WorkerHiredText : _view.WorkerNotHiredText);
            }

            return _status.ToString();
        }

        private void ShowUpgrade(string pointId, UpgradeKind kind)
        {
            UpgradeRowView row = _view.RowOf(kind);
            if (row == null)
            {
                return;
            }

            UpgradeAvailability availability = _upgrades.GetAvailability(pointId, kind);
            if (availability == UpgradeAvailability.NotSupported || !_config.TryGetUpgrade(kind, out UpgradeSettings settings))
            {
                row.SetVisible(false);
                return;
            }

            row.SetVisible(true);
            row.SetInfo(settings.DisplayName, settings.EffectFormat);
            row.SetLevel(_upgrades.GetLevel(pointId, kind));
            string cost = MoneyFormatter.Format(_upgrades.GetNextCost(pointId, kind));
            switch (availability)
            {
                case UpgradeAvailability.Available:
                    row.SetButton(true, string.Format(_view.UpgradeLabelFormat, cost));
                    break;
                case UpgradeAvailability.NotEnoughMoney:
                    row.SetButton(false, string.Format(_view.NeedLabelFormat, cost));
                    break;
                case UpgradeAvailability.Maxed:
                    row.SetButton(false, _view.MaxLabel);
                    break;
                default:
                    row.SetButton(false, LockedLabel(settings.RequiredLevel));
                    break;
            }
        }

        private void ShowHire(string pointId, PointWorkerSettings worker)
        {
            HireRowView row = _view.HireRow;
            if (row == null)
            {
                return;
            }

            HireAvailability availability = _staff.GetWorkerAvailability(pointId);
            if (availability == HireAvailability.NotSupported || worker == null)
            {
                row.SetVisible(false);
                return;
            }

            row.SetVisible(true);
            string cost = MoneyFormatter.Format(worker.HireCost);
            switch (availability)
            {
                case HireAvailability.Available:
                    row.SetButton(true, string.Format(_view.HireLabelFormat, worker.Title, cost));
                    break;
                case HireAvailability.NotEnoughMoney:
                    row.SetButton(false, string.Format(_view.NeedLabelFormat, cost));
                    break;
                case HireAvailability.Hired:
                    row.SetButton(false, _view.HiredLabel);
                    break;
                default:
                    row.SetButton(false, LockedLabel(worker.RequiredLevel));
                    break;
            }
        }

        private string LockedLabel(int requiredLevel)
        {
            return string.Format(_view.LockedLabelFormat, requiredLevel.ToString(CultureInfo.InvariantCulture));
        }

        private void ShowIncome()
        {
            _incomeTimer = IncomeRefreshSeconds;
            if (_openPoint != null)
            {
                _view.SetIncome(_income.GetIncomePerMinute(_openPoint.Definition.Id).Amount);
            }
        }

        private void OnUpgradeClicked(UpgradeKind kind)
        {
            // Why: on success the Upgraded event refreshes the panel; on failure (spent meanwhile) refresh it here.
            if (_openPoint != null && !_upgrades.TryUpgrade(_openPoint.Definition.Id, kind))
            {
                Refresh();
            }
        }

        private void OnHireClicked()
        {
            if (_openPoint != null && !_staff.TryHireWorker(_openPoint.Definition.Id))
            {
                Refresh();
            }
        }

        private void OnUpgraded(string pointId, UpgradeKind kind)
        {
            if (_openPoint != null && string.Equals(_openPoint.Definition.Id, pointId, StringComparison.Ordinal))
            {
                Refresh();
            }
        }

        private void OnHired(StaffMember member) => Refresh();

        private void OnBalanceChanged(Money balance) => Refresh();

        private void OnSupplyChanged(SupplyStock stock) => Refresh();

        private void OnPadLeft(ManagePadView pad)
        {
            if (pad == _openPad)
            {
                Close();
            }
        }
    }
}
