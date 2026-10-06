namespace AutoService.Services.Core
{
    /// <summary>
    /// A service updated every frame by the single game loop. Implementations must not allocate in <see cref="Tick"/>.
    /// </summary>
    public interface ITickable
    {
        /// <summary>Advances the service by one frame.</summary>
        /// <param name="deltaTime">Scaled frame time in seconds; 0 while the game is paused.</param>
        void Tick(float deltaTime);
    }
}
