namespace AutoService.Services.Save
{
    /// <summary>Writes the current progress right now (leaving to the menu, quitting).</summary>
    /// <remarks>
    /// A narrow view of <see cref="SaveCoordinator"/> for code that only needs "save now" — the pause menu does not get to
    /// restore or reset progress.
    /// </remarks>
    public interface IGameSaver
    {
        /// <summary>Captures and writes the progress immediately.</summary>
        void SaveNow();

        /// <summary>Deletes the save and suspends future saving until next restore.</summary>
        void ResetProgress();
    }
}
