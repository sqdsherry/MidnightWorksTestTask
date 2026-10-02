namespace AutoService.Services.Upgrades
{
    /// <summary>Whether the next level of a point upgrade can be bought now, and if not — why.</summary>
    public enum UpgradeAvailability
    {
        /// <summary>Unlocked, not maxed and affordable.</summary>
        Available = 0,

        /// <summary>At the max level.</summary>
        Maxed = 1,

        /// <summary>The level requirement is not met.</summary>
        Locked = 2,

        /// <summary>Unlocked, but the balance does not cover the cost.</summary>
        NotEnoughMoney = 3,

        /// <summary>The config has no upgrade of this kind (the panel hides its row).</summary>
        NotSupported = 4,
    }
}
