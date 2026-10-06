namespace AutoService.Domain.Points
{
    /// <summary>Role of a service point in the car flow.</summary>
    public enum PointKind
    {
        /// <summary>Parking entrance barrier: accepting the order takes the fee for the planned stay and lets the car into the lot.</summary>
        Barrier = 0,

        /// <summary>A real service (wash, oil change...): the car leaves the location afterwards.</summary>
        Service = 1,
    }
}
