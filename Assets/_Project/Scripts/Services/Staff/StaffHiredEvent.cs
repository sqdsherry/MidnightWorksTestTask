using AutoService.Domain.Staff;

namespace AutoService.Services.Staff
{
    /// <summary>
    /// Bus event: the player hired a staff member (XP, onboarding, sound). Not published for staff restored from a save.
    /// Published by <see cref="StaffService"/>.
    /// </summary>
    public readonly struct StaffHiredEvent
    {
        /// <summary>Creates the event.</summary>
        public StaffHiredEvent(StaffRole role, string locationId, string pointId)
        {
            Role = role;
            LocationId = locationId;
            PointId = pointId;
        }

        /// <summary>Job of the hired NPC.</summary>
        public StaffRole Role { get; }

        /// <summary>Location it works in.</summary>
        public string LocationId { get; }

        /// <summary>Point of a worker; null for the storekeeper.</summary>
        public string PointId { get; }
    }
}
