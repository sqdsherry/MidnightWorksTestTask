using System;

namespace AutoService.Services.Progression
{
    /// <summary>
    /// Service for reading and updating player progression.
    /// </summary>
    public interface IProgressionService
    {
        /// <summary>Total experience collected.</summary>
        int Xp { get; }

        /// <summary>Current level, derived from <see cref="Xp"/>.</summary>
        int Level { get; }

        /// <summary>Progress towards the next level in [0, 1].</summary>
        float LevelProgress01 { get; }

        /// <summary>Experience still needed for the next level.</summary>
        int XpToNextLevel { get; }

        /// <summary>Raised after any XP or level change, restore included.</summary>
        event Action Changed;

        /// <summary>Raised on a level-up during play (not on restore); the argument is the new level.</summary>
        event Action<int> LeveledUp;

        /// <summary>Sets the total XP from a save without level-up notifications.</summary>
        void Restore(int xp);

        /// <summary>Adds experience points, triggering level up if thresholds are met.</summary>
        void AddExperience(int amount);
    }
}
