namespace AutoService.Services.Traffic
{
    /// <summary>Kind of place a car can be sent to.</summary>
    public enum CarDestinationKind
    {
        /// <summary>A slot of the entry queue lane (<see cref="CarDestination.Index"/>).</summary>
        QueueSlot = 0,

        /// <summary>A slot of the buffer in front of a service point (<see cref="CarDestination.PointId"/> + <see cref="CarDestination.Index"/>).</summary>
        BufferSlot = 1,

        /// <summary>The car spot of a parking entrance barrier (<see cref="CarDestination.PointId"/>).</summary>
        Entrance = 2,

        /// <summary>A parking slot (<see cref="CarDestination.Index"/>).</summary>
        ParkingSlot = 3,

        /// <summary>The car spot of a service point (<see cref="CarDestination.PointId"/>).</summary>
        Point = 4,

        /// <summary>The location exit; the car is despawned there.</summary>
        Exit = 5,
    }
}
