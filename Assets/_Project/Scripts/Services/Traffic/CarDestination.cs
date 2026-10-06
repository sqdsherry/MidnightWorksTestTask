namespace AutoService.Services.Traffic
{
    /// <summary>
    /// Engine-agnostic address of a place in a location. Presentation resolves it into a road node of the scene.
    /// </summary>
    public readonly struct CarDestination
    {
        private CarDestination(CarDestinationKind kind, int index, string pointId)
        {
            Kind = kind;
            Index = index;
            PointId = pointId;
        }

        /// <summary>Kind of place.</summary>
        public CarDestinationKind Kind { get; }

        /// <summary>Slot index for queue, buffer and parking slots; -1 otherwise.</summary>
        public int Index { get; }

        /// <summary>Point id for <see cref="CarDestinationKind.Point"/>, <see cref="CarDestinationKind.Entrance"/> and
        /// <see cref="CarDestinationKind.BufferSlot"/> (the point the buffer belongs to); null otherwise.</summary>
        public string PointId { get; }

        /// <summary>Slot <paramref name="index"/> of the entry queue (0 = head at the fork).</summary>
        public static CarDestination QueueSlot(int index) => new CarDestination(CarDestinationKind.QueueSlot, index, null);

        /// <summary>Slot <paramref name="index"/> of the buffer in front of point <paramref name="pointId"/> (0 = nearest to the point).</summary>
        public static CarDestination BufferSlot(string pointId, int index) => new CarDestination(CarDestinationKind.BufferSlot, index, pointId);

        /// <summary>The car spot of the parking entrance barrier <paramref name="pointId"/>.</summary>
        public static CarDestination Entrance(string pointId) => new CarDestination(CarDestinationKind.Entrance, -1, pointId);

        /// <summary>Parking slot <paramref name="index"/>.</summary>
        public static CarDestination ParkingSlot(int index) => new CarDestination(CarDestinationKind.ParkingSlot, index, null);

        /// <summary>The car spot of point <paramref name="pointId"/>.</summary>
        public static CarDestination Point(string pointId) => new CarDestination(CarDestinationKind.Point, -1, pointId);

        /// <summary>The location exit.</summary>
        public static CarDestination Exit() => new CarDestination(CarDestinationKind.Exit, -1, null);

        /// <summary>Debug text, e.g. "QueueSlot 2", "BufferSlot loc1_wash_1 0" or "Point loc1_wash_1". Allocates — not for per-frame use.</summary>
        public override string ToString()
        {
            switch (Kind)
            {
                case CarDestinationKind.QueueSlot:
                case CarDestinationKind.ParkingSlot:
                    return Kind + " " + Index;
                case CarDestinationKind.BufferSlot:
                    return Kind + " " + PointId + " " + Index;
                case CarDestinationKind.Point:
                case CarDestinationKind.Entrance:
                    return Kind + " " + PointId;
                default:
                    return Kind.ToString();
            }
        }
    }
}
