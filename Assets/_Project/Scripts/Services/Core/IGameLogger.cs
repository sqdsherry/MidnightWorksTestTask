namespace AutoService.Services.Core
{
    /// <summary>
    /// Engine-agnostic logging facade. Domain and Services cannot reference UnityEngine,
    /// so they log through this interface; Infrastructure routes it to the Unity console.
    /// </summary>
    public interface IGameLogger
    {
        /// <summary>Logs an informational message.</summary>
        void Info(string message);

        /// <summary>Logs a recoverable problem.</summary>
        void Warning(string message);

        /// <summary>Logs an error.</summary>
        void Error(string message);
    }
}
