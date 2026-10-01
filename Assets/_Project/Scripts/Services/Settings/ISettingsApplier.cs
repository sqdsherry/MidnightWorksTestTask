namespace AutoService.Services.Settings
{
    /// <summary>
    /// Port that pushes settings into the engine (quality, screen mode, audio volumes).
    /// </summary>
    public interface ISettingsApplier
    {
        /// <summary>Applies <paramref name="settings"/> to the running game.</summary>
        void Apply(GameSettings settings);
    }
}
