namespace AutoService.Domain.Staff
{
    /// <summary>
    /// What a hired NPC is doing. Workers: <see cref="WalkingToSpot"/> → (<see cref="WaitingForSpot"/>) → <see cref="Working"/>.
    /// Storekeeper: (<see cref="ReturningHome"/> →) <see cref="Idle"/> → <see cref="ToWarehouse"/> → (<see cref="WaitingForMoney"/>) →
    /// <see cref="ToPoint"/> → <see cref="ReturningHome"/> → <see cref="Idle"/>; a hungry point turns a returning storekeeper
    /// straight back to the warehouse.
    /// </summary>
    public enum StaffState
    {
        /// <summary>Worker: walks from the staff room to the point's work spot.</summary>
        WalkingToSpot = 0,

        /// <summary>Worker: arrived, but the player stands on the work spot; takes it once the player leaves.</summary>
        WaitingForSpot = 1,

        /// <summary>Worker: holds the work spot (final state).</summary>
        Working = 2,

        /// <summary>Storekeeper: waits at its spot by the warehouse; nothing needs restocking.</summary>
        Idle = 3,

        /// <summary>Storekeeper: walks to the warehouse for a box.</summary>
        ToWarehouse = 4,

        /// <summary>Storekeeper: at the warehouse, but the balance does not cover the box; retries.</summary>
        WaitingForMoney = 5,

        /// <summary>Storekeeper: carries a box to its target point.</summary>
        ToPoint = 6,

        /// <summary>Storekeeper: walks to its waiting spot at the warehouse (after hiring, a delivery or a cancelled run).</summary>
        ReturningHome = 7,
    }
}
