using System;
using System.Collections.Generic;

namespace AutoService.Domain.Progression
{
    /// <summary>
    /// Represents a level progression table mapping XP to levels.
    /// </summary>
    public sealed class LevelTable
    {
        private readonly int[] _thresholds;
        private readonly int _xpPerLevelAfterTable;

        public LevelTable(IReadOnlyList<int> thresholds, int xpPerLevelAfterTable)
        {
            if (thresholds == null || thresholds.Count == 0)
                throw new ArgumentException("Thresholds cannot be null or empty.", nameof(thresholds));

            if (thresholds[0] != 0)
                throw new ArgumentException("The first threshold must be 0.", nameof(thresholds));

            for (int i = 1; i < thresholds.Count; i++)
            {
                if (thresholds[i] <= thresholds[i - 1])
                    throw new ArgumentException("Thresholds must be strictly increasing.", nameof(thresholds));
            }

            if (xpPerLevelAfterTable <= 0)
                throw new ArgumentException("XP per level after table must be greater than zero.", nameof(xpPerLevelAfterTable));

            _thresholds = new int[thresholds.Count];
            for (int i = 0; i < thresholds.Count; i++)
            {
                _thresholds[i] = thresholds[i];
            }
            _xpPerLevelAfterTable = xpPerLevelAfterTable;
        }

        public int LevelForXp(int xp)
        {
            if (xp < 0)
                throw new ArgumentOutOfRangeException(nameof(xp), "XP cannot be negative.");

            for (int i = _thresholds.Length - 1; i >= 0; i--)
            {
                if (xp >= _thresholds[i])
                {
                    if (i == _thresholds.Length - 1)
                    {
                        int extraXp = xp - _thresholds[i];
                        return _thresholds.Length + (extraXp / _xpPerLevelAfterTable);
                    }
                    return i + 1;
                }
            }

            return 1;
        }

        public int XpForLevel(int level)
        {
            if (level <= 0)
                throw new ArgumentOutOfRangeException(nameof(level), "Level must be greater than zero.");

            if (level <= _thresholds.Length)
            {
                return _thresholds[level - 1];
            }

            int lastThreshold = _thresholds[_thresholds.Length - 1];
            return lastThreshold + (level - _thresholds.Length) * _xpPerLevelAfterTable;
        }
    }
}
