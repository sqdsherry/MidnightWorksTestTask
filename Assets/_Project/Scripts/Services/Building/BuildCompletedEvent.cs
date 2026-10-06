using AutoService.Domain.Building;

namespace AutoService.Services.Building
{
    /// <summary>
    /// Bus event: the player paid for and built a plot (for XP, onboarding, sound). Not published for plots restored from a save.
    /// Published by <see cref="BuildService"/>.
    /// </summary>
    public readonly struct BuildCompletedEvent
    {
        /// <summary>Creates the event.</summary>
        public BuildCompletedEvent(string plotId, BuildableKind kind, string targetId)
        {
            PlotId = plotId;
            Kind = kind;
            TargetId = targetId;
        }

        /// <summary>Id of the built plot.</summary>
        public string PlotId { get; }

        /// <summary>What was built.</summary>
        public BuildableKind Kind { get; }

        /// <summary>Point id or parking slot index of the plot.</summary>
        public string TargetId { get; }
    }
}
