using System;
using AutoService.Domain.Staff;

namespace AutoService.Services.Staff
{
    /// <summary>
    /// Port to the physical staff NPCs. Implemented in Presentation (NavMesh agents); the staff logic only talks to it
    /// by staff id and <see cref="StaffDestination"/>.
    /// </summary>
    public interface IStaffAgents
    {
        /// <summary>Raised once per <see cref="MoveTo"/> when the NPC has reached the destination and faces it.</summary>
        /// <remarks>
        /// Must never be raised synchronously from inside <see cref="Spawn"/>/<see cref="MoveTo"/>/<see cref="SetCarried"/>:
        /// the staff logic calls them in the middle of its own bookkeeping. Raise it from the implementation's tick instead.
        /// </remarks>
        event Action<int> Arrived;

        /// <summary>Creates the NPC's body in the location's staff room.</summary>
        void Spawn(int staffId, StaffRole role, string locationId);

        /// <summary>Walks the NPC to the destination; a new call replaces the previous destination (which never arrives then).</summary>
        void MoveTo(int staffId, StaffDestination destination);

        /// <summary>Shows a box of <paramref name="supplyTypeIdOrEmpty"/> in the NPC's hands, or no box for an empty string.</summary>
        void SetCarried(int staffId, string supplyTypeIdOrEmpty);
    }
}
