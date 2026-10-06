namespace AutoService.Services.Traffic
{
    /// <summary>Bus event: a new car appeared and joined the entry queue.</summary>
    public readonly struct CarSpawnedEvent
    {
        /// <summary>Creates the event.</summary>
        public CarSpawnedEvent(int carId, string carTypeId, string serviceTypeId, string locationId)
        {
            CarId = carId;
            CarTypeId = carTypeId;
            ServiceTypeId = serviceTypeId;
            LocationId = locationId;
        }

        /// <summary>Runtime id of the car.</summary>
        public int CarId { get; }

        /// <summary>Type id of the car.</summary>
        public string CarTypeId { get; }

        /// <summary>Id of the service the car wants, or null for a parking-only car.</summary>
        public string ServiceTypeId { get; }

        /// <summary>Location the car spawned in.</summary>
        public string LocationId { get; }
    }
}
