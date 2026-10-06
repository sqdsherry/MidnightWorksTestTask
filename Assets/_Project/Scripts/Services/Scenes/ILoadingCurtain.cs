namespace AutoService.Services.Scenes
{
    /// <summary>What hides the scene switch: the scene loader waits for it to cover the screen before activating a scene.</summary>
    public interface ILoadingCurtain
    {
        /// <summary>True when the screen is fully covered (the fade-in has finished).</summary>
        bool IsOpaque { get; }
    }
}
