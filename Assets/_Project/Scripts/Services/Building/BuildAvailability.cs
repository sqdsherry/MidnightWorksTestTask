namespace AutoService.Services.Building
{
    /// <summary>Whether a plot can be built right now, and if not — why.</summary>
    public enum BuildAvailability
    {
        /// <summary>Can be built: unlocked and affordable.</summary>
        Available = 0,

        /// <summary>Already built.</summary>
        Built = 1,

        /// <summary>The level requirement is not met.</summary>
        Locked = 2,

        /// <summary>Unlocked, but the balance does not cover the cost.</summary>
        NotEnoughMoney = 3,
    }
}
