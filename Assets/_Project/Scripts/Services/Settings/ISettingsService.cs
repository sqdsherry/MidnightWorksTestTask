using System;

namespace AutoService.Services.Settings
{
    /// <summary>
    /// Owns the current player settings: persists, applies and broadcasts every change.
    /// Settings are stored separately from the game save, so resetting progress keeps them.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Raised after <see cref="Set"/> or <see cref="Preview"/> has applied new settings; the argument is the new value.
        /// </summary>
        event Action<GameSettings> Changed;

        /// <summary>The settings in effect.</summary>
        GameSettings Current { get; }

        /// <summary>Saves and applies <paramref name="settings"/>, then raises <see cref="Changed"/>.</summary>
        void Set(GameSettings settings);

        /// <summary>
        /// Applies <paramref name="settings"/> and raises <see cref="Changed"/> WITHOUT saving them — for values that change
        /// continuously (a dragged volume slider). Call <see cref="Set"/> with the final value to keep it.
        /// </summary>
        void Preview(GameSettings settings);
    }
}
