namespace AutoService.Domain.Traffic
{
    /// <summary>
    /// Where a car is in the flow:
    /// <c>Arriving → InQueue → (ToPoint | ToBarrier → AtBarrier → ToParking → Parked → ToPoint) → AtPoint → Leaving</c>.
    /// </summary>
    public enum CarState
    {
        /// <summary>Just spawned, not yet in the entry queue.</summary>
        Arriving = 0,

        /// <summary>Standing (or moving up) in the entry queue lane.</summary>
        InQueue = 1,

        /// <summary>Driving to the parking barrier.</summary>
        ToBarrier = 2,

        /// <summary>Waiting at the barrier for the parking order to be accepted.</summary>
        AtBarrier = 3,

        /// <summary>Driving to its reserved parking slot.</summary>
        ToParking = 4,

        /// <summary>Parked, waiting for a free point of its service.</summary>
        Parked = 5,

        /// <summary>Driving to the reserved service point.</summary>
        ToPoint = 6,

        /// <summary>On the service point spot (waiting for acceptance or being served).</summary>
        AtPoint = 7,

        /// <summary>Driving to the exit; despawned on arrival.</summary>
        Leaving = 8,
    }
}
