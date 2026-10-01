using System.Collections.Generic;
using AutoService.Services.Core;

namespace AutoService.Tests.EditMode
{
    /// <summary>Test double that records every logged message.</summary>
    public sealed class FakeGameLogger : IGameLogger
    {
        /// <summary>Messages passed to <see cref="Info"/>.</summary>
        public List<string> Infos { get; } = new List<string>();

        /// <summary>Messages passed to <see cref="Warning"/>.</summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>Messages passed to <see cref="Error"/>.</summary>
        public List<string> Errors { get; } = new List<string>();

        /// <inheritdoc />
        public void Info(string message) => Infos.Add(message);

        /// <inheritdoc />
        public void Warning(string message) => Warnings.Add(message);

        /// <inheritdoc />
        public void Error(string message) => Errors.Add(message);
    }
}
