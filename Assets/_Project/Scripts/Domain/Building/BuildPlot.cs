using System;

namespace AutoService.Domain.Building
{
    /// <summary>A fixed place on the map where something can be built once.</summary>
    public sealed class BuildPlot
    {
        /// <summary>Creates an unbuilt plot.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
        public BuildPlot(BuildPlotDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        /// <summary>Rules of the plot.</summary>
        public BuildPlotDefinition Definition { get; }

        /// <summary>True once the construction is done.</summary>
        public bool IsBuilt { get; private set; }

        /// <summary>Marks the plot as built. Payment is the caller's business.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the plot is already built.</exception>
        public void MarkBuilt()
        {
            if (IsBuilt)
            {
                // Why: building twice would mean paying twice or registering the same point twice — always a caller bug.
                throw new InvalidOperationException("Build plot '" + Definition.Id + "' is already built.");
            }

            IsBuilt = true;
        }
    }
}
