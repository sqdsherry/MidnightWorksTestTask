using System;
using System.Collections.Generic;
using AutoService.Domain.Building;
using AutoService.Services.Core;
using AutoService.Services.Save;

namespace AutoService.Services.Building
{
    /// <summary>Saves and restores the built plots (<see cref="SaveData.builtPlotIds"/>).</summary>
    /// <remarks>
    /// Restoring raises <see cref="IBuildService.BuiltRestored"/> for every plot, which registers the built bays
    /// synchronously — so this saveable must be restored before anything that refers to those bays (points, staff).
    /// </remarks>
    public sealed class BuildSaveable : ISaveable
    {
        private readonly IBuildService _build;
        private readonly UnknownSaveIds _unknown;
        private readonly List<string> _known = new List<string>();

        /// <summary>Creates the saveable.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public BuildSaveable(IBuildService build, IGameLogger logger)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _unknown = new UnknownSaveIds(logger, "build plot");
        }

        /// <inheritdoc />
        public void Capture(SaveData data)
        {
            IReadOnlyList<string> built = _build.BuiltPlotIds;
            var ids = new string[built.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = built[i];
            }

            data.builtPlotIds = ids;
        }

        /// <inheritdoc />
        public void Restore(SaveData data)
        {
            string[] ids = data.builtPlotIds;
            if (ids == null || ids.Length == 0)
            {
                return;
            }

            _known.Clear();
            for (int i = 0; i < ids.Length; i++)
            {
                if (_build.TryGet(ids[i], out BuildPlot _))
                {
                    _known.Add(ids[i]);
                }
                else
                {
                    _unknown.Report(ids[i]);
                }
            }

            _build.RestoreBuilt(_known);
        }
    }
}
