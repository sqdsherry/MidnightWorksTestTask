using System;
using System.Collections.Generic;
using AutoService.Services.Core;
using AutoService.Services.Scenes;

namespace AutoService.Presentation.Loading
{
    /// <summary>
    /// Shows the <see cref="LoadingScreenView"/> with a random tip for every scene load, moves its bar while loading and
    /// hides it when the new scene is ready.
    /// </summary>
    /// <remarks>Ticked by the project's game loop; per frame it only writes the progress into the bar (no allocations).</remarks>
    public sealed class LoadingScreenPresenter : ITickable, IDisposable
    {
        private readonly ISceneLoader _loader;
        private readonly LoadingScreenView _view;
        private readonly IRandom _random;
        private bool _disposed;

        /// <summary>Creates the presenter and subscribes to the loader.</summary>
        /// <param name="loader">The scene loader.</param>
        /// <param name="view">The loading screen.</param>
        /// <param name="random">Picks the tip.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public LoadingScreenPresenter(ISceneLoader loader, LoadingScreenView view, IRandom random)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _loader.LoadStarted += OnLoadStarted;
            _loader.LoadCompleted += OnLoadCompleted;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_loader.IsLoading)
            {
                _view.SetProgress(_loader.Progress);
            }
        }

        /// <summary>Unsubscribes from the loader. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _loader.LoadStarted -= OnLoadStarted;
            _loader.LoadCompleted -= OnLoadCompleted;
        }

        private void OnLoadStarted(GameScene scene)
        {
            _view.Show(PickTip());
        }

        private void OnLoadCompleted(GameScene scene)
        {
            _view.SetProgress(1f);
            _view.Hide();
        }

        private string PickTip()
        {
            IReadOnlyList<string> tips = _view.Tips;
            return tips.Count > 0 ? tips[_random.Range(0, tips.Count)] : string.Empty;
        }
    }
}
