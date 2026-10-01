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
        public ServiceCompletedEvent(string pointId, string serviceTypeId, PointKind kind, int carId, string carTypeId)
        {
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

        /// <summary>Barrier (parking fee paid, the car leaves the lot) or real service (car leaves).</summary>
        public PointKind Kind { get; }

        /// <summary>Runtime id of the served car.</summary>
        public int CarId { get; }

        /// <summary>Type id of the served car.</summary>
        public string CarTypeId { get; }
    }
}
