using System;
using AutoService.Services.Building;
using AutoService.Services.Events;
using AutoService.Services.Staff;
using AutoService.Services.Upgrades;

namespace AutoService.Services.Save
{
    /// <summary>
    /// Asks for a save after important player actions — a build, an upgrade, a hire — so they are not lost before the
    /// next autosave. <see cref="SaveCoordinator.RequestSave"/> merges several requests of one frame into one write.
    /// </summary>
    public sealed class SaveTriggers : IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly SaveCoordinator _coordinator;
        private bool _disposed;

        /// <summary>Subscribes to the events.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public SaveTriggers(IEventBus eventBus, SaveCoordinator coordinator)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

            _eventBus.Subscribe<BuildCompletedEvent>(OnBuilt);
            _eventBus.Subscribe<UpgradePurchasedEvent>(OnUpgraded);
            _eventBus.Subscribe<StaffHiredEvent>(OnHired);
        }

        /// <summary>Unsubscribes. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _eventBus.Unsubscribe<BuildCompletedEvent>(OnBuilt);
            _eventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgraded);
            _eventBus.Unsubscribe<StaffHiredEvent>(OnHired);
        }

        private void OnBuilt(BuildCompletedEvent gameEvent) => _coordinator.RequestSave();

        private void OnUpgraded(UpgradePurchasedEvent gameEvent) => _coordinator.RequestSave();

        private void OnHired(StaffHiredEvent gameEvent) => _coordinator.RequestSave();
    }
}
