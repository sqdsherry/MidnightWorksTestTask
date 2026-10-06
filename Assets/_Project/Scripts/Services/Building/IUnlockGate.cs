using System;

namespace AutoService.Services.Building
{
    /// <summary>
    /// Answers whether content with a level requirement is open. The build service asks it instead of knowing about
    /// the player's progression, so the progression module only swaps the implementation in the entry point.
    /// </summary>
    public interface IUnlockGate
    {
        /// <summary>Raised when the answer for some level may have changed (e.g. a level up).</summary>
        event Action Changed;

        /// <summary>True when content requiring <paramref name="requiredLevel"/> is available.</summary>
        bool IsUnlocked(int requiredLevel);
    }
}
