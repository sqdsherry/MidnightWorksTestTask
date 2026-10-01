namespace AutoService.Domain.Points
{
    /// <summary>Who stands on a point's work spot.</summary>
    public enum OccupantKind
    {
        /// <summary>Nobody: the point neither accepts orders nor makes progress.</summary>
        None = 0,

        /// <summary>The player character.</summary>
        Player = 1,

        /// <summary>A hired worker (module 06).</summary>
        Worker = 2,
    }
}
