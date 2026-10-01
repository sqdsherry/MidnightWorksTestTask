namespace AutoService.Domain.Building
{
    /// <summary>What a build plot turns into once it is built.</summary>
    public enum BuildableKind
    {
        /// <summary>A service point (bay); the plot's target is the point id.</summary>
        ServicePoint = 0,

        /// <summary>One extra parking slot; the plot's target is the slot index.</summary>
        ParkingSlot = 1,
    }
}
