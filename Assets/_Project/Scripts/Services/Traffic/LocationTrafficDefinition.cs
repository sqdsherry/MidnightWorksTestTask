using System;

namespace AutoService.Services.Traffic
{
    /// <summary>Immutable layout facts of one location needed by <see cref="LocationTraffic"/> (read from the scene).</summary>
    public sealed class LocationTrafficDefinition
    {
        /// <summary>Creates and validates the definition.</summary>
        /// <param name="locationId">Location id; must match <c>LocationId</c> of the location's points.</param>
        /// <param name="barrierPointId">Id of the registered parking barrier point.</param>
        /// <param name="queueCapacity">Number of entry queue slots (&gt;= 1).</param>
        /// <param name="parkingCapacity">Number of parking slots (&gt;= 0).</param>
        /// <exception cref="ArgumentException">Thrown for empty ids or out-of-range capacities.</exception>
        public LocationTrafficDefinition(string locationId, string barrierPointId, int queueCapacity, int parkingCapacity)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                throw new ArgumentException("Location id must not be empty.", nameof(locationId));
            }

            if (string.IsNullOrWhiteSpace(barrierPointId))
            {
                throw new ArgumentException("Barrier point id must not be empty.", nameof(barrierPointId));
            }

            if (queueCapacity < 1)
            {
                throw new ArgumentException("A location needs at least one queue slot.", nameof(queueCapacity));
            }

            if (parkingCapacity < 0)
            {
                throw new ArgumentException("Parking capacity must be non-negative.", nameof(parkingCapacity));
            }

            LocationId = locationId;
            BarrierPointId = barrierPointId;
            QueueCapacity = queueCapacity;
            ParkingCapacity = parkingCapacity;
        }

        /// <summary>Location id.</summary>
        public string LocationId { get; }

        /// <summary>Id of the parking barrier point.</summary>
        public string BarrierPointId { get; }

        /// <summary>Number of entry queue slots.</summary>
        public int QueueCapacity { get; }

        /// <summary>Number of parking slots.</summary>
        public int ParkingCapacity { get; }
    }
}
