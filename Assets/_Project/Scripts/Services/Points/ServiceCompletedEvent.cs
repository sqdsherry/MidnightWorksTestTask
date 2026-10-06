using AutoService.Domain.Points;

namespace AutoService.Services.Points
{
    /// <summary>
    /// Bus event: a car has been served at a point (XP in module 07). Raised for barriers too — filter by <see cref="Kind"/>.
    /// </summary>
    /// <remarks>
    /// Why it is published by <c>LocationTraffic</c> and not by <see cref="ServicePointService"/>: the point only knows the
    /// car id, while <see cref="CarTypeId"/> is known by the traffic orchestrator that owns the car. A single publisher
    /// guarantees exactly one event per served car.
    /// </remarks>
    public readonly struct ServiceCompletedEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="locationId">Location of the point; car ids are unique only within a location.</param>
        public ServiceCompletedEvent(string pointId, string serviceTypeId, PointKind kind, int carId, string carTypeId, string locationId = null)
        {
            LocationId = locationId;
            PointId = pointId;
            ServiceTypeId = serviceTypeId;
            Kind = kind;
            CarId = carId;
            CarTypeId = carTypeId;
        }

        /// <summary>Id of the point that served the car.</summary>
        public string PointId { get; }

        /// <summary>Service type of the point.</summary>
        public string ServiceTypeId { get; }

        /// <summary>Barrier (parking fee paid, the car drives to its slot) or real service.</summary>
        public PointKind Kind { get; }

        /// <summary>Location of the point. Car ids start from 0 in every location, so a car is identified by both.</summary>
        public string LocationId { get; }

        /// <summary>Runtime id of the served car (unique within <see cref="LocationId"/> only).</summary>
        public int CarId { get; }

        /// <summary>Type id of the served car.</summary>
        public string CarTypeId { get; }
    }
}
