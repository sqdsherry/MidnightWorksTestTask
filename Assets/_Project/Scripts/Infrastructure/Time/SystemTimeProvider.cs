using System;
using AutoService.Services.Core;

namespace AutoService.Infrastructure.Time
{
    /// <summary>
    /// <see cref="ITimeProvider"/> backed by the system clock.
    /// </summary>
    public sealed class SystemTimeProvider : ITimeProvider
    {
        /// <inheritdoc />
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
