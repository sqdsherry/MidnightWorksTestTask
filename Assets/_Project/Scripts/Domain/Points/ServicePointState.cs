namespace AutoService.Domain.Points
{
    /// <summary>Lifecycle of a service point's current order.</summary>
    public enum ServicePointState
    {
        /// <summary>Free: no car is assigned.</summary>
        Idle = 0,

        /// <summary>A car is assigned and driving to the point.</summary>
        Reserved = 1,

        /// <summary>The car is on the spot and waits for someone to accept the order.</summary>
        AwaitingAccept = 2,

        /// <summary>The order is accepted (paid) and the service is in progress.</summary>
        Servicing = 3,

        /// <summary>The service is done and the car drives away; the point is unavailable for <c>ClearDelay</c> seconds.</summary>
        Clearing = 4,
    }
}
