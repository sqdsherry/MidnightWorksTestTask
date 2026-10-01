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

        /// <summary>Slot index for <see cref="CarDestinationKind.QueueSlot"/> / <see cref="CarDestinationKind.ParkingSlot"/>; -1 otherwise.</summary>
        public int Index { get; }

        /// <summary>Point id for <see cref="CarDestinationKind.Point"/>; null otherwise.</summary>
        public string PointId { get; }

        /// <summary>Slot <paramref name="index"/> of the entry queue (0 = head at the fork).</summary>
        public static CarDestination QueueSlot(int index) => new CarDestination(CarDestinationKind.QueueSlot, index, null);

        /// <summary>The parking exit barrier's car spot.</summary>
        public static CarDestination ParkingExit() => new CarDestination(CarDestinationKind.ParkingExit, -1, null);

        /// <summary>Parking slot <paramref name="index"/>.</summary>
        public static CarDestination ParkingSlot(int index) => new CarDestination(CarDestinationKind.ParkingSlot, index, null);

        /// <summary>The car spot of point <paramref name="pointId"/>.</summary>
        public static CarDestination Point(string pointId) => new CarDestination(CarDestinationKind.Point, -1, pointId);

        /// <summary>The location exit.</summary>
        public static CarDestination Exit() => new CarDestination(CarDestinationKind.Exit, -1, null);

        /// <summary>Debug text, e.g. "QueueSlot 2" or "Point loc1_wash_1". Allocates — not for per-frame use.</summary>
        public override string ToString()
        {
            switch (Kind)
            {
                case CarDestinationKind.QueueSlot:
                case CarDestinationKind.ParkingSlot:
                    return Kind + " " + Index;
                case CarDestinationKind.Point:
                    return Kind + " " + PointId;
                default:
                    return Kind.ToString();
            }
        }
    }
}
