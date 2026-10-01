namespace AutoService.Domain.Points
{
    /// <summary>Role of a service point in the car flow.</summary>
    public enum PointKind
    {
        /// <summary>Parking barrier: accepting the order lets the car into the parking lot.</summary>
        Barrier = 0,

        /// <summary>A real service (wash, oil change...): the car leaves the location afterwards.</summary>
        Service = 1,
    }
}
