using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Points;
using AutoService.Domain.Upgrades;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;

namespace AutoService.Services.Upgrades
{
    /// <summary>
    /// Default <see cref="IUpgradeService"/>: one <see cref="UpgradeTrack"/> per configured kind for every registered point
    /// (also bays built later, through <see cref="IServicePointService.PointRegistered"/>); after every change the point's
    /// multipliers are recomputed from the config formulas (<see cref="ServicePoint.ApplyModifiers"/>).
    /// </summary>
    public sealed class UpgradeService : IUpgradeService, IDisposable
    {
        private readonly IServicePointService _points;
        private readonly IWalletService _wallet;
        private readonly IUnlockGate _gate;
        private readonly IEventBus _eventBus;
        private readonly UpgradeSettings _speed;
        private readonly UpgradeSettings _price;
        private readonly Dictionary<string, PointUpgrades> _upgrades = new Dictionary<string, PointUpgrades>(StringComparer.Ordinal);
        private bool _disposed;

        /// <summary>Creates the service and starts tracking the registered points (and those registered later).</summary>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public UpgradeService(IServicePointService points, IWalletService wallet, IUnlockGate gate, IConfigProvider config, IEventBus eventBus)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            config.TryGetUpgrade(UpgradeKind.Speed, out _speed);
            config.TryGetUpgrade(UpgradeKind.Price, out _price);

            IReadOnlyList<ServicePoint> all = _points.All;
            for (int i = 0; i < all.Count; i++)
            {
                Track(all[i]);
            }

            _points.PointRegistered += Track;
        }

        /// <inheritdoc />
        public event Action<string, UpgradeKind> Upgraded;

        /// <inheritdoc />
        public int GetLevel(string pointId, UpgradeKind kind)
        {
            UpgradeTrack track = Get(pointId).TrackOf(kind);
            return track?.Level ?? 0;
        }

        /// <inheritdoc />
        public Money GetNextCost(string pointId, UpgradeKind kind)
        {
            UpgradeTrack track = Get(pointId).TrackOf(kind);
            return track == null || track.IsMaxed ? Money.Zero : SettingsOf(kind).CostAt(track.Level);
        }

        /// <inheritdoc />
        public UpgradeAvailability GetAvailability(string pointId, UpgradeKind kind) => AvailabilityOf(Get(pointId), kind);

        /// <inheritdoc />
        public bool TryUpgrade(string pointId, UpgradeKind kind)
        {
            if (pointId == null || !_upgrades.TryGetValue(pointId, out PointUpgrades upgrades)
                || AvailabilityOf(upgrades, kind) != UpgradeAvailability.Available)
            {
                return false;
            }

            UpgradeTrack track = upgrades.TrackOf(kind);
            Money cost = SettingsOf(kind).CostAt(track.Level);

            // Why: affordability was just checked, but the wallet stays the single authority on spending.
            if (!_wallet.TrySpend(cost))
            {
                return false;
            }

            track.LevelUp();
            Apply(upgrades);
            Upgraded?.Invoke(pointId, kind);
            _eventBus.Publish(new UpgradePurchasedEvent(pointId, kind, track.Level, cost));
            return true;
        }

        /// <inheritdoc />
        public void Restore(string pointId, UpgradeKind kind, int level)
        {
            if (pointId == null || !_upgrades.TryGetValue(pointId, out PointUpgrades upgrades))
            {
                return;
            }

            UpgradeTrack track = upgrades.TrackOf(kind);
            if (track == null)
            {
                return;
            }

            track.Restore(level);
            Apply(upgrades);
            Upgraded?.Invoke(pointId, kind);
        }

        /// <summary>Stops listening to the point registry. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _points.PointRegistered -= Track;
        }

        private void Track(ServicePoint point)
        {
            string pointId = point.Definition.Id;
            if (_upgrades.ContainsKey(pointId))
            {
                return;
            }

            var upgrades = new PointUpgrades(
                point,
                _speed != null ? new UpgradeTrack(_speed.MaxLevel) : null,
                _price != null ? new UpgradeTrack(_price.MaxLevel) : null);
            _upgrades.Add(pointId, upgrades);
            Apply(upgrades);
        }

        private PointUpgrades Get(string pointId)
        {
            if (pointId == null || !_upgrades.TryGetValue(pointId, out PointUpgrades upgrades))
            {
                throw new ArgumentException("Unknown point '" + pointId + "'.", nameof(pointId));
            }

            return upgrades;
        }

        private UpgradeAvailability AvailabilityOf(PointUpgrades upgrades, UpgradeKind kind)
        {
            UpgradeTrack track = upgrades.TrackOf(kind);
            if (track == null)
            {
                return UpgradeAvailability.NotSupported;
            }

            if (track.IsMaxed)
            {
                return UpgradeAvailability.Maxed;
            }

            UpgradeSettings settings = SettingsOf(kind);
            if (!_gate.IsUnlocked(settings.RequiredLevel))
            {
                return UpgradeAvailability.Locked;
            }

            return _wallet.CanAfford(settings.CostAt(track.Level)) ? UpgradeAvailability.Available : UpgradeAvailability.NotEnoughMoney;
        }

        private UpgradeSettings SettingsOf(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Speed:
                    return _speed;
                case UpgradeKind.Price:
                    return _price;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown upgrade kind.");
            }
        }

        private void Apply(PointUpgrades upgrades)
        {
            double duration = _speed != null ? _speed.DurationMultiplierAt(upgrades.Speed.Level) : 1.0;
            double price = _price != null ? _price.PriceMultiplierAt(upgrades.Price.Level) : 1.0;
            upgrades.Point.ApplyModifiers((float)duration, price);
        }

        /// <summary>The tracks of one point; a track is null when its kind is not configured.</summary>
        private sealed class PointUpgrades
        {
            public PointUpgrades(ServicePoint point, UpgradeTrack speed, UpgradeTrack price)
            {
                Point = point;
                Speed = speed;
                Price = price;
            }

            public ServicePoint Point { get; }

            public UpgradeTrack Speed { get; }

            public UpgradeTrack Price { get; }

            public UpgradeTrack TrackOf(UpgradeKind kind)
            {
                switch (kind)
                {
                    case UpgradeKind.Speed:
                        return Speed;
                    case UpgradeKind.Price:
                        return Price;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown upgrade kind.");
                }
            }
        }
    }
}
