namespace AutoService.Services.Save
{
    /// <summary>
    /// Reads and writes the single save slot. Knows nothing about game services; that is <see cref="SaveCoordinator"/>'s job.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>True if a save exists (it may still fail to load if it is corrupted).</summary>
        bool HasSave { get; }

        /// <summary>Loads, validates, migrates and normalizes the save.</summary>
        /// <param name="data">The loaded snapshot with every array non-null, or null on failure.</param>
        /// <returns>False if there is no save or it is unreadable, corrupted or of an unsupported version.</returns>
        bool TryLoad(out SaveData data);

        /// <summary>Stamps <paramref name="data"/> with the current version and time and writes it.</summary>
        void Save(SaveData data);

        /// <summary>Deletes the save.</summary>
        void Delete();
    }
}
