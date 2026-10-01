namespace AutoService.Domain.Points
{
    /// <summary>Role of a service point in the car flow.</summary>
    public enum PointKind
    {
        /// <summary>Parking exit barrier: accepting the order takes the time-based parking fee and lets the car out of the lot.</summary>
        Barrier = 0,

        /// <summary>A real service (wash, oil change...): the car leaves the location afterwards.</summary>
        Service = 1,
    }
}
