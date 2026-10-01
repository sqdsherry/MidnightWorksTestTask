using System;
using AutoService.Infrastructure.Config;
using AutoService.Infrastructure.Logging;
using AutoService.Infrastructure.Pause;
using AutoService.Infrastructure.Randomness;
using AutoService.Infrastructure.Save;
using AutoService.Infrastructure.Settings;
using AutoService.Infrastructure.Timing;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Events;
using AutoService.Services.Save;
using AutoService.Services.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoService.Bootstrap
{
    /// <summary>
    /// Composition Root of the whole application. Lives in the <c>Boot</c> scene, survives scene loads,
    /// builds project-wide services and hands them to the entry point of every loaded scene.
    /// </summary>
    public sealed class ProjectEntryPoint : MonoBehaviour
    {
        private const float DefaultMusicVolume = 0.7f;
        private const float DefaultSfxVolume = 0.8f;

        [SerializeField]
        [Tooltip("Root game configuration asset.")]
        private GameConfig _gameConfig;

        [SerializeField]
        [Tooltip("Scene loaded right after boot. Must be in the Build Profile scene list.")]
        private string _firstSceneName = "Gameplay";

        private ServiceContainer _container;
        private IGameLogger _logger;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            _logger = new UnityGameLogger();
            _container = new ServiceContainer();

            ITimeProvider timeProvider = new SystemTimeProvider();

            _container.Register<IGameLogger>(_logger);
            _container.Register<ITimeProvider>(timeProvider);
            _container.Register<IRandom>(new SystemRandom());
            _container.Register<IPauseService>(new TimeScalePauseService(_logger));
            _container.Register<IEventBus>(new EventBus(_logger));
            _container.Register<IConfigProvider>(new ScriptableObjectConfigProvider(_gameConfig));
            _container.Register<ISaveService>(CreateSaveService(timeProvider));
            _container.Register<ISettingsService>(CreateSettingsService());
        }

        // TODO(09-scenes-ui): replace with ISceneLoader + loading screen.
        private async void Start()
        {
            // Why: an exception escaping an async void method has no caller to catch it,
            // so everything is caught and logged here with the scene name for context.
            try
            {
                AsyncOperation loading = SceneManager.LoadSceneAsync(_firstSceneName, LoadSceneMode.Single);
                if (loading == null)
                {
                    _logger.Error("[Boot] Cannot load scene '" + _firstSceneName + "'. Is it added to the Build Profile scene list?");
                    return;
                }

                await loading;

                // The object may have been destroyed while awaiting (e.g. Play Mode stopped).
                if (this == null)
                {
                    return;
                }

                EnterScene(SceneManager.GetSceneByName(_firstSceneName));
            }
            catch (Exception exception)
            {
                _logger.Error("[Boot] Failed to start scene '" + _firstSceneName + "': " + exception);
            }
        }

        private void OnDestroy()
        {
            _container?.Dispose();
            _container = null;
        }

        private ISaveService CreateSaveService(ITimeProvider timeProvider)
        {
            var storage = new FileSaveStorage(Application.persistentDataPath, FileSaveStorage.DefaultFileName, _logger);
            return new SaveService(storage, new JsonUtilitySaveSerializer(), timeProvider, _logger);
        }

        private static ISettingsService CreateSettingsService()
        {
            // Why: quality and window mode default to what the player launched with, so the first launch changes nothing.
            var defaults = new GameSettings(
                DefaultMusicVolume,
                DefaultSfxVolume,
                QualitySettings.GetQualityLevel(),
                Screen.fullScreen,
                resolutionWidth: 0,
                resolutionHeight: 0);

            var settings = new SettingsService(new PlayerPrefsSettingsStore(), new UnitySettingsApplier(), defaults);

            // Why: applied right here rather than with the scene's IInitializable pass, because settings are
            // project-wide and must be in effect before (and independently of) any scene.
            settings.Initialize();
            return settings;
        }

        private void EnterScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].TryGetComponent(out ISceneEntryPoint entryPoint))
                {
                    entryPoint.Enter(_container);
                    return;
                }
            }

            _logger.Error("[Boot] No ISceneEntryPoint found on the root objects of scene '" + scene.name + "'.");
        }
    }
}
