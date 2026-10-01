namespace AutoService.Domain.Staff
{
    /// <summary>Job of a hired NPC.</summary>
    public enum StaffRole
    {
        /// <summary>Stands on the work spot of one point forever and serves its cars (does not carry boxes).</summary>
        PointWorker = 0,

        /// <summary>One per location: carries boxes from the warehouse to the hungriest point.</summary>
        Storekeeper = 1,
    }
}
