using AutoService.Services.Settings;

namespace AutoService.Tests.EditMode
{
    /// <summary>In-memory <see cref="ISettingsStore"/>.</summary>
    public sealed class FakeSettingsStore : ISettingsStore
    {
        /// <summary>The stored settings; null means nothing has been saved.</summary>
        public GameSettings Stored { get; set; }

        /// <summary>Number of <see cref="Save"/> calls.</summary>
        public int SaveCount { get; private set; }

        /// <inheritdoc />
        public bool TryLoad(out GameSettings settings)
        {
            settings = Stored;
            return settings != null;
        }

        /// <inheritdoc />
        public void Save(GameSettings settings)
        {
            SaveCount++;
            Stored = settings;
        }
    }
}
