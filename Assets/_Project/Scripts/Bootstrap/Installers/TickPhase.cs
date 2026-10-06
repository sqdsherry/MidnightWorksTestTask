namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// Position of a ticked service in the frame. The game loop ticks phases in this order; inside a phase,
    /// services tick in registration order.
    /// </summary>
    /// <remarks>
    /// Why phases: installers run module by module, but the frame must still go input → simulation → visuals.
    /// A later module (staff) can thus put its service between two earlier ones (traffic and car agents).
    /// </remarks>
    internal enum TickPhase
    {
        /// <summary>Player input and movement.</summary>
        Input = 0,

        /// <summary>Domain simulation: service points.</summary>
        Simulation = 1,

        /// <summary>Traffic orchestration (cars, queues, parking).</summary>
        Traffic = 2,

        /// <summary>Staff logic (workers, storekeeper).</summary>
        Staff = 3,

        /// <summary>Physical agents that execute the orders of the logic above (cars, then staff).</summary>
        Agents = 4,

        /// <summary>Statistics derived from the simulated frame (income per minute).</summary>
        Trackers = 5,

        /// <summary>Presenters: render the state produced by this frame.</summary>
        Presentation = 6,
    }
}
