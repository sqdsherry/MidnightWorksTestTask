using System.Collections.Generic;
using AutoService.Services.Settings;

namespace AutoService.Tests.EditMode
{
    /// <summary><see cref="ISettingsApplier"/> that records every applied value.</summary>
    public sealed class FakeSettingsApplier : ISettingsApplier
    {
        /// <summary>Every value passed to <see cref="Apply"/>, in order.</summary>
        public List<GameSettings> Applied { get; } = new List<GameSettings>();

        /// <inheritdoc />
        public void Apply(GameSettings settings) => Applied.Add(settings);
    }
}
