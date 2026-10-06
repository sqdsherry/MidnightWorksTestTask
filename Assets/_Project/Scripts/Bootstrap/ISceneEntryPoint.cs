namespace AutoService.Bootstrap
{
    /// <summary>
    /// Implemented by the single root component of a scene that builds that scene's object graph.
    /// </summary>
    /// <remarks>
    /// <see cref="ProjectEntryPoint"/> finds it among the loaded scene's root objects and hands over the project
    /// container explicitly — this is how services reach the scene without static state or singletons.
    /// </remarks>
    public interface ISceneEntryPoint
    {
        /// <summary>Builds the scene's services on top of the project-wide ones.</summary>
        /// <param name="projectServices">Project container; use it as the parent of the scene container.</param>
        void Enter(ServiceContainer projectServices);
    }
}
