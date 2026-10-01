namespace AutoService.Services.Core
{
    /// <summary>
    /// A service that needs a one-time setup step after the whole object graph of its scope has been built
    /// (e.g. to subscribe to other services that did not exist yet at construction time).
    /// </summary>
    public interface IInitializable
    {
        /// <summary>Called exactly once by the entry point after all services are registered.</summary>
        void Initialize();
    }
}
