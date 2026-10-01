namespace AutoService.Services.Save
{
    /// <summary>
    /// Port to the place where the serialized save lives (a file on disk in the game, memory in tests).
    /// Implementations handle their own I/O errors and never throw them to the caller.
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>True if there is something to read.</summary>
        bool Exists { get; }

        /// <summary>Reads the stored text.</summary>
        /// <param name="json">The stored text, or null when nothing could be read.</param>
        /// <returns>False if nothing is stored or reading failed.</returns>
        bool TryRead(out string json);

        /// <summary>Replaces the stored text. On failure the previous content stays intact.</summary>
        void Write(string json);

        /// <summary>Removes everything stored, including backups.</summary>
        void Delete();
    }
}
