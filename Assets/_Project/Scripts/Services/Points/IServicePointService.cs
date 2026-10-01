using System;
using System.Collections.Generic;
using AutoService.Domain.Points;

namespace AutoService.Services.Points
{
    /// <summary>
    /// Registry of all service points of the scene (every location) and the entry point for work spot occupancy.
    /// </summary>
    public interface IServicePointService
    {
        /// <summary>Raised after a point was registered (at the start or when a bay gets built later).</summary>
        event Action<ServicePoint> PointRegistered;

        /// <summary>All registered points, in registration order.</summary>
        IReadOnlyList<ServicePoint> All { get; }

        /// <summary>Looks up a point by id.</summary>
        /// <returns>False (and null) for an unknown id.</returns>
        bool TryGet(string pointId, out ServicePoint point);

        /// <summary>Puts <paramref name="occupant"/> on the point's work spot.</summary>
        /// <returns>False for an unknown point or when someone else holds the spot.</returns>
        bool TryOccupy(string pointId, OccupantKind occupant);

        /// <summary>Frees the point's work spot if <paramref name="occupant"/> holds it. Unknown points are ignored.</summary>
        void Vacate(string pointId, OccupantKind occupant);

        /// <summary>Free (Idle) service point of the given type in the location, or null. First by registration order.</summary>
        ServicePoint FindAvailable(string locationId, string serviceTypeId);
    }
}
