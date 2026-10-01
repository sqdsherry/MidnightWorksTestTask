namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// One module's part of the Gameplay Composition Root: creates the module's services, views and presenters
    /// and registers them in the scene's <see cref="GameplayContext"/>.
    /// </summary>
    /// <remarks>
    /// Installers run once, in a fixed order, from <see cref="GameplayEntryPoint.Enter"/>. A module that cannot start
    /// (missing scene references, a broken layout) logs why and leaves its outputs unset; later installers check them.
    /// </remarks>
    internal interface IGameplayInstaller
    {
        /// <summary>Builds the module on top of <paramref name="context"/>.</summary>
        void Install(GameplayContext context);
    }
}
