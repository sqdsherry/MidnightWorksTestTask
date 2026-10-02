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
    /// "Storekeepers 1/3", description, the next price, requirement, "Hire Storekeeper $900" / "Max staff").
    /// Hires through <see cref="IStaffService.TryHireStorekeeper"/>; the panel stays open and moves on to the next one.
    /// </summary>
    /// <remarks>
    /// Same flow as the build panel: closes when the character leaves, on Close / Esc (through the <see cref="EscapeRouter"/>). The texts are formatted on opening
    /// and after a hire (the count and the price change); a balance or gate change only switches the button between its
    /// cached labels. Per frame: the pad's dwell and the panel position.
    /// </remarks>
    public sealed class StorekeeperOfferPresenter : ITickable, IEscapeHandler, IDisposable
    {
        private readonly IStaffService _staff;
        private readonly IWalletService _wallet;
        private readonly IUnlockGate _gate;
        private readonly StaffSettings _settings;
        private readonly OfferPanelView _view;
        private readonly Camera _camera;
        private readonly ManagePadView _pad;
        private readonly EscapeRouter _escape;
        private readonly string _locationId;

        private string _hireLabel;
        private string _needLabel;
        private bool _open;
        private HireAvailability _shown;
        private bool _disposed;

        /// <summary>Creates the presenter, hides the panel and subscribes to the pad, the panel and the services.</summary>
        /// <param name="staff">Storekeeper hiring.</param>
        /// <param name="wallet">Balance, for the Hire / Need label.</param>
        /// <param name="gate">Level requirement.</param>
        /// <param name="config">Storekeeper texts, prices and maximum.</param>
        /// <param name="view">The scene's storekeeper offer panel.</param>
        /// <param name="camera">Gameplay camera, to place the panel next to the pad.</param>
        /// <param name="pad">The warehouse pad (its Target Id is the location id).</param>
        /// <param name="escape">The scene's Esc router; may be null (no Esc then).</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        public StorekeeperOfferPresenter(
            IStaffService staff,
            IWalletService wallet,
            IUnlockGate gate,
            IConfigProvider config,
            OfferPanelView view,
            Camera camera,
            ManagePadView pad,
            EscapeRouter escape)
        {
            _staff = staff ?? throw new ArgumentNullException(nameof(staff));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _settings = config?.Staff ?? throw new ArgumentNullException(nameof(config));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _pad = pad != null ? pad : throw new ArgumentNullException(nameof(pad));
            _escape = escape;
            _locationId = pad.TargetId;

            _pad.DwellCompleted += OnDwellCompleted;
            _pad.Left += OnPadLeft;
            _view.ActionClicked += OnHireClicked;
            _view.CloseClicked += Close;
            _staff.Hired += OnHired;
            _wallet.BalanceChanged += OnBalanceChanged;
            _gate.Changed += Refresh;

            _view.Hide();
        }

        /// <inheritdoc />
        public bool TryHandleEscape()
        {
            // Why: a panel hidden because its object is behind the camera stays open but invisible; Esc must not be
            // swallowed by something the player cannot see — it goes on to the pause menu.
            if (!_open || !_view.IsVisible)
            {
                return false;
            }

            Close();
            return true;
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
            _escape?.Remove(this);

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
            _escape?.Push(this);
            ShowOffer(availability);
            _view.Follow(_camera, _pad.PanelAnchor.position);
        }

        /// <summary>Formats every text for the current count and the price of the next storekeeper.</summary>
        private void ShowOffer(HireAvailability availability)
        {
            int hired = _staff.StorekeeperCount(_locationId);
            string title = string.Format(
                _view.CountTitleFormat,
                _settings.StorekeeperTitle,
                hired.ToString(CultureInfo.InvariantCulture),
                _settings.MaxStorekeepers.ToString(CultureInfo.InvariantCulture));
            bool maxed = availability == HireAvailability.Hired;
            string cost = maxed ? null : MoneyFormatter.Format(_staff.GetStorekeeperCost(_locationId));
            _hireLabel = maxed ? null : string.Format(_view.ActionLabelFormat, cost, _settings.StorekeeperTitle);
            _needLabel = maxed ? null : string.Format(_view.NeedLabelFormat, cost);
            string requirement = availability == HireAvailability.Locked
                ? string.Format(_view.RequirementFormat, _settings.StorekeeperRequiredLevel.ToString(CultureInfo.InvariantCulture))
                : null;
            _view.Show(title, _settings.StorekeeperDescription, cost, requirement);
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
                    _view.SetAffordable(false, _view.MaxLabel);
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
            _escape?.Remove(this);
            if (_view != null)
            {
                _view.Hide();
            }
        }

        private void OnHireClicked()
        {
            // Why: on success the Hired event moves the panel on to the next storekeeper; on failure refresh the button.
            if (_open && !_staff.TryHireStorekeeper(_locationId))
            {
                Refresh();
            }
        }

        // Why: a hire changes the count and the next price — the offer is formatted again and stays open.
        private void OnHired(StaffMember member)
        {
            if (_open && member.Role == StaffRole.Storekeeper)
            {
                ShowOffer(_staff.GetStorekeeperAvailability(_locationId));
            }
        }

        private void OnBalanceChanged(Money balance) => Refresh();

        private void OnPadLeft(ManagePadView pad) => Close();
    }
}
