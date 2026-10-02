using System;
using System.Collections.Generic;
using AutoService.Services.Core;

namespace AutoService.Services.Save
{
    /// <summary>
    /// Reports ids of a save that no longer exist in the scene (a save from an older layout or config): one warning per
    /// id, so a repeated restore does not flood the log.
    /// </summary>
    public sealed class UnknownSaveIds
    {
        private readonly IGameLogger _logger;
        private readonly string _what;
        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Creates the reporter.</summary>
        /// <param name="logger">Receives the warnings.</param>
        /// <param name="what">What the ids are, for the message (e.g. "build plot").</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public UnknownSaveIds(IGameLogger logger, string what)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _what = what ?? "id";
        }

        /// <summary>Logs that <paramref name="id"/> was skipped, unless it was already reported.</summary>
        public void Report(string id)
        {
            string key = id ?? string.Empty;
            if (_reported.Add(key))
            {
                _logger.Warning("[Save] Skipped unknown " + _what + " '" + key + "' (the save comes from another layout).");
            }
        }
    }
}
