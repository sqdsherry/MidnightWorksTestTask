using AutoService.Infrastructure.Config;
using AutoService.Infrastructure.Logging;
using AutoService.Infrastructure.Pause;
using AutoService.Infrastructure.Randomness;
using AutoService.Infrastructure.Save;
using AutoService.Infrastructure.Scenes;
using AutoService.Infrastructure.Settings;
using AutoService.Infrastructure.Timing;
using AutoService.Presentation.Loading;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Events;
using AutoService.Services.Save;
using AutoService.Services.Scenes;
using AutoService.Services.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoService.Bootstrap
{
    /// <summary>
    /// Composition Root of the whole application. Lives in the <c>Boot</c> scene, survives scene loads,
    /// builds project-wide services and hands them to the entry point of every loaded scene.
    /// </summary>
    /// <remarks>
    /// Scenes are loaded by the <see cref="ISceneLoader"/> behind the persistent loading screen (a child of this object);
    /// the loader calls back into <see cref="EnterScene"/> once a scene is loaded. The loading screen is ticked by the
    /// game loop on this object — the only project-wide tickable.
    /// </remarks>
    public sealed class ProjectEntryPoint : MonoBehaviour
    {
        private const float DefaultMusicVolume = 0.7f;
        private const float DefaultSfxVolume = 0.8f;

        [SerializeField]
        [Tooltip("Root game configuration asset.")]
        private GameConfig _gameConfig;

        [Header("Scenes")]
        [SerializeField]
        [Tooltip("Scene loaded right after boot. MainMenu in builds; Gameplay is handy for debugging in the editor.")]
        private GameScene _firstScene = GameScene.MainMenu;

        [SerializeField]
        [Tooltip("Name of the main menu scene in the Build Profile scene list.")]
        private string _mainMenuSceneName = "MainMenu";

        [SerializeField]
        [Tooltip("Name of the gameplay scene in the Build Profile scene list.")]
        private string _gameplaySceneName = "Gameplay";

        [Header("Loading screen")]
        [SerializeField]
        [Tooltip("Persistent loading screen (child of this object).")]
        private LoadingScreenView _loadingScreen;

        [SerializeField]
        [Tooltip("Game loop on this object; ticks the loading screen's progress bar.")]
        private GameLoop _gameLoop;

        private ServiceContainer _container;
        private IGameLogger _logger;
        private ISceneLoader _sceneLoader;
        private LoadingScreenPresenter _loadingScreenPresenter;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            _logger = new UnityGameLogger();
            _container = new ServiceContainer();

            ITimeProvider timeProvider = new SystemTimeProvider();
            IRandom random = new SystemRandom();
            IPauseService pause = new TimeScalePauseService(_logger);

            _container.Register<IGameLogger>(_logger);
            _container.Register<ITimeProvider>(timeProvider);
            _container.Register<IRandom>(random);
            _container.Register<IPauseService>(pause);
            _container.Register<IEventBus>(new EventBus(_logger));
            _container.Register<IConfigProvider>(new ScriptableObjectConfigProvider(_gameConfig));
            _container.Register<ISaveService>(CreateSaveService(timeProvider));
            _container.Register<ISettingsService>(CreateSettingsService());

            // Why: Unity's == — an unassigned (or missing) view must reach the loader as a real null.
            ILoadingCurtain curtain = _loadingScreen != null ? _loadingScreen : null;
            _sceneLoader = new UnitySceneLoader(_mainMenuSceneName, _gameplaySceneName, pause, _logger, curtain, EnterScene);
            _container.Register(_sceneLoader);
            CreateLoadingScreen(random);
        }

        private void Start()
        {
            _sceneLoader.Load(_firstScene);
        }

        private void OnDestroy()
        {
            // Why: Unity's == — the loop is on this object and may be destroyed first when Play Mode stops.
            if (_gameLoop != null && _loadingScreenPresenter != null)
            {
                _gameLoop.Remove(_loadingScreenPresenter);
            }

            _container?.Dispose();
            _container = null;
        }

        private void CreateLoadingScreen(IRandom random)
        {
            if (_loadingScreen == null)
            {
                _logger.Warning("[Boot] _loadingScreen is not assigned on " + name + "; scenes load without a loading screen.");
                return;
            }

            _loadingScreenPresenter = new LoadingScreenPresenter(_sceneLoader, _loadingScreen, random);
            _container.Register(_loadingScreenPresenter);
            if (_gameLoop != null)
            {
                _gameLoop.Add(_loadingScreenPresenter);
            }
            else
            {
                _logger.Warning("[Boot] _gameLoop is not assigned on " + name + "; the loading bar will not move.");
            }
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

        /// <summary>Hands the project container to the <see cref="ISceneEntryPoint"/> of a freshly loaded scene.</summary>
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
