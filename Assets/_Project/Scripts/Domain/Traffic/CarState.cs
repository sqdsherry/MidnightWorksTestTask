namespace AutoService.Domain.Traffic
{
    /// <summary>
    /// Where a car is in the flow:
    /// <c>Arriving → InQueue → (ToPoint | ToParking → Parked → ToParkingExit → AtParkingExit → (ToPoint | Leaving)) → AtPoint → Leaving</c>.
    /// The parking entrance is an automatic gate with no state of its own; every car leaving the lot pays at the parking exit.
    /// </summary>
    public enum CarState
    {
        /// <summary>Just spawned, not yet in the entry queue.</summary>
        Arriving = 0,

        /// <summary>Standing (or moving up) in the entry queue lane.</summary>
        InQueue = 1,

        /// <summary>Driving through the automatic entry gate to its reserved parking slot.</summary>
        ToParking = 2,

        /// <summary>Parked: first stays for its parking time, then waits until it may drive to the parking exit.</summary>
        Parked = 3,

        /// <summary>Driving from its slot to the parking exit barrier (the exit, and the point for a service car, are reserved).</summary>
        ToParkingExit = 4,

        /// <summary>Waiting at the parking exit barrier for the time-based fee to be taken.</summary>
        AtParkingExit = 5,

        /// <summary>Driving to the reserved service point.</summary>
        ToPoint = 6,

        /// <summary>On the service point spot (waiting for acceptance or being served).</summary>
        AtPoint = 7,

        /// <summary>Driving to the exit; despawned on arrival.</summary>
        Leaving = 8,
    }
}
