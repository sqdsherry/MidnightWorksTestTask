namespace AutoService.Services.Traffic
{
    /// <summary>
    /// Bus event: a served car wanted to park afterwards, but the service entrance or every slot was taken, so it left
    /// (for the future "No spaces" speech bubble).
    /// </summary>
    public readonly struct ParkingRefusedEvent
    {
        /// <summary>Creates the event.</summary>
        public ParkingRefusedEvent(int carId, string locationId)
        {
            CarId = carId;
            LocationId = locationId;
        }

        /// <summary>Runtime id of the car.</summary>
        public int CarId { get; }

        /// <summary>Location the car left.</summary>
        public string LocationId { get; }
    }
}
