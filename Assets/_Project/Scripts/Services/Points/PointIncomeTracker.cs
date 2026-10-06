using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Points;
using AutoService.Services.Core;
using AutoService.Services.Events;

namespace AutoService.Services.Points
{
    /// <summary>
    /// "Income per minute" of every point: the money of its accepted orders over the last minute, in a ring of
    /// <see cref="BucketCount"/> buckets of <see cref="BucketSeconds"/> seconds.
    /// </summary>
    /// <remarks>
    /// All points share one clock (the current bucket index), advanced by <see cref="Tick"/> with scaled time — the window
    /// stands still while the game is paused. The buckets of a point are allocated once, when it is registered; ticking
    /// and recording allocate nothing. The window is 50–60 s long (the current bucket is still filling).
    /// </remarks>
    public sealed class PointIncomeTracker : ITickable, IDisposable
    {
        /// <summary>Number of buckets in the window.</summary>
        public const int BucketCount = 6;

        /// <summary>Length of one bucket, in seconds.</summary>
        public const float BucketSeconds = 10f;

        private readonly IServicePointService _points;
        private readonly IEventBus _eventBus;
        private readonly Action<OrderAcceptedEvent> _onOrderAccepted;
        private readonly List<long[]> _allBuckets = new List<long[]>();
        private readonly Dictionary<string, long[]> _bucketsByPoint = new Dictionary<string, long[]>(StringComparer.Ordinal);

        private int _current;
        private float _bucketTime;
        private bool _disposed;

        /// <summary>Creates the tracker for the registered points (and those registered later) and starts listening to orders.</summary>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public PointIncomeTracker(IServicePointService points, IEventBus eventBus)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            IReadOnlyList<ServicePoint> all = _points.All;
            for (int i = 0; i < all.Count; i++)
            {
                Track(all[i]);
            }

            _points.PointRegistered += Track;

            // Why: one cached delegate, so Unsubscribe removes exactly what Subscribe added.
            _onOrderAccepted = OnOrderAccepted;
            _eventBus.Subscribe(_onOrderAccepted);
        }

        /// <summary>Money the point earned over the last minute; zero for an unknown point.</summary>
        public Money GetIncomePerMinute(string pointId)
        {
            if (pointId == null || !_bucketsByPoint.TryGetValue(pointId, out long[] buckets))
            {
                return Money.Zero;
            }

            long sum = 0L;
            for (int i = 0; i < buckets.Length; i++)
            {
                sum += buckets[i];
            }

            return new Money(sum);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f))
            {
                return;
            }

            _bucketTime += deltaTime;

            // Why: a long frame (or a huge dt in tests) may skip several buckets; more than the whole ring clears it all.
            int steps = 0;
            while (_bucketTime >= BucketSeconds && steps < BucketCount)
            {
                _bucketTime -= BucketSeconds;
                _current = (_current + 1) % BucketCount;
                ClearCurrentBuckets();
                steps++;
            }

            if (_bucketTime >= BucketSeconds)
            {
                _bucketTime %= BucketSeconds;
            }
        }

        /// <summary>Stops listening to orders and the point registry. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _points.PointRegistered -= Track;
            _eventBus.Unsubscribe(_onOrderAccepted);
        }

        private void Track(ServicePoint point)
        {
            string pointId = point.Definition.Id;
            if (_bucketsByPoint.ContainsKey(pointId))
            {
                return;
            }

            var buckets = new long[BucketCount];
            _bucketsByPoint.Add(pointId, buckets);
            _allBuckets.Add(buckets);
        }

        private void ClearCurrentBuckets()
        {
            for (int i = 0; i < _allBuckets.Count; i++)
            {
                _allBuckets[i][_current] = 0L;
            }
        }

        private void OnOrderAccepted(OrderAcceptedEvent order)
        {
            if (order.PointId != null && _bucketsByPoint.TryGetValue(order.PointId, out long[] buckets))
            {
                buckets[_current] += order.Price.Amount;
            }
        }
    }
}
