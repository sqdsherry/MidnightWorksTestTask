namespace AutoService.Services.Traffic
{
    /// <summary>Bus event: a car reached the exit and was despawned.</summary>
    public readonly struct CarLeftEvent
    {
        /// <summary>Creates the event.</summary>
        public CarLeftEvent(int carId, CarLeaveReason reason)
        {
            CarId = carId;
            Reason = reason;
        }

        /// <summary>Runtime id of the car.</summary>
        public int CarId { get; }

        /// <summary>Why the car left.</summary>
        public CarLeaveReason Reason { get; }
    }
}
