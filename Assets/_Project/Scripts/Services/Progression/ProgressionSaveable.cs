using System;
using AutoService.Services.Core;
using AutoService.Services.Save;

namespace AutoService.Services.Progression
{
    /// <summary>
    /// Saves and restores player progression (<see cref="SaveData.xp"/>, with <see cref="SaveData.level"/> for readability).
    /// </summary>
    /// <remarks>The level is derived from the total XP on restore, so a changed level table cannot desync them.</remarks>
    public sealed class ProgressionSaveable : ISaveable
    {
        private readonly IProgressionService _progression;
        private readonly IGameLogger _logger;

        /// <summary>Creates the saveable.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public ProgressionSaveable(IProgressionService progression, IGameLogger logger)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void Capture(SaveData data)
        {
            data.xp = _progression.Xp;
            data.level = _progression.Level;
        }

        /// <inheritdoc />
        public void Restore(SaveData data)
        {
            // Why: a hand-edited or corrupted file may hold a negative amount, which the domain rejects.
            if (data.xp < 0)
            {
                _logger.Warning("[Save] Negative XP " + data.xp + " in the save; restored as 0.");
                _progression.Restore(0);
                return;
            }

            _progression.Restore(data.xp);
        }
    }
}
