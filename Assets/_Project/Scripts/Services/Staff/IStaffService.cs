using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Staff;

namespace AutoService.Services.Staff
{
    /// <summary>
    /// Hiring and running the staff: one worker per point (takes its work spot for good) and up to
    /// <c>StaffSettings.MaxStorekeepers</c> storekeepers per location (carry boxes from the warehouse to the hungriest points;
    /// two storekeepers never restock the same point at once).
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

        /// <summary>Whether the next storekeeper of <paramref name="locationId"/> can be hired now; <see cref="HireAvailability.Hired"/> at the maximum.</summary>
        HireAvailability GetStorekeeperAvailability(string locationId);

        /// <summary>Hiring price of the next storekeeper (it grows with every storekeeper hired); zero when not supported.</summary>
        Money GetStorekeeperCost(string locationId);

        /// <summary>Storekeepers hired in <paramref name="locationId"/>.</summary>
        int StorekeeperCount(string locationId);

        /// <summary>Pays for and hires the next storekeeper of the location if it is <see cref="HireAvailability.Available"/>.</summary>
        /// <returns>False (nothing changes) otherwise.</returns>
        bool TryHireStorekeeper(string locationId);

        /// <summary>Spawns the saved worker of a point without payment. Ignored when not supported or already hired.</summary>
        void RestoreWorker(string pointId);

        /// <summary>
        /// Spawns one saved storekeeper of a location without payment (call it once per saved storekeeper).
        /// Ignored at the maximum or without staff settings.
        /// </summary>
        void RestoreStorekeeper(string locationId);
    }
}
