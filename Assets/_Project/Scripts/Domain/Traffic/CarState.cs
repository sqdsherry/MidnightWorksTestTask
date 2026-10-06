namespace AutoService.Domain.Traffic
{
    /// <summary>
    /// Where a car is in the flow (layout v3, GDD §3):
    /// <c>Arriving → InQueue → [ToBuffer → InBuffer →] ToPoint → AtPoint → (Leaving | ToEntrance …)</c> for a service car,
    /// <c>InQueue | AtPoint → ToEntrance → AtEntrance → ToParking → Parked → Leaving</c> for a parking visit.
    /// </summary>
    public enum CarState
    {
        /// <summary>Just spawned, not yet in the entry queue.</summary>
        Arriving = 0,

        /// <summary>Standing (or moving up) in the entry queue lane.</summary>
        InQueue = 1,

        /// <summary>Driving from the queue head into the buffer in front of its service point.</summary>
        ToBuffer = 2,

        /// <summary>Standing (or moving up) in the buffer in front of its service point.</summary>
        InBuffer = 3,

        /// <summary>Driving to the reserved service point.</summary>
        ToPoint = 4,

        /// <summary>On the service point spot (waiting for acceptance or being served).</summary>
        AtPoint = 5,

        /// <summary>Driving to a parking entrance barrier (the barrier and a parking slot are reserved).</summary>
        ToEntrance = 6,

        /// <summary>At the entrance barrier, waiting for the parking fee to be taken.</summary>
        AtEntrance = 7,

        /// <summary>Driving from the entrance to its reserved parking slot.</summary>
        ToParking = 8,

        /// <summary>Parked for the stay it paid for; leaves automatically afterwards.</summary>
        Parked = 9,

        /// <summary>Driving to the exit; despawned on arrival.</summary>
        Leaving = 10,
    }
}
