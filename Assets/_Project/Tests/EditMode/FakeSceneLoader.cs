using System;
using System.Collections.Generic;
using AutoService.Services.Scenes;

namespace AutoService.Tests.EditMode
{
    /// <summary><see cref="ISceneLoader"/> that only records the requested scenes.</summary>
    public sealed class FakeSceneLoader : ISceneLoader
    {
        /// <summary>Every scene passed to <see cref="Load"/>, in order.</summary>
        public List<GameScene> Loaded { get; } = new List<GameScene>();

        /// <inheritdoc />
        public event Action<GameScene> LoadStarted;

        /// <inheritdoc />
        public event Action<GameScene> LoadCompleted;

        /// <inheritdoc />
        public bool IsLoading => false;

        /// <inheritdoc />
        public float Progress => 1f;

        /// <inheritdoc />
        public void Load(GameScene scene)
        {
            Loaded.Add(scene);
            LoadStarted?.Invoke(scene);
            LoadCompleted?.Invoke(scene);
        }
    }
}
