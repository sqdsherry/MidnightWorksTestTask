using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Points;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Events;

namespace AutoService.Services.Points
{
    /// <summary>
    /// Default <see cref="IServicePointService"/>: owns the <see cref="ServicePoint"/> entities, ticks them and turns
    /// accepted orders into money (<see cref="IWalletService.Add"/>) plus an <see cref="OrderAcceptedEvent"/>.
    /// </summary>
    /// <remarks>
    /// Completed services are NOT published here: <see cref="ServiceCompletedEvent"/> needs the car type, which only the
    /// traffic orchestrator knows, so <c>LocationTraffic</c> is its single publisher.
    /// </remarks>
    public sealed class ServicePointService : IServicePointService, ITickable, IDisposable
    {
        private readonly IWalletService _wallet;
        private readonly IEventBus _eventBus;
        private readonly List<ServicePoint> _points = new List<ServicePoint>();
        private readonly Dictionary<string, ServicePoint> _pointsById = new Dictionary<string, ServicePoint>(StringComparer.Ordinal);
        private bool _disposed;

        /// <summary>Creates an empty registry.</summary>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public ServicePointService(IWalletService wallet, IEventBus eventBus)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <inheritdoc />
        public event Action<ServicePoint> PointRegistered;

        /// <inheritdoc />
        public IReadOnlyList<ServicePoint> All => _points;

        /// <summary>Creates and registers a point. Called by the scene entry point while building the scene.</summary>
        /// <returns>The new point entity.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a point with the same id is already registered.</exception>
        public ServicePoint Register(ServicePointDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (_pointsById.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException("Service point '" + definition.Id + "' is already registered.");
            }

            var point = new ServicePoint(definition);
            _points.Add(point);
            _pointsById.Add(definition.Id, point);
            point.OrderAccepted += OnOrderAccepted;
            PointRegistered?.Invoke(point);
            return point;
        }

        /// <inheritdoc />
        public bool TryGet(string pointId, out ServicePoint point)
        {
            if (pointId == null)
            {
                point = null;
                return false;
            }

            return _pointsById.TryGetValue(pointId, out point);
        }

        /// <inheritdoc />
        public bool TryOccupy(string pointId, OccupantKind occupant)
        {
            return TryGet(pointId, out ServicePoint point) && point.TryOccupy(occupant);
        }

        /// <inheritdoc />
        public void Vacate(string pointId, OccupantKind occupant)
        {
            if (TryGet(pointId, out ServicePoint point))
            {
                point.Vacate(occupant);
            }
        }

        /// <inheritdoc />
        public ServicePoint FindAvailable(string locationId, string serviceTypeId)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                ServicePoint point = _points[i];
                ServicePointDefinition definition = point.Definition;
                if (point.IsAvailable
                    && string.Equals(definition.ServiceTypeId, serviceTypeId, StringComparison.Ordinal)
                    && string.Equals(definition.LocationId, locationId, StringComparison.Ordinal))
                {
                    return point;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                _points[i].Tick(deltaTime);
            }
        }

        /// <summary>Stops listening to the points. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = 0; i < _points.Count; i++)
            {
                _points[i].OrderAccepted -= OnOrderAccepted;
            }
        }

        // Why: GDD §4.2 — the payment happens at the moment of acceptance, not when the service finishes.
        private void OnOrderAccepted(ServicePoint point, Money price)
        {
            _wallet.Add(price);

            ServicePointDefinition definition = point.Definition;
            _eventBus.Publish(new OrderAcceptedEvent(definition.Id, definition.ServiceTypeId, definition.Kind, price));
        }
    }
}
