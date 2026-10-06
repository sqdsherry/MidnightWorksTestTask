using System;
using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Configures the XP required for each player level.
    /// </summary>
    [Serializable]
    public sealed class ProgressionSection
    {
        [SerializeField]
        [Tooltip("XP required for each level (0-based index corresponds to level 1, 2, etc.). First element must be 0.")]
        private int[] _levelThresholds = { 0, 15, 35, 70, 110 };

        [SerializeField, Min(1)]
        [Tooltip("XP required for each level after the last threshold in the table.")]
        private int _xpPerLevelAfterTable = 50;

        public int[] LevelThresholds => _levelThresholds;
        public int XpPerLevelAfterTable => _xpPerLevelAfterTable;
    }
}
