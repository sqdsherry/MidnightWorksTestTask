using System;
using System.Collections.Generic;
using AutoService.Domain.Points;
using AutoService.Presentation.Traffic;
using AutoService.Services.Staff;
using AutoService.Services.Supplies;

namespace AutoService.Presentation.Points
{
    /// <summary>
    /// Gives every registered point view (the start points and the bays built later) the staff and supply services:
    /// the box hand-over on click and the "a worker holds this spot" rule.
    /// </summary>
    /// <remarks>
    /// Why a binder: the points are registered by <see cref="PointRegistrar"/> before the staff and supply services exist
    /// (the traffic needs the entrances first), so the views are completed afterwards and on every later registration.
    /// </remarks>
    public sealed class PointStaffSuppliesBinder : IDisposable
    {
        private readonly PointRegistrar _registrar;
        private readonly LocationLayout _layout;
        private readonly ISupplyService _supplies;
        private readonly IPlayerCarry _carry;
        private readonly IStaffService _staff;
        private bool _disposed;

        /// <summary>Completes the views registered so far and listens for new ones.</summary>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public PointStaffSuppliesBinder(
            PointRegistrar registrar,
            LocationLayout layout,
            ISupplyService supplies,
            IPlayerCarry carry,
            IStaffService staff)
        {
            _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
            _layout = layout != null ? layout : throw new ArgumentNullException(nameof(layout));
            _supplies = supplies ?? throw new ArgumentNullException(nameof(supplies));
            _carry = carry ?? throw new ArgumentNullException(nameof(carry));
            _staff = staff ?? throw new ArgumentNullException(nameof(staff));

            IReadOnlyList<ServicePointView> views = _registrar.Views;
            for (int i = 0; i < views.Count; i++)
            {
                Bind(views[i]);
            }

            _registrar.Registered += OnRegistered;
        }

        /// <summary>Stops listening to the registrar. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _registrar.Registered -= OnRegistered;
        }

        private void OnRegistered(ServicePoint point, ServicePointView view) => Bind(view);

        private void Bind(ServicePointView view)
        {
            if (_layout.TryGetManagePad(view.PointId, out ManagePadView pad))
            {
                view.ConstructStaffSupplies(_supplies, _carry, _staff, pad.ApproachPoint);
            }
        }
    }
}
