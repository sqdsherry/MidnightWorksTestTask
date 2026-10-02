using System;

namespace AutoService.Services.Scenes
{
    /// <summary>
    /// Loads one scene at a time behind the loading screen and reports its progress.
    /// </summary>
    /// <remarks>
    /// A load always replaces the current scene. Requests made while a load is running are ignored, so a double click on
    /// "New Game" cannot start two loads.
    /// </remarks>
    public interface ISceneLoader
    {
        /// <summary>True from <see cref="Load"/> until <see cref="LoadCompleted"/> (or until the load failed).</summary>
        bool IsLoading { get; }

        /// <summary>Progress of the current load in [0, 1]; 1 when nothing is loading.</summary>
        float Progress { get; }

        /// <summary>Raised when a load starts (before the old scene is unloaded).</summary>
        event Action<GameScene> LoadStarted;

        /// <summary>
        /// Raised when the new scene is loaded, entered and the loading screen may go. Not raised if the load failed;
        /// the error is logged instead.
        /// </summary>
        event Action<GameScene> LoadCompleted;

        /// <summary>Starts loading <paramref name="scene"/>. Ignored while another load is running.</summary>
        void Load(GameScene scene);
    }
}
