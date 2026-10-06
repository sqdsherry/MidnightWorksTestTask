using System;

namespace AutoService.Services.Core
{
    /// <summary>
    /// Source of wall-clock time. Abstracted so time-dependent logic can be tested deterministically.
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>Current UTC date and time.</summary>
        DateTime UtcNow { get; }
    }
}
