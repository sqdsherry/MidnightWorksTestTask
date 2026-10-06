using System;

namespace AutoService.Services.Building
{
    /// <summary>
    /// <see cref="IUnlockGate"/> stub of module A1: everything is open. Replaced by the progression module's gate.
    /// </summary>
    public sealed class AlwaysUnlockedGate : IUnlockGate
    {
        /// <inheritdoc />
        /// <remarks>Never raised: the answer never changes.</remarks>
        public event Action Changed
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public bool IsUnlocked(int requiredLevel) => true;
    }
}
