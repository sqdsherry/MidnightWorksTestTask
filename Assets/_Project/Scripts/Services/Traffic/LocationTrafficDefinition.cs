using System;

namespace AutoService.Services.Traffic
{
    /// <summary>Immutable layout facts of one location needed by <see cref="LocationTraffic"/> (read from the scene).</summary>
    public sealed class LocationTrafficDefinition
    {
        /// <summary>Creates and validates the definition.</summary>
        /// <param name="locationId">Location id; must match <c>LocationId</c> of the location's points.</param>
        /// <param name="mainEntranceId">Id of the parking entrance barrier reached from the road (barrier 1).</param>
        /// <param name="serviceEntranceId">Id of the parking entrance barrier reached after a service (barrier 2).</param>
        /// <param name="queueCapacity">Number of entry queue slots (&gt;= 1).</param>
        /// <param name="parkingCapacity">Number of parking slots (&gt;= 0).</param>
        /// <param name="serviceBufferCapacity">
        /// Number of buffer slots in front of EVERY service point of the location (&gt;= 0; 0 = no buffer).
        /// </param>
        /// <exception cref="ArgumentException">Thrown for empty or equal entrance ids, empty location id or out-of-range capacities.</exception>
        public LocationTrafficDefinition(
            string locationId,
            string mainEntranceId,
            string serviceEntranceId,
            int queueCapacity,
            int parkingCapacity,
            int serviceBufferCapacity)
        {
            RequireId(locationId, nameof(locationId));
            if (mainEntranceId != null || serviceEntranceId != null)
            {
                RequireId(mainEntranceId, nameof(mainEntranceId));
                RequireId(serviceEntranceId, nameof(serviceEntranceId));
                if (string.Equals(mainEntranceId, serviceEntranceId, StringComparison.Ordinal))
                {
                    throw new ArgumentException("The two parking entrances must be different points.", nameof(serviceEntranceId));
                }
            }

            if (queueCapacity < 1)
            {
                throw new ArgumentException("A location needs at least one queue slot.", nameof(queueCapacity));
            }

            if (parkingCapacity < 0)
            {
                throw new ArgumentException("Parking capacity must be non-negative.", nameof(parkingCapacity));
            }

            if (serviceBufferCapacity < 0)
            {
                throw new ArgumentException("Service buffer capacity must be non-negative.", nameof(serviceBufferCapacity));
            }

            LocationId = locationId;
            MainEntranceId = mainEntranceId;
            ServiceEntranceId = serviceEntranceId;
            QueueCapacity = queueCapacity;
            ParkingCapacity = parkingCapacity;
            ServiceBufferCapacity = serviceBufferCapacity;
        }

        /// <summary>Location id.</summary>
        public string LocationId { get; }

        /// <summary>Id of the parking entrance barrier reached from the road (kind <c>Barrier</c>).</summary>
        public string MainEntranceId { get; }

        /// <summary>Id of the parking entrance barrier reached after a service (kind <c>Barrier</c>).</summary>
        public string ServiceEntranceId { get; }

        /// <summary>Number of entry queue slots.</summary>
        public int QueueCapacity { get; }

        /// <summary>Number of parking slots.</summary>
        public int ParkingCapacity { get; }

        /// <summary>Number of buffer slots in front of every service point (0 = no buffer).</summary>
        public int ServiceBufferCapacity { get; }

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Id must not be empty.", parameterName);
            }
        }
    }
}
