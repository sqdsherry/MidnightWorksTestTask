namespace AutoService.Services.Staff
{
    /// <summary>Whether a staff member can be hired now, and if not — why.</summary>
    public enum HireAvailability
    {
        /// <summary>Unlocked, not hired yet and affordable.</summary>
        Available = 0,

        /// <summary>Already hired (also while still walking to the job).</summary>
        Hired = 1,

        /// <summary>The level requirement is not met.</summary>
        Locked = 2,

        /// <summary>Unlocked, but the balance does not cover the cost.</summary>
        NotEnoughMoney = 3,

        /// <summary>Nobody can be hired here (unknown point, or its service type has no worker config).</summary>
        NotSupported = 4,
    }
}
