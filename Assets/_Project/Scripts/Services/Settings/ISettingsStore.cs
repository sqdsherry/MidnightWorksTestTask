namespace AutoService.Services.Settings
{
    /// <summary>
    /// Port to persistent settings storage (PlayerPrefs in the game, memory in tests).
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>Loads previously saved settings.</summary>
        /// <param name="settings">The saved settings, or null if there are none.</param>
        /// <returns>False if nothing has been saved yet.</returns>
        bool TryLoad(out GameSettings settings);

        /// <summary>Persists <paramref name="settings"/>.</summary>
        void Save(GameSettings settings);
    }
}
