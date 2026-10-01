using System;
using AutoService.Domain.Points;
using AutoService.Services.Core;
using UnityEngine;

namespace AutoService.Presentation.Points
{
    /// <summary>
    /// Shows a <see cref="ServicePoint"/>'s state on its <see cref="ServicePointView"/>: HUD every tick (cheap, change-checked)
    /// and, for barriers, opens the arm while the order is serviced / the car drives through.
    /// </summary>
    public sealed class ServicePointPresenter : ITickable, IDisposable
    {
        private readonly ServicePoint _point;
        private readonly ServicePointHud _hud;
        private readonly BarrierArm _barrierArm;
        private bool _disposed;

        /// <summary>Creates the presenter and listens to the point's state.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public ServicePointPresenter(ServicePointView view, ServicePoint point)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            _point = point ?? throw new ArgumentNullException(nameof(point));
            _hud = view.Hud;

            // Why: optional component on the same object; looked up once here, never per frame.
            view.TryGetComponent(out _barrierArm);

            _point.StateChanged += OnStateChanged;
            OnStateChanged(_point);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_hud != null)
            {
                _hud.Render(_point.State, _point.Progress, _point.IsOccupied);
            }

            if (_barrierArm != null)
            {
                // Why: the arm is cosmetic and should finish moving even if the game gets paused mid-animation.
                _barrierArm.Animate(Time.unscaledDeltaTime);
            }
        }

        /// <summary>Stops listening to the point. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _point.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(ServicePoint point)
        {
            if (_barrierArm != null)
            {
                _barrierArm.SetOpen(point.State == ServicePointState.Servicing || point.State == ServicePointState.Clearing);
            }
        }
    }
}
