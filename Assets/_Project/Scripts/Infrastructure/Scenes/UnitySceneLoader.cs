using System;
using System.Threading;
using AutoService.Services.Core;
using AutoService.Services.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoService.Infrastructure.Scenes
{
    /// <summary>
    /// <see cref="ISceneLoader"/> on <see cref="SceneManager.LoadSceneAsync(string, LoadSceneMode)"/> and
    /// <see cref="Awaitable"/>. The new scene is activated only once the loading curtain covers the screen, then handed to
    /// the composition root (the <c>sceneLoaded</c> callback enters it), and the loading screen is kept for at least
    /// <see cref="MinScreenSeconds"/>.
    /// </summary>
    /// <remarks>
    /// Timing is unscaled (real time), so a paused game cannot freeze the loading screen. Disposing cancels a running load
    /// (Play Mode stopped while loading).
    /// </remarks>
    public sealed class UnitySceneLoader : ISceneLoader, IDisposable
    {
        /// <summary>Shortest time the loading screen stays up, in real seconds.</summary>
        /// <remarks>Why: a small scene loads in a couple of frames; without a minimum the loading screen only flickers.</remarks>
        public const float MinScreenSeconds = 0.6f;

        /// <summary>Longest wait for the curtain to cover the screen, in real seconds.</summary>
        /// <remarks>Why: a broken curtain (disabled object, zero alpha forever) must delay a load, never stall it.</remarks>
        public const float MaxCurtainWaitSeconds = 1f;

        // Why: Unity reports 0.9 once the scene is loaded and only waits for activation; the rest is not real work.
        private const float LoadedProgress = 0.9f;

        private readonly string _mainMenuSceneName;
        private readonly string _gameplaySceneName;
        private readonly IPauseService _pause;
        private readonly IGameLogger _logger;
        private readonly ILoadingCurtain _curtain;
        private readonly Action<Scene> _sceneLoaded;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private AsyncOperation _operation;
        private bool _disposed;

        /// <summary>Creates the loader.</summary>
        /// <param name="mainMenuSceneName">Name of the main menu scene in the Build Profile scene list.</param>
        /// <param name="gameplaySceneName">Name of the gameplay scene in the Build Profile scene list.</param>
        /// <param name="pause">Reset around every load, so no pause leaks into the next scene.</param>
        /// <param name="logger">Receives load errors.</param>
        /// <param name="curtain">Covers the switch; may be null (scenes then switch as soon as they are loaded).</param>
        /// <param name="sceneLoaded">Called with the new scene once it is active, while the loading screen is still up.</param>
        /// <exception cref="ArgumentException">Thrown when a scene name is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when another required argument is null.</exception>
        public UnitySceneLoader(
            string mainMenuSceneName,
            string gameplaySceneName,
            IPauseService pause,
            IGameLogger logger,
            ILoadingCurtain curtain,
            Action<Scene> sceneLoaded)
        {
            if (string.IsNullOrEmpty(mainMenuSceneName))
            {
                throw new ArgumentException("The main menu scene name is empty.", nameof(mainMenuSceneName));
            }

            if (string.IsNullOrEmpty(gameplaySceneName))
            {
                throw new ArgumentException("The gameplay scene name is empty.", nameof(gameplaySceneName));
            }

            _mainMenuSceneName = mainMenuSceneName;
            _gameplaySceneName = gameplaySceneName;
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _curtain = curtain;
            _sceneLoaded = sceneLoaded ?? throw new ArgumentNullException(nameof(sceneLoaded));
        }

        /// <inheritdoc />
        public event Action<GameScene> LoadStarted;

        /// <inheritdoc />
        public event Action<GameScene> LoadCompleted;

        /// <inheritdoc />
        public event Action<GameScene> LoadFailed;

        /// <inheritdoc />
        public bool IsLoading { get; private set; }

        /// <inheritdoc />
        public float Progress { get; private set; } = 1f;

        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an unknown <paramref name="scene"/>.</exception>
        public void Load(GameScene scene)
        {
            if (_disposed || IsLoading)
            {
                return;
            }

            string sceneName = GetSceneName(scene);
            IsLoading = true;
            Progress = 0f;
            Run(scene, sceneName);
        }

        /// <summary>Cancels a running load. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        // Why: the loader's only async void — Load is fire-and-forget for its callers, so every exception (subscribers of
        // LoadStarted included) is caught here, and IsLoading is always reset: a throw must never lock the loader.
        private async void Run(GameScene scene, string sceneName)
        {
            try
            {
                // Why: the old scene's pause requests die with it; the time scale is back to 1 while it unloads.
                _pause.ResetAll();
                LoadStarted?.Invoke(scene);
                await LoadAsync(scene, sceneName, _lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // The loader was disposed (Play Mode stopped): nothing to finish.
                ReleaseOperation();
                IsLoading = false;
            }
            catch (Exception exception)
            {
                _logger.Error("[Scenes] Failed to load scene '" + sceneName + "' (is it in File > Build Profiles > Scene List?): "
                    + exception);

                // Why: reset before raising LoadFailed — a listener may start the next load right away.
                ReleaseOperation();
                IsLoading = false;
                Progress = 1f;
                RaiseFailed(scene);
            }
        }

        // Why: a pending operation with activation off blocks every later async load in Unity.
        private void ReleaseOperation()
        {
            if (_operation != null)
            {
                _operation.allowSceneActivation = true;
                _operation = null;
            }
        }

        private async Awaitable LoadAsync(GameScene scene, string sceneName, CancellationToken token)
        {
            float startTime = Time.realtimeSinceStartup;
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException("Scene '" + sceneName + "' cannot be loaded.");
            }

            // Why: activation swaps the scenes; it is held back until the curtain is opaque, so the player never sees the
            // switch through a half-faded loading screen. Progress stops at 0.9 meanwhile (the bar shows it as full load).
            _operation = operation;
            operation.allowSceneActivation = false;
            while (operation.progress < LoadedProgress || !IsCovered(startTime))
            {
                Progress = VisibleProgress(operation.progress / LoadedProgress, startTime);
                await Awaitable.NextFrameAsync(token);
            }

            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                await Awaitable.NextFrameAsync(token);
            }

            _operation = null;

            // Why: again after the load — the old scene kept running behind the loading screen and may have paused.
            _pause.ResetAll();
            EnterScene(SceneManager.GetSceneByName(sceneName));

            while (Time.realtimeSinceStartup - startTime < MinScreenSeconds)
            {
                Progress = VisibleProgress(1f, startTime);
                await Awaitable.NextFrameAsync(token);
            }

            Progress = 1f;
            IsLoading = false;

            // Why: the scene is in; a throwing listener must not be reported as a failed load.
            try
            {
                LoadCompleted?.Invoke(scene);
            }
            catch (Exception exception)
            {
                _logger.Error("[Scenes] A LoadCompleted listener threw: " + exception);
            }
        }

        private bool IsCovered(float startTime)
        {
            return _curtain == null
                || _curtain.IsOpaque
                || Time.realtimeSinceStartup - startTime >= MaxCurtainWaitSeconds;
        }

        // Why: a scene whose entry point throws is broken, but the loading screen must still go away so the error is
        // visible and the player is not left in front of a frozen bar.
        private void EnterScene(Scene loaded)
        {
            try
            {
                _sceneLoaded(loaded);
            }
            catch (Exception exception)
            {
                _logger.Error("[Scenes] Failed to enter scene '" + loaded.name + "': " + exception);
            }
        }

        private void RaiseFailed(GameScene scene)
        {
            try
            {
                LoadFailed?.Invoke(scene);
            }
            catch (Exception exception)
            {
                _logger.Error("[Scenes] A LoadFailed listener threw: " + exception);
            }
        }

        // Why: the bar follows the slower of the real load and the minimum screen time, so it fills smoothly instead of
        // jumping to the end in the first frames and then standing still.
        private static float VisibleProgress(float loadProgress, float startTime)
        {
            float timeProgress = (Time.realtimeSinceStartup - startTime) / MinScreenSeconds;
            return Mathf.Clamp01(Mathf.Min(loadProgress, timeProgress));
        }

        private string GetSceneName(GameScene scene)
        {
            switch (scene)
            {
                case GameScene.MainMenu:
                    return _mainMenuSceneName;
                case GameScene.Gameplay:
                    return _gameplaySceneName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scene), scene, "Unknown scene.");
            }
        }
    }
}
