namespace AutoService.Services.Save
{
    /// <summary>
    /// A service that owns a slice of <see cref="SaveData"/> (money, buildings, staff...).
    /// Registered in <see cref="SaveCoordinator"/>, which calls it on every save and on load.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Writes the service's own slice into <paramref name="data"/>; must not touch other slices.</summary>
        void Capture(SaveData data);

        /// <summary>
        /// Reads the service's own slice from <paramref name="data"/>.
        /// Must tolerate missing or empty arrays and unknown ids (content may have changed since the save was written).
        /// </summary>
        void Restore(SaveData data);
    }
}
