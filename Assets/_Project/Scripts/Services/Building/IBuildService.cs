using System;
using System.Collections.Generic;
using AutoService.Domain.Building;

namespace AutoService.Services.Building
{
    /// <summary>
    /// Registry of the fixed build plots of the scene and the only way to build them (pay → mark built → notify).
    /// What a built plot does to the world (a new point, a parking slot) is decided by its listeners.
    /// </summary>
    public interface IBuildService
    {
        /// <summary>Raised after the player built a plot (paid for it).</summary>
        event Action<BuildPlot> Built;

        /// <summary>Raised for every plot marked built by <see cref="RestoreBuilt"/> (no payment, no build animation).</summary>
        event Action<BuildPlot> BuiltRestored;

        /// <summary>All registered plots, in registration order.</summary>
        IReadOnlyList<BuildPlot> Plots { get; }

        /// <summary>Ids of the built plots, in build order (for the save module).</summary>
        IReadOnlyList<string> BuiltPlotIds { get; }

        /// <summary>Looks up a plot by id.</summary>
        /// <returns>False (and null) for an unknown id.</returns>
        bool TryGet(string plotId, out BuildPlot plot);

        /// <summary>Whether <paramref name="plotId"/> can be built now.</summary>
        /// <exception cref="ArgumentException">Thrown for an unknown plot id.</exception>
        BuildAvailability GetAvailability(string plotId);

        /// <summary>Pays for and builds the plot if it is <see cref="BuildAvailability.Available"/>.</summary>
        /// <returns>False (nothing changes) for an unknown, built, locked or unaffordable plot.</returns>
        bool TryBuild(string plotId);

        /// <summary>
        /// Marks saved plots as built without payment and raises <see cref="BuiltRestored"/> for each.
        /// Unknown and already built ids are skipped (a save may come from an older layout).
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plotIds"/> is null.</exception>
        void RestoreBuilt(IEnumerable<string> plotIds);
    }
}
