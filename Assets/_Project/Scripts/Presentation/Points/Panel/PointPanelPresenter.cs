using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// <para><b>No garbage while open.</b> The balance changes inside the game loop (payments, boxes) several times a second,
    /// so every string is built only when the panel opens, an upgrade level changes or a worker is hired (player clicks).
    /// A balance or gate change only switches a button between its cached labels (Available ↔ Need / Locked); the status
    /// line and the income are numbers written into cached formats with the non-allocating <c>TMP_Text.SetText</c>.</para>
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
        private readonly UpgradeRowState _speed = new UpgradeRowState(UpgradeKind.Speed);
        private readonly UpgradeRowState _price = new UpgradeRowState(UpgradeKind.Price);
        private readonly HireRowState _hire = new HireRowState();

        private ManagePadView _openPad;
        private ServicePoint _openPoint;
        private PointWorkerSettings _worker;
        private string _statusHiredFormat;
        private string _statusNotHiredFormat;
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
            _gate.Changed += RefreshAvailability;
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
            _gate.Changed -= RefreshAvailability;
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

            bool knownType = _config.TryGetServiceType(point.Definition.ServiceTypeId, out ServiceTypeSettings type);
            _worker = knownType ? type.Worker : null;
            _view.SetTitle(knownType ? type.DisplayName : point.Definition.Id);
            PrepareStatusFormats();
            ShowStatus();
            PrepareUpgradeRow(_speed);
            PrepareUpgradeRow(_price);
            PrepareHireRow();
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
            _worker = null;
        }

        // ── Status ─────────────────────────────────────────────────────────────────────────────────────────────────

        // Why: the worker part never changes while open except hired ↔ not hired, so both full status formats are built
        // once; the supply numbers are then filled in by SetText ("Supply {0}/{1} · Washer: hired").
        private void PrepareStatusFormats()
        {
            string supply = _openPoint.Supply != null ? _view.SupplyFormat : string.Empty;
            if (_worker == null)
            {
                _statusHiredFormat = supply;
                _statusNotHiredFormat = supply;
                return;
            }

            string separator = supply.Length > 0 ? _view.StatusSeparator : string.Empty;
            _statusHiredFormat = supply + separator + string.Format(_view.WorkerFormat, _worker.Title, _view.WorkerHiredText);
            _statusNotHiredFormat = supply + separator + string.Format(_view.WorkerFormat, _worker.Title, _view.WorkerNotHiredText);
        }

        private void ShowStatus()
        {
            string format = _staff.HasWorker(_openPoint.Definition.Id) ? _statusHiredFormat : _statusNotHiredFormat;
            SupplyStock supply = _openPoint.Supply;
            if (supply != null)
            {
                _view.SetStatus(format, supply.Current, supply.Capacity);
            }
            else
            {
                _view.SetStatus(format);
            }
        }

        // ── Upgrades ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Formats the row's labels for the current level (on opening and after a purchase).</summary>
        private void PrepareUpgradeRow(UpgradeRowState state)
        {
            UpgradeRowView row = _view.RowOf(state.Kind);
            if (row == null)
            {
                return;
            }

            string pointId = _openPoint.Definition.Id;
            UpgradeAvailability availability = _upgrades.GetAvailability(pointId, state.Kind);
            if (availability == UpgradeAvailability.NotSupported || !_config.TryGetUpgrade(state.Kind, out UpgradeSettings settings))
            {
                row.SetVisible(false);
                state.Visible = false;
                return;
            }

            state.Visible = true;
            row.SetVisible(true);
            row.SetInfo(settings.DisplayName, settings.EffectFormat);
            row.SetLevel(_upgrades.GetLevel(pointId, state.Kind));
            string cost = MoneyFormatter.Format(_upgrades.GetNextCost(pointId, state.Kind));
            state.BuyLabel = string.Format(_view.UpgradeLabelFormat, cost);
            state.NeedLabel = string.Format(_view.NeedLabelFormat, cost);
            state.LockedLabel = LockedLabel(settings.RequiredLevel);
            ShowUpgradeButton(state, availability);
        }

        private void ShowUpgradeButton(UpgradeRowState state, UpgradeAvailability availability)
        {
            state.Shown = availability;
            UpgradeRowView row = _view.RowOf(state.Kind);
            switch (availability)
            {
                case UpgradeAvailability.Available:
                    row.SetButton(true, state.BuyLabel);
                    break;
                case UpgradeAvailability.NotEnoughMoney:
                    row.SetButton(false, state.NeedLabel);
                    break;
                case UpgradeAvailability.Maxed:
                    row.SetButton(false, _view.MaxLabel);
                    break;
                default:
                    row.SetButton(false, state.LockedLabel);
                    break;
            }
        }

        private void RefreshUpgradeButton(UpgradeRowState state)
        {
            if (!state.Visible)
            {
                return;
            }

            UpgradeAvailability availability = _upgrades.GetAvailability(_openPoint.Definition.Id, state.Kind);
            if (availability != state.Shown)
            {
                ShowUpgradeButton(state, availability);
            }
        }

        // ── Hire ───────────────────────────────────────────────────────────────────────────────────────────────────

        private void PrepareHireRow()
        {
            HireRowView row = _view.HireRow;
            if (row == null)
            {
                return;
            }

            HireAvailability availability = _staff.GetWorkerAvailability(_openPoint.Definition.Id);
            if (availability == HireAvailability.NotSupported || _worker == null)
            {
                row.SetVisible(false);
                _hire.Visible = false;
                return;
            }

            _hire.Visible = true;
            row.SetVisible(true);
            row.SetTitle(_worker.Title);
            string cost = MoneyFormatter.Format(_worker.HireCost);
            _hire.HireLabel = string.Format(_view.HireLabelFormat, _worker.Title, cost);
            _hire.NeedLabel = string.Format(_view.NeedLabelFormat, cost);
            _hire.LockedLabel = LockedLabel(_worker.RequiredLevel);
            ShowHireButton(availability);
        }

        private void ShowHireButton(HireAvailability availability)
        {
            _hire.Shown = availability;
            HireRowView row = _view.HireRow;
            switch (availability)
            {
                case HireAvailability.Available:
                    row.SetButton(true, _hire.HireLabel);
                    break;
                case HireAvailability.NotEnoughMoney:
                    row.SetButton(false, _hire.NeedLabel);
                    break;
                case HireAvailability.Hired:
                    row.SetButton(false, _view.HiredLabel);
                    break;
                default:
                    row.SetButton(false, _hire.LockedLabel);
                    break;
            }
        }

        private void RefreshHireButton()
        {
            if (!_hire.Visible)
            {
                return;
            }

            HireAvailability availability = _staff.GetWorkerAvailability(_openPoint.Definition.Id);
            if (availability != _hire.Shown)
            {
                ShowHireButton(availability);
            }
        }

        // ── Events ─────────────────────────────────────────────────────────────────────────────────────────────────

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

        // Why: balance and gate changes only switch the buttons between their cached labels — no strings are built.
        private void RefreshAvailability()
        {
            if (_openPoint == null)
            {
                return;
            }

            RefreshUpgradeButton(_speed);
            RefreshUpgradeButton(_price);
            RefreshHireButton();
        }

        private void OnUpgradeClicked(UpgradeKind kind)
        {
            // Why: on success the Upgraded event re-formats the row; on failure (spent meanwhile) only the button changes.
            if (_openPoint != null && !_upgrades.TryUpgrade(_openPoint.Definition.Id, kind))
            {
                RefreshAvailability();
            }
        }

        private void OnHireClicked()
        {
            if (_openPoint != null && !_staff.TryHireWorker(_openPoint.Definition.Id))
            {
                RefreshAvailability();
            }
        }

        private void OnUpgraded(string pointId, UpgradeKind kind)
        {
            if (_openPoint == null || !string.Equals(_openPoint.Definition.Id, pointId, StringComparison.Ordinal))
            {
                return;
            }

            // Why: a new level means a new price — this row's labels are formatted again; the other row only re-checks.
            PrepareUpgradeRow(kind == UpgradeKind.Speed ? _speed : _price);
            RefreshAvailability();
        }

        private void OnHired(StaffMember member)
        {
            if (_openPoint == null)
            {
                return;
            }

            ShowStatus();
            RefreshAvailability();
        }

        private void OnBalanceChanged(Money balance) => RefreshAvailability();

        private void OnSupplyChanged(SupplyStock stock)
        {
            if (_openPoint != null)
            {
                ShowStatus();
            }
        }

        private void OnPadLeft(ManagePadView pad)
        {
            if (pad == _openPad)
            {
                Close();
            }
        }

        /// <summary>Cached labels and the last shown state of one upgrade row.</summary>
        private sealed class UpgradeRowState
        {
            public UpgradeRowState(UpgradeKind kind)
            {
                Kind = kind;
            }

            public UpgradeKind Kind { get; }

            public bool Visible { get; set; }

            public UpgradeAvailability Shown { get; set; }

            public string BuyLabel { get; set; }

            public string NeedLabel { get; set; }

            public string LockedLabel { get; set; }
        }

        /// <summary>Cached labels and the last shown state of the hire row.</summary>
        private sealed class HireRowState
        {
            public bool Visible { get; set; }

            public HireAvailability Shown { get; set; }

            public string HireLabel { get; set; }

            public string NeedLabel { get; set; }

            public string LockedLabel { get; set; }
        }
    }
}
