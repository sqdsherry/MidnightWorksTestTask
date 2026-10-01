using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Points;
using AutoService.Domain.Supplies;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;

namespace AutoService.Services.Supplies
{
    /// <summary>
    /// Default <see cref="ISupplyService"/>: tracks every point with a stock (also bays built later, through
    /// <see cref="IServicePointService.PointRegistered"/>), sells boxes and announces deliveries and empty stocks.
    /// </summary>
    /// <remarks>
    /// Lookups are linear scans over the location's points with no allocations, so the storekeeper may ask every frame.
    /// </remarks>
    public sealed class SupplyService : ISupplyService, IDisposable
    {
        private readonly IServicePointService _points;
        private readonly IWalletService _wallet;
        private readonly IConfigProvider _config;
        private readonly IEventBus _eventBus;
        private readonly List<StockEntry> _entries = new List<StockEntry>();
        private readonly Dictionary<string, StockEntry> _entriesByPoint = new Dictionary<string, StockEntry>(StringComparer.Ordinal);
        private readonly Dictionary<SupplyStock, StockEntry> _entriesByStock = new Dictionary<SupplyStock, StockEntry>();
        private bool _disposed;

        /// <summary>Creates the service and starts tracking the registered points (and those registered later).</summary>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public SupplyService(IServicePointService points, IWalletService wallet, IConfigProvider config, IEventBus eventBus)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            IReadOnlyList<ServicePoint> all = _points.All;
            for (int i = 0; i < all.Count; i++)
            {
                Track(all[i]);
            }

            _points.PointRegistered += Track;
        }

        /// <inheritdoc />
        public ServicePoint FindHungriest(string locationId) => FindHungriest(locationId, null, 0);

        /// <inheritdoc />
        public ServicePoint FindRestockTarget(string locationId, in SupplyBox box)
        {
            return box.IsNone ? null : FindHungriest(locationId, box.SupplyTypeId, box.Units);
        }

        /// <inheritdoc />
        public Money GetBoxPrice(string supplyTypeId)
        {
            if (!_config.TryGetSupplyType(supplyTypeId, out SupplyTypeSettings settings))
            {
                throw new ArgumentException("Unknown supply type '" + supplyTypeId + "'.", nameof(supplyTypeId));
            }

            return settings.BoxPrice;
        }

        /// <inheritdoc />
        public bool TryBuyBoxForHungriest(string locationId, bool byPlayer, out SupplyBox box, out ServicePoint target)
        {
            box = SupplyBox.None;
            target = FindHungriest(locationId);
            if (target == null || !_config.TryGetSupplyType(target.Supply.SupplyTypeId, out SupplyTypeSettings settings))
            {
                target = null;
                return false;
            }

            if (!_wallet.TrySpend(settings.BoxPrice))
            {
                return false;
            }

            box = new SupplyBox(settings.Id, settings.UnitsPerBox);
            _eventBus.Publish(new BoxBoughtEvent(locationId, settings.Id, settings.BoxPrice, byPlayer));
            return true;
        }

        /// <inheritdoc />
        public bool CanDeliver(in SupplyBox box, string pointId)
        {
            return !box.IsNone
                && pointId != null
                && _entriesByPoint.TryGetValue(pointId, out StockEntry entry)
                && string.Equals(entry.Stock.SupplyTypeId, box.SupplyTypeId, StringComparison.Ordinal)
                && entry.Stock.CanAdd(box.Units);
        }

        /// <inheritdoc />
        public bool TryDeliver(in SupplyBox box, string pointId, bool byPlayer)
        {
            if (!CanDeliver(box, pointId))
            {
                return false;
            }

            _entriesByPoint[pointId].Stock.Add(box.Units);
            _eventBus.Publish(new SupplyDeliveredEvent(pointId, box.SupplyTypeId, box.Units, byPlayer));
            return true;
        }

        /// <inheritdoc />
        public void MarkIncoming(string pointId, int units)
        {
            if (pointId != null && units > 0 && _entriesByPoint.TryGetValue(pointId, out StockEntry entry))
            {
                entry.Incoming += units;
            }
        }

        /// <inheritdoc />
        public void ClearIncoming(string pointId, int units)
        {
            if (pointId != null && units > 0 && _entriesByPoint.TryGetValue(pointId, out StockEntry entry))
            {
                entry.Incoming = Math.Max(0, entry.Incoming - units);
            }
        }

        /// <summary>Stops listening to the points and their stocks. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _points.PointRegistered -= Track;
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].Stock.Changed -= OnStockChanged;
            }
        }

        /// <param name="locationId">Location to search.</param>
        /// <param name="supplyTypeId">Only points of this consumable, or null for any.</param>
        /// <param name="units">Units that must fit, or 0 for "one box of the point's own consumable".</param>
        private ServicePoint FindHungriest(string locationId, string supplyTypeId, int units)
        {
            StockEntry best = null;
            float bestFill = float.MaxValue;
            for (int i = 0; i < _entries.Count; i++)
            {
                StockEntry entry = _entries[i];
                SupplyStock stock = entry.Stock;
                if (!string.Equals(entry.Point.Definition.LocationId, locationId, StringComparison.Ordinal)
                    || (supplyTypeId != null && !string.Equals(stock.SupplyTypeId, supplyTypeId, StringComparison.Ordinal)))
                {
                    continue;
                }

                // Why: a box already on its way counts as stock, so the next box goes elsewhere.
                int expected = stock.Current + entry.Incoming;
                if (stock.Capacity - expected < (units > 0 ? units : entry.UnitsPerBox))
                {
                    continue;
                }

                float fill = (float)expected / stock.Capacity;
                if (fill < bestFill)
                {
                    best = entry;
                    bestFill = fill;
                }
            }

            return best?.Point;
        }

        private void Track(ServicePoint point)
        {
            SupplyStock stock = point.Supply;
            if (stock == null || _entriesByPoint.ContainsKey(point.Definition.Id))
            {
                return;
            }

            // Why: a consumable missing from the config cannot be bought; such a stock is never offered for restocking.
            int unitsPerBox = _config.TryGetSupplyType(stock.SupplyTypeId, out SupplyTypeSettings settings)
                ? settings.UnitsPerBox
                : int.MaxValue;
            var entry = new StockEntry(point, unitsPerBox);
            _entries.Add(entry);
            _entriesByPoint.Add(point.Definition.Id, entry);
            _entriesByStock.Add(stock, entry);
            stock.Changed += OnStockChanged;
        }

        private void OnStockChanged(SupplyStock stock)
        {
            if (stock.IsEmpty && _entriesByStock.TryGetValue(stock, out StockEntry entry))
            {
                _eventBus.Publish(new SupplyDepletedEvent(entry.Point.Definition.Id, stock.SupplyTypeId));
            }
        }

        /// <summary>A tracked stock: its point, the size of its box and the storekeeper units on their way.</summary>
        private sealed class StockEntry
        {
            public StockEntry(ServicePoint point, int unitsPerBox)
            {
                Point = point;
                Stock = point.Supply;
                UnitsPerBox = unitsPerBox;
            }

            public ServicePoint Point { get; }

            public SupplyStock Stock { get; }

            public int UnitsPerBox { get; }

            public int Incoming { get; set; }
        }
    }
}
