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
        Tuning = 1,
        Painting = 2
    }

    /// <summary>
    /// Listens for completed services on cars and applies visual customizations:
    /// tuning upgrades the car model to Car_Sport, and painting changes its body color.
    /// </summary>
    public sealed class CarCustomizationPresenter : IDisposable
    {
        private static readonly Color[] PaintPalette =
        {
            Color.red,
            Color.blue,
            Color.yellow,
            Color.green,
            new Color(1f, 0.5f, 0f),      // Orange
            new Color(0.6f, 0f, 0.8f)     // Purple
        };

        private readonly IEventBus _eventBus;
        private readonly CarVisualCatalog _catalog;
        private readonly IEnumerable<CarAgents> _agents;
        private readonly CarView _sportPrefab;
        private bool _disposed;

        public CarCustomizationPresenter(
            IEventBus eventBus,
            CarVisualCatalog catalog,
            IEnumerable<CarAgents> agents)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _agents = agents ?? throw new ArgumentNullException(nameof(agents));

            if (_catalog.TryGetPrefab("sport", out CarView sport))
            {
                _sportPrefab = sport;
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

            CarView view = FindCarView(evt.CarId);
            if (view == null)
            {
                return;
            }

            switch (serviceType)
            {
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

        private CarView FindCarView(int carId)
        {
            foreach (CarAgents agent in _agents)
            {
                if (agent != null && agent.TryGetCarView(carId, out CarView view))
                {
                    return view;
                }
            }

            return null;
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
