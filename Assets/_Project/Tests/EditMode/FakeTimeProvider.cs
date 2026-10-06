using System;
using AutoService.Services.Core;

namespace AutoService.Tests.EditMode
{
    /// <summary><see cref="ITimeProvider"/> that returns a time set by the test.</summary>
    public sealed class FakeTimeProvider : ITimeProvider
    {
        /// <inheritdoc />
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}
