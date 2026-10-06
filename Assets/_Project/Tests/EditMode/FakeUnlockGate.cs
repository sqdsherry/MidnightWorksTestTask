using System;
using AutoService.Services.Building;

namespace AutoService.Tests.EditMode
{
    /// <summary>Test <see cref="IUnlockGate"/>: everything up to <see cref="MaxUnlockedLevel"/> is open.</summary>
    public sealed class FakeUnlockGate : IUnlockGate
    {
        /// <inheritdoc />
        public event Action Changed;

        /// <summary>Highest open level; changing it raises <see cref="Changed"/>.</summary>
        public int MaxUnlockedLevel { get; private set; } = int.MaxValue;

        /// <inheritdoc />
        public bool IsUnlocked(int requiredLevel) => requiredLevel <= MaxUnlockedLevel;

        /// <summary>Opens levels up to <paramref name="level"/> (and closes the ones above).</summary>
        public void UnlockUpTo(int level)
        {
            MaxUnlockedLevel = level;
            Changed?.Invoke();
        }
    }
}
