using System;

namespace AutoService.Domain.Upgrades
{
    /// <summary>Purchased level of one upgrade of one point (0 = not upgraded), capped at <see cref="MaxLevel"/>.</summary>
    /// <remarks>Prices and effects are not here: they come from the upgrade's config, so balancing never touches the domain.</remarks>
    public sealed class UpgradeTrack
    {
        /// <summary>Creates a track at level 0.</summary>
        /// <param name="maxLevel">Highest level (&gt;= 0; 0 = cannot be upgraded).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative max level.</exception>
        public UpgradeTrack(int maxLevel)
        {
            if (maxLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLevel), maxLevel, "Max level must be non-negative.");
            }

            MaxLevel = maxLevel;
        }

        /// <summary>Current level.</summary>
        public int Level { get; private set; }

        /// <summary>Highest level.</summary>
        public int MaxLevel { get; }

        /// <summary>True at <see cref="MaxLevel"/>.</summary>
        public bool IsMaxed => Level >= MaxLevel;

        /// <summary>Raises the level by one.</summary>
        /// <exception cref="InvalidOperationException">Thrown at <see cref="MaxLevel"/>.</exception>
        public void LevelUp()
        {
            if (IsMaxed)
            {
                throw new InvalidOperationException("Upgrade is already at its max level " + MaxLevel + ".");
            }

            Level++;
        }

        /// <summary>Sets the saved level, clamped to 0..<see cref="MaxLevel"/> (a save may come from another config).</summary>
        public void Restore(int level)
        {
            Level = Math.Max(0, Math.Min(MaxLevel, level));
        }
    }
}
