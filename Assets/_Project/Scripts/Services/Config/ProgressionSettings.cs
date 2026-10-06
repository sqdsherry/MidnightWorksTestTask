using System;
using AutoService.Domain.Progression;

namespace AutoService.Services.Config
{
    /// <summary>
    /// Immutable settings for player level progression.
    /// </summary>
    public sealed class ProgressionSettings
    {
        public ProgressionSettings(int[] levelThresholds, int xpPerLevelAfterTable)
        {
            Table = new LevelTable(levelThresholds, xpPerLevelAfterTable);
        }

        public LevelTable Table { get; }
    }
}
