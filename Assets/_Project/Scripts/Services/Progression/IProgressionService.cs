using System;

namespace AutoService.Services.Progression
{
    /// <summary>
    /// Service for reading and updating player progression.
    /// </summary>
    public interface IProgressionService
    {
        int Xp { get; }
        int Level { get; }
        float LevelProgress01 { get; }
        int XpToNextLevel { get; }

        event Action Changed;
        event Action<int> LeveledUp;

        void Restore(int xp);
    }
}
