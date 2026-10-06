using System;
using AutoService.Services.Building;

namespace AutoService.Services.Progression
{
    /// <summary>
    /// Gate that unlocks content based on the player's progression level.
    /// </summary>
    public sealed class LevelUnlockGate : IUnlockGate, IDisposable
    {
        private readonly IProgressionService _progression;

        public LevelUnlockGate(IProgressionService progression)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _progression.LeveledUp += OnLeveledUp;
        }

        public event Action Changed;

        public bool IsUnlocked(int requiredLevel)
        {
            return _progression.Level >= requiredLevel;
        }

        private void OnLeveledUp(int newLevel)
        {
            Changed?.Invoke();
        }

        public void Dispose()
        {
            _progression.LeveledUp -= OnLeveledUp;
        }
    }
}
