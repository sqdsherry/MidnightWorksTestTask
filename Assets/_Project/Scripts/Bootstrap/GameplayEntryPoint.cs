using System;
using System.Collections.Generic;
using AutoService.Bootstrap.Installers;
using AutoService.Presentation.CameraControl;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Panels;
using AutoService.Presentation.Pause;
using AutoService.Presentation.Player;
using AutoService.Presentation.Points.Panel;
using AutoService.Presentation.Popups;
using AutoService.Presentation.Settings;
using AutoService.Presentation.Staff;
using AutoService.Presentation.Supplies;
using AutoService.Presentation.Traffic;
using AutoService.Presentation.Ui;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Formatting;
using AutoService.Services.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoService.Bootstrap
{
    /// <summary>
    /// Composition Root of the <c>Gameplay</c> scene. Builds all gameplay services on top of the project container,
    /// initializes them and starts ticking.
    /// </summary>
    /// <remarks>
    /// <see cref="Enter"/> is a table of contents: one installer per module (<c>Bootstrap/Installers</c>), run in
    /// dependency order, then Initialize() → ticking. The serialized scene references stay here and reach the installers
    /// as an immutable <see cref="GameplaySceneRefs"/>.
    /// </remarks>
    public sealed class GameplayEntryPoint : MonoBehaviour, ISceneEntryPoint
    {
        [SerializeField]
        [Tooltip("The scene's single game loop that ticks gameplay services.")]
        private GameLoop _gameLoop;

        [Header("Player & Input")]
        [SerializeField]
        [Tooltip("GameControls asset with the 'Gameplay' action map.")]
        private InputActionAsset _inputActions;

        [SerializeField]
        [Tooltip("Gameplay camera used for pointer raycasts.")]
        private Camera _camera;

        [SerializeField]
        private CameraRig _cameraRig;

        [SerializeField]
        private PlayerView _player;

        [SerializeField]
        [Tooltip("Optional ground click feedback.")]
        private ClickMarkerView _clickMarker;

        [SerializeField]
        [Tooltip("Layers of clickable interactable objects.")]
        private LayerMask _interactableMask;

        [SerializeField]
        [Tooltip("Layers of walkable ground.")]
        private LayerMask _groundMask;

        [SerializeField]
        [Tooltip("Layers that block pointer raycasts without being clickable (walls, roofs). May be empty.")]
        private LayerMask _occluderMask;

        [Header("Locations")]
        [SerializeField]
        [Tooltip("Markup of locations: points, road graph, queue and parking slots, spawn/exit. If empty, found automatically.")]
        private LocationLayout[] _locations;

        [SerializeField]
        [Tooltip("Car type id → car prefab.")]
        private CarVisualCatalog _carVisuals;

        [SerializeField]
        [Tooltip("Parent of pooled car instances. Optional (scene root if empty).")]
        private Transform _carPoolRoot;

        [Header("HUD")]
        [SerializeField]
        [Tooltip("Temporary balance label (until the full HUD).")]
        private BalanceView _balanceView;

        [SerializeField]
        [Tooltip("Level and XP progression display in HUD.")]
        private ProgressionView _progressionView;

        [SerializeField]
        [Tooltip("Screen-space build panel shown next to a plot the character stands at.")]
        private OfferPanelView _buildPanel;

        [Header("Staff & Supplies")]
        [SerializeField]
        [Tooltip("Screen-space management panel opened from a point's blue pad.")]
        private PointPanelView _pointPanel;

        [SerializeField]
        [Tooltip("Screen-space storekeeper offer opened from the warehouse's blue pad.")]
        private OfferPanelView _storekeeperPanel;

        [SerializeField]
        [Tooltip("Body prefab of hired NPCs (Prefabs/Staff).")]
        private StaffView _staffPrefab;

        [SerializeField]
        [Tooltip("Staff role → body material.")]
        private StaffVisualCatalog _staffVisuals;

        [SerializeField]
        [Tooltip("Consumable → box color.")]
        private SupplyVisualCatalog _supplyVisuals;

        [SerializeField]
        [Tooltip("Parent of NPC instances. Optional (scene root if empty).")]
        private Transform _staffRoot;

        [SerializeField]
        [Tooltip("Shows the box in the player's hands (on the Player object).")]
        private PlayerCarryView _playerCarry;

        [Header("Pause & Settings")]
        [SerializeField]
        [Tooltip("Pause menu (Resume / Settings / Main Menu / Quit).")]
        private PauseMenuView _pauseMenu;

        [SerializeField]
        [Tooltip("Pause button in the top left corner of the HUD.")]
        private PauseButtonView _pauseButton;

        [SerializeField]
        [Tooltip("Settings screen opened from the pause menu (Prefabs/UI/SettingsPanel).")]
        private SettingsView _settingsPanel;

        [Header("Popups & Cheats")]
        [SerializeField]
        [Tooltip("Modal level up congratulatory popup.")]
        private LevelUpPopupView _levelUpPopup;

        [SerializeField]
        [Tooltip("Modal welcome popup upon arriving at Location 2.")]
        private Location2WelcomePopupView _loc2WelcomePopup;

        [SerializeField]
        [Tooltip("F1 / button debug cheat panel.")]
        private DebugCheatView _debugCheatView;

        // Why: lifecycle lists are filled by the installers (through GameplayContext), so every service created there is
        // initialized and ticked without each module having to remember to wire itself in.
        private readonly List<IInitializable> _initializables = new List<IInitializable>();
        private readonly List<ITickable> _tickables = new List<ITickable>();
        private readonly List<TickPhase> _tickPhases = new List<TickPhase>();

        // Why: the container holds one instance per contract type, but there is one presenter per point (and, later,
        // one traffic per location); such instances are owned and disposed by this entry point instead.
        private readonly List<IDisposable> _ownedDisposables = new List<IDisposable>();

        private ServiceContainer _container;
        private IGameLogger _logger;
        private IGameSaver _saver;

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Thrown if the scene was already entered.</exception>
        public void Enter(ServiceContainer projectServices)
        {
            if (projectServices == null)
            {
                throw new ArgumentNullException(nameof(projectServices));
            }

            if (_container != null)
            {
                throw new InvalidOperationException("Gameplay scene has already been entered.");
            }

            _container = new ServiceContainer(projectServices);
            _logger = _container.Resolve<IGameLogger>();
            var context = new GameplayContext(
                _container, _logger, CreateSceneRefs(), name, _initializables, _tickables, _tickPhases, _ownedDisposables);

            // Why: the order is the dependency order — every installer only uses what the ones above it produced.
            var player = new PlayerInstaller();
            var serviceLoop = new ServiceLoopInstaller();
            var building = new BuildingInstaller(serviceLoop, player);
            IGameplayInstaller[] installers =
            {
                new EconomyInstaller(),
                new ProgressionInstaller(),
                new JuiceInstaller(),
                player,
                serviceLoop,
                building,
                new StaffSuppliesInstaller(serviceLoop, building, player),
                new SaveInstaller(),
                new HudInstaller(),
                new AudioInstaller(gameObject.scene.GetRootGameObjects()),
            };

            for (int i = 0; i < installers.Length; i++)
            {
                installers[i].Install(context);
            }

            _container.TryResolve(out _saver);
            InitializeServices();
            StartTicking();

            _logger.Info("[Gameplay] Ready. Balance: " + MoneyFormatter.Format(_container.Resolve<IWalletService>().Balance));
        }

        // Why: Unity calls OnApplicationQuit before OnDestroy, so the services are still alive and can be captured;
        // disposal (OnDestroy) must not save — see SaveCoordinator.Dispose.
        private void OnApplicationQuit()
        {
            _saver?.SaveNow();
        }

        // Why: a suspended app may be killed without OnApplicationQuit ever being called.
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _saver?.SaveNow();
            }
        }

        private void OnDestroy()
        {
            _saver = null;
            StopTicking();
            DisposeOwned();
            _container?.Dispose();
            _container = null;
        }

        private GameplaySceneRefs CreateSceneRefs()
        {
            LocationLayout[] locations = _locations;
            if (locations == null || locations.Length == 0)
            {
                locations = FindObjectsByType<LocationLayout>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                // Why: FindObjectsByType has no defined order, but the first location is the primary one (camera
                // bounds, storekeeper panel, debug teleport) — sort by name so Location_1 always comes first.
                Array.Sort(locations, (a, b) => string.CompareOrdinal(a.name, b.name));
            }

            return new GameplaySceneRefs(
                _inputActions,
                _camera,
                _cameraRig,
                _player,
                _clickMarker,
                _interactableMask,
                _groundMask,
                _occluderMask,
                locations,
                _carVisuals,
                _carPoolRoot,
                _balanceView,
                _progressionView,
                _buildPanel,
                _pointPanel,
                _storekeeperPanel,
                _staffPrefab,
                _staffVisuals,
                _supplyVisuals,
                _staffRoot,
                _playerCarry,
                _pauseMenu,
                _pauseButton,
                _settingsPanel,
                _levelUpPopup,
                _loc2WelcomePopup,
                _debugCheatView);
        }

        // Why: reverse order, like the container — dependents go before what they depend on.
        private void DisposeOwned()
        {
            for (int i = _ownedDisposables.Count - 1; i >= 0; i--)
            {
                try
                {
                    _ownedDisposables[i].Dispose();
                }
                catch (Exception exception)
                {
                    // Why: one failing presenter must not leave the remaining subscriptions alive.
                    Debug.LogException(exception, this);
                }
            }

            _ownedDisposables.Clear();
        }

        private void InitializeServices()
        {
            for (int i = 0; i < _initializables.Count; i++)
            {
                _initializables[i].Initialize();
            }
        }

        // Why: phase by phase, registration order inside a phase (see TickPhase).
        private void StartTicking()
        {
            if (_tickables.Count == 0)
            {
                return;
            }

            if (_gameLoop == null)
            {
                _logger.Error("[Gameplay] GameLoop is not assigned; " + _tickables.Count + " services will not tick.");
                return;
            }

            for (TickPhase phase = TickPhase.Input; phase <= TickPhase.Presentation; phase++)
            {
                for (int i = 0; i < _tickables.Count; i++)
                {
                    if (_tickPhases[i] == phase)
                    {
                        _gameLoop.Add(_tickables[i]);
                    }
                }
            }
        }

        private void StopTicking()
        {
            if (_gameLoop == null)
            {
                return;
            }

            for (int i = 0; i < _tickables.Count; i++)
            {
                _gameLoop.Remove(_tickables[i]);
            }

            _tickables.Clear();
            _tickPhases.Clear();
        }
    }
}
