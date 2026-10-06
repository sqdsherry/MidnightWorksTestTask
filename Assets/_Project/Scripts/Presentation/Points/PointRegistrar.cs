using System;
using System.Collections.Generic;
using AutoService.Domain.Points;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Points;

namespace AutoService.Presentation.Points
{
    /// <summary>
    /// The one way a scene point becomes a working point: registers its <see cref="ServicePoint"/> (from the view's
    /// service type config), constructs the <see cref="ServicePointView"/> and creates its <see cref="ServicePointPresenter"/>.
    /// Used for the points that work from the start and for bays built later.
    /// </summary>
    /// <remarks>
    /// Owns (ticks and disposes) every presenter it creates. Modules that attach more to a point (staff, supplies) listen to
    /// <see cref="Registered"/> and read <see cref="Views"/> for the points registered before them.
    /// </remarks>
    public sealed class PointRegistrar : ITickable, IDisposable
    {
        private readonly ServicePointService _points;
        private readonly IConfigProvider _config;
        private readonly IGameLogger _logger;
        private readonly List<ServicePointPresenter> _presenters = new List<ServicePointPresenter>();
        private readonly List<ServicePointView> _views = new List<ServicePointView>();
        private bool _disposed;

        /// <summary>Creates the registrar.</summary>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public PointRegistrar(ServicePointService points, IConfigProvider config, IGameLogger logger)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Raised after a point was registered and its view constructed.</summary>
        public event Action<ServicePoint, ServicePointView> Registered;

        /// <summary>Views of all registered points, in registration order.</summary>
        public IReadOnlyList<ServicePointView> Views => _views;

        /// <summary>Registers the point of <paramref name="view"/> in <paramref name="locationId"/>.</summary>
        /// <param name="view">Scene point.</param>
        /// <param name="locationId">Location the point belongs to.</param>
        /// <param name="expectedKind">Kind its service type must have (entrances are barriers, bays are services).</param>
        /// <param name="point">The new point, or null on failure.</param>
        /// <returns>False (logged, nothing registered) for an unknown or wrong-kind service type, an empty or duplicate id.</returns>
        public bool TryRegister(ServicePointView view, string locationId, PointKind expectedKind, out ServicePoint point)
        {
            point = null;
            if (view == null)
            {
                return false;
            }

            if (!_config.TryGetServiceType(view.ServiceTypeId, out ServiceTypeSettings settings))
            {
                _logger.Error("[Points] ServicePointView '" + view.name + "': unknown service type '" + view.ServiceTypeId
                    + "'; add it to GameConfig. Point skipped.");
                return false;
            }

            if (settings.Kind != expectedKind)
            {
                _logger.Error("[Points] ServicePointView '" + view.name + "': service type '" + settings.Id + "' is "
                    + settings.Kind + ", expected " + expectedKind + ". Point skipped.");
                return false;
            }

            try
            {
                point = _points.Register(settings.CreatePointDefinition(view.PointId, locationId));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                // Empty or duplicate point id.
                _logger.Error("[Points] ServicePointView '" + view.name + "': " + exception.Message + " Point skipped.");
                return false;
            }

            view.Construct(_points);
            _presenters.Add(new ServicePointPresenter(view, point));
            _views.Add(view);
            Registered?.Invoke(point, view);
            return true;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _presenters.Count; i++)
            {
                _presenters[i].Tick(deltaTime);
            }
        }

        /// <summary>Disposes the created presenters. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int i = 0; i < _presenters.Count; i++)
            {
                _presenters[i].Dispose();
            }

            _presenters.Clear();
        }
    }
}
