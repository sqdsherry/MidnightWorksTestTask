using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Staff;

namespace AutoService.Services.Staff
{
    /// <summary>
    /// Hiring and running the staff: one worker per point (takes its work spot for good) and one storekeeper per location
    /// (carries boxes from the warehouse to the hungriest point).
    /// </summary>
    /// <remarks>
    /// Kept asymmetry (GDD 2026-09-30): the player never helps a worker on its spot, and workers never carry boxes.
    /// </remarks>
    public interface IStaffService
    {
        /// <summary>Raised after a staff member was hired or restored (its body is already spawned).</summary>
        event Action<StaffMember> Hired;

        /// <summary>All hired staff, in hiring order.</summary>
        IReadOnlyList<StaffMember> Staff { get; }

        /// <summary>Whether a worker can be hired for <paramref name="pointId"/> now.</summary>
        HireAvailability GetWorkerAvailability(string pointId);

        /// <summary>Hiring price of the point's worker; zero when not supported.</summary>
        Money GetWorkerCost(string pointId);

        /// <summary>Pays for and hires the point's worker if it is <see cref="HireAvailability.Available"/>.</summary>
        /// <returns>False (nothing changes) otherwise.</returns>
        bool TryHireWorker(string pointId);

        /// <summary>True from the moment of hiring (also while the worker still walks to the spot).</summary>
        bool HasWorker(string pointId);

        /// <summary>Whether the storekeeper of <paramref name="locationId"/> can be hired now.</summary>
        HireAvailability GetStorekeeperAvailability(string locationId);

        /// <summary>Hiring price of the storekeeper.</summary>
        Money GetStorekeeperCost(string locationId);

        /// <summary>Pays for and hires the storekeeper of the location if it is <see cref="HireAvailability.Available"/>.</summary>
        /// <returns>False (nothing changes) otherwise.</returns>
        bool TryHireStorekeeper(string locationId);

        /// <summary>Spawns the saved worker of a point without payment. Ignored when not supported or already hired.</summary>
        void RestoreWorker(string pointId);

        /// <summary>Spawns the saved storekeeper of a location without payment. Ignored when already hired.</summary>
        void RestoreStorekeeper(string locationId);
    }
}
