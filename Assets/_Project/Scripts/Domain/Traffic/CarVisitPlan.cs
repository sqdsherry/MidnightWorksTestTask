namespace AutoService.Domain.Traffic
{
    /// <summary>What a car came for; chosen once on spawn (GDD §3).</summary>
    /// <remarks>"Wash" stands for any service point of the location (the car's requested service type).</remarks>
    public enum CarVisitPlan
    {
        /// <summary>Pays at the main entrance, parks for a while, leaves.</summary>
        ParkOnly = 0,

        /// <summary>Gets its service and leaves by the top road.</summary>
        WashOnly = 1,

        /// <summary>Gets its service, then parks through the service entrance (or leaves if it cannot).</summary>
        WashThenPark = 2,
    }
}
