namespace AutoService.Domain.Upgrades
{
    /// <summary>What a point upgrade improves.</summary>
    public enum UpgradeKind
    {
        /// <summary>Shorter service time.</summary>
        Speed = 0,

        /// <summary>Higher price of new orders.</summary>
        Price = 1,
    }
}
