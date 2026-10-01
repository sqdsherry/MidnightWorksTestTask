using AutoService.Services.Core;
using UnityEngine;

namespace AutoService.Infrastructure.Logging
{
    /// <summary>
    /// <see cref="IGameLogger"/> that writes to the Unity console.
    /// </summary>
    public sealed class UnityGameLogger : IGameLogger
    {
        /// <inheritdoc />
        public void Info(string message) => Debug.Log(message);

        /// <inheritdoc />
        public void Warning(string message) => Debug.LogWarning(message);

        /// <inheritdoc />
        public void Error(string message) => Debug.LogError(message);
    }
}
