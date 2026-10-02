using System;
using System.Globalization;
using AutoService.Domain.Common;
using AutoService.Domain.Staff;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Panels;
using AutoService.Presentation.Points;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Formatting;
using AutoService.Services.Staff;
using UnityEngine;

namespace AutoService.Presentation.Supplies
{
    /// <summary>
    /// The warehouse's blue pad: stand on it and the storekeeper offer opens (an <see cref="OfferPanelView"/>:
    /// title, description, cost, requirement, Hire). Hires through <see cref="IStaffService.TryHireStorekeeper"/>.
    /// </summary>
    /// <remarks>
    /// Same flow as the build panel: closes when the character leaves, on Close / Esc; the button is re-evaluated on
    /// balance, gate and hire changes only. Per frame: the pad's dwell and the panel position.
    /// </remarks>
    public sealed class StorekeeperOfferPresenter : ITickable, IDisposable
    {
        private readonly IStaffService _staff;
        private readonly IWalletService _wallet;
        private readonly IUnlockGate _gate;
        private readonly StaffSettings _settings;
        private readonly OfferPanelView _view;
        private readonly Camera _camera;
        private readonly ManagePadView _pad;
        private readonly GameplayInput _input;
        private readonly string _locationId;
        private readonly string _hireLabel;
        private readonly string _needLabel;

        private bool _open;
        private HireAvailability _shown;
        private bool _disposed;

        /// <summary>Creates the presenter, hides the panel and subscribes to the pad, the panel and the services.</summary>
        /// <param name="staff">Storekeeper hiring.</param>
        /// <param name="wallet">Balance, for the Hire / Need label.</param>
        /// <param name="gate">Level requirement.</param>
        /// <param name="config">Storekeeper texts and price.</param>
        /// <param name="view">The scene's storekeeper offer panel.</param>
        /// <param name="camera">Gameplay camera, to place the panel next to the pad.</param>
        /// <param name="pad">The warehouse pad (its Target Id is the location id).</param>
        /// <param name="input">Gameplay input for Esc; may be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        public StorekeeperOfferPresenter(
            IStaffService staff,
            IWalletService wallet,
            IUnlockGate gate,
            IConfigProvider config,
            OfferPanelView view,
            Camera camera,
            ManagePadView pad,
            GameplayInput input)
        {
            _staff = staff ?? throw new ArgumentNullException(nameof(staff));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _settings = config?.Staff ?? throw new ArgumentNullException(nameof(config));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _pad = pad != null ? pad : throw new ArgumentNullException(nameof(pad));
            _input = input;
            _locationId = pad.TargetId;

            // Why: the price never changes, so both labels are formatted once.
            string cost = MoneyFormatter.Format(_settings.StorekeeperCost);
            _hireLabel = string.Format(_view.ActionLabelFormat, cost);
            _needLabel = string.Format(_view.NeedLabelFormat, cost);

            _pad.DwellCompleted += OnDwellCompleted;
            _pad.Left += OnPadLeft;
            _view.ActionClicked += OnHireClicked;
            _view.CloseClicked += Close;
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
            _pad.TickDwell(deltaTime);
            if (_open)
            {
                _view.Follow(_camera, _pad.PanelAnchor.position);
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
            if (_pad != null)
            {
                _pad.DwellCompleted -= OnDwellCompleted;
                _pad.Left -= OnPadLeft;
            }

            if (_view != null)
            {
                _view.ActionClicked -= OnHireClicked;
                _view.CloseClicked -= Close;
            }

            _staff.Hired -= OnHired;
            _wallet.BalanceChanged -= OnBalanceChanged;
            _gate.Changed -= Refresh;
            if (_input != null)
            {
                _input.CancelPressed -= Close;
            }

            _open = false;
        }

        private void OnDwellCompleted(ManagePadView pad)
        {
            HireAvailability availability = _staff.GetStorekeeperAvailability(_locationId);
            if (availability == HireAvailability.NotSupported)
            {
                return;
            }

            _open = true;
            ShowOffer(availability);
            _view.Follow(_camera, _pad.PanelAnchor.position);
        }

        private void ShowOffer(HireAvailability availability)
        {
            string requirement = availability == HireAvailability.Locked
                ? string.Format(_view.RequirementFormat, _settings.StorekeeperRequiredLevel.ToString(CultureInfo.InvariantCulture))
                : null;
            _view.Show(_settings.StorekeeperTitle, _settings.StorekeeperDescription, MoneyFormatter.Format(_settings.StorekeeperCost), requirement);
            Show(availability);
        }

        private void Refresh()
        {
            if (!_open)
            {
                return;
            }

            HireAvailability availability = _staff.GetStorekeeperAvailability(_locationId);
            if (availability == _shown)
            {
                return;
            }

            // Why: the requirement line only exists while locked, so entering or leaving Locked redraws the whole offer
            // (a gate change, not a per-frame event); otherwise only the button switches.
            if ((availability == HireAvailability.Locked) != (_shown == HireAvailability.Locked))
            {
                ShowOffer(availability);
            }
            else
            {
                Show(availability);
            }
        }

        private void Show(HireAvailability availability)
        {
            _shown = availability;
            switch (availability)
            {
                case HireAvailability.Available:
                    _view.SetAffordable(true, _hireLabel);
                    break;
                case HireAvailability.NotEnoughMoney:
                    _view.SetAffordable(false, _needLabel);
                    break;
                case HireAvailability.Locked:
                    _view.SetAffordable(false, _view.LockedLabel);
                    break;
                default:
                    _view.SetAffordable(false, _view.CompletedLabel);
                    break;
            }
        }

        private void Close()
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            if (_view != null)
            {
                _view.Hide();
            }
        }

        private void OnHireClicked()
        {
            // Why: on success the Hired event refreshes the button; on failure (spent meanwhile) refresh it here.
            if (_open && !_staff.TryHireStorekeeper(_locationId))
            {
                Refresh();
            }
        }

        private void OnHired(StaffMember member) => Refresh();

        private void OnBalanceChanged(Money balance) => Refresh();

        private void OnPadLeft(ManagePadView pad) => Close();
    }
}
