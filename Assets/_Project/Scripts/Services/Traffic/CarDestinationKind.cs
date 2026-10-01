namespace AutoService.Services.Traffic
{
    /// <summary>Kind of place a car can be sent to.</summary>
    public enum CarDestinationKind
    {
        /// <summary>A slot of the entry queue lane (<see cref="CarDestination.Index"/>).</summary>
        QueueSlot = 0,

        /// <summary>The car spot of the location's parking barrier.</summary>
        Barrier = 1,

        /// <summary>A parking slot (<see cref="CarDestination.Index"/>).</summary>
        ParkingSlot = 2,

        /// <summary>The car spot of a service point (<see cref="CarDestination.PointId"/>).</summary>
        Point = 3,

        /// <summary>The location exit; the car is despawned there.</summary>
        Exit = 4,
    }
}
