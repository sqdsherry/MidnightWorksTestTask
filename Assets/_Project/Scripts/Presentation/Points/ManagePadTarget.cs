namespace AutoService.Presentation.Points
{
    /// <summary>What a <see cref="ManagePadView"/> manages, i.e. what its Target Id means.</summary>
    public enum ManagePadTarget
    {
        /// <summary>A service point (Target Id = point id): its upgrades and worker.</summary>
        ServicePoint = 0,

        /// <summary>The warehouse of a location (Target Id = location id): the storekeeper.</summary>
        Warehouse = 1,
    }
}
