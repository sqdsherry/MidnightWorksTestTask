using System;
using System.Collections.Generic;
using AutoService.Services.Events;
using AutoService.Services.Points;
using UnityEngine;

namespace AutoService.Presentation.Traffic
{
    public enum ServiceType
    {
        None = 0,
        Tires = 1,
        Tuning = 2,
        Painting = 3
    }

    /// <summary>
    /// Listens for completed services on cars and applies visual customizations:
    /// tires upgrades rims, tuning upgrades the car model to Car_Sport, and painting changes its body color.
    /// Modifications accumulate across services and reset when the car returns to the pool.
    /// </summary>
    public sealed class CarCustomizationPresenter : IDisposable
    {
        // Why: no red, orange or green — those are the stock body colors (sedan, sport, SUV), and a car repainted
        // into almost the same color looked as if the paint shop had done nothing.
        private static readonly Color[] PaintPalette =
        {
            new Color(0.1f, 0.5f, 1f),      // Electric Neon Blue
            new Color(0.7f, 0.15f, 0.95f),  // Deep Neon Purple
            new Color(1f, 0.85f, 0.1f),     // Racing Yellow
            new Color(0.1f, 0.9f, 0.9f),    // Bright Cyan
            new Color(0.95f, 0.2f, 0.6f),   // Hot Pink
            new Color(0.95f, 0.95f, 0.95f)  // Pearl White
        };

        private readonly IEventBus _eventBus;
        private readonly CarVisualCatalog _catalog;
        private readonly IReadOnlyDictionary<string, CarAgents> _agents;
        private readonly CarView _sportPrefab;
        private readonly GameObject _darkWheelPrefab;
        private bool _disposed;

        public CarCustomizationPresenter(
            IEventBus eventBus,
            CarVisualCatalog catalog,
            IReadOnlyDictionary<string, CarAgents> agents)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _agents = agents ?? throw new ArgumentNullException(nameof(agents));

            if (_catalog.TryGetPrefab("sport", out CarView sport))
            {
                _sportPrefab = sport;
            }

            // Why: racing wheels have orange rims, clearly different from the stock ones; the dark wheels differ only
            // by a slightly darker rim and the change was not noticeable.
            _darkWheelPrefab = Resources.Load<GameObject>("wheel-racing");
            if (_darkWheelPrefab == null)
            {
                _darkWheelPrefab = Resources.Load<GameObject>("wheel-dark");
            }

            _eventBus.Subscribe<ServiceCompletedEvent>(OnServiceCompleted);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _eventBus.Unsubscribe<ServiceCompletedEvent>(OnServiceCompleted);
        }

        private void OnServiceCompleted(ServiceCompletedEvent evt)
        {
            ServiceType serviceType = ResolveServiceType(evt.PointId, evt.ServiceTypeId);
            if (serviceType == ServiceType.None)
            {
                return;
            }

            CarView view = FindCarView(evt.LocationId, evt.CarId);
            if (view == null)
            {
                return;
            }

            switch (serviceType)
            {
                case ServiceType.Tires:
                    ApplyTires(view);
                    break;
                case ServiceType.Tuning:
                    ApplyTuning(view);
                    break;
                case ServiceType.Painting:
                    ApplyPainting(view);
                    break;
            }
        }

        private static ServiceType ResolveServiceType(string pointId, string serviceTypeId)
        {
            if ((pointId != null && pointId.IndexOf("tires", StringComparison.OrdinalIgnoreCase) >= 0)
                || (serviceTypeId != null && serviceTypeId.IndexOf("tires", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return ServiceType.Tires;
            }

            if ((pointId != null && pointId.IndexOf("tuning", StringComparison.OrdinalIgnoreCase) >= 0)
                || (serviceTypeId != null && serviceTypeId.IndexOf("tuning", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return ServiceType.Tuning;
            }

            if ((pointId != null && pointId.IndexOf("paint", StringComparison.OrdinalIgnoreCase) >= 0)
                || (serviceTypeId != null && serviceTypeId.IndexOf("paint", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return ServiceType.Painting;
            }

            return ServiceType.None;
        }

        // Why: car ids start from 0 in every location — searching all locations by id alone found a car with the same
        // id on location 1 and painted it instead of the one that left the location 2 paint shop.
        private CarView FindCarView(string locationId, int carId)
        {
            if (locationId != null
                && _agents.TryGetValue(locationId, out CarAgents agents)
                && agents != null
                && agents.TryGetCarView(carId, out CarView view))
            {
                return view;
            }

            return null;
        }

        private void ApplyTires(CarView view)
        {
            view.ApplyTiresUpgrade(_darkWheelPrefab);
        }

        private void ApplyTuning(CarView view)
        {
            if (_sportPrefab != null)
            {
                view.SetSportModel(_sportPrefab);
            }
        }

        private void ApplyPainting(CarView view)
        {
            Color color = PaintPalette[UnityEngine.Random.Range(0, PaintPalette.Length)];
            view.SetBodyColor(color);
        }
    }
}
