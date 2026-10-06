using System;

namespace AutoService.Services.Core
{
    /// <summary>
    /// Reference-counted pause: nested popups can each request pause safely.
    /// The game is paused while at least one request is active.
    /// </summary>
    public interface IPauseService
    {
        /// <summary>True while at least one pause request is active.</summary>
        bool IsPaused { get; }

        /// <summary>Raised when <see cref="IsPaused"/> flips; the argument is the new value.</summary>
        event Action<bool> PausedChanged;

        /// <summary>Adds a pause request. The first request (0 → 1) pauses the game.</summary>
        void Push();

        /// <summary>
        /// Removes a pause request. The last one (1 → 0) resumes the game.
        /// Calling it with no active requests logs a warning and does nothing.
        /// </summary>
        void Pop();

        /// <summary>
        /// Drops every pause request and resumes the game (raises <see cref="PausedChanged"/> if it was paused).
        /// </summary>
        /// <remarks>
        /// Why: requests belong to the scene that made them. When that scene is unloaded its popups never pop, so the
        /// scene loader calls this to keep a pause from leaking into the next scene. Not for gameplay code.
        /// </remarks>
        void ResetAll();
    }
}
