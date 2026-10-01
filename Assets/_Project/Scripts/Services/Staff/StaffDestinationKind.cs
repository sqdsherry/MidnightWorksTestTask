namespace AutoService.Services.Staff
{
    /// <summary>Kind of place a staff NPC walks to.</summary>
    public enum StaffDestinationKind
    {
        /// <summary>The work spot of a point (the worker's place).</summary>
        WorkSpot = 0,

        /// <summary>The warehouse of a location.</summary>
        Warehouse = 1,

        /// <summary>Where a box is handed over at a point (next to it, not on its work spot).</summary>
        SupplyDrop = 2,
    }
}
