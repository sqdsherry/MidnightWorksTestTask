using System;
using System.Collections.Generic;
using AutoService.Bootstrap.Installers;
using AutoService.Presentation.Building;
using AutoService.Presentation.CameraControl;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Player;
using AutoService.Presentation.Traffic;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Formatting;
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

        [Header("Location")]
        [SerializeField]
        [Tooltip("Markup of location 1: points, road graph, queue and parking slots, spawn/exit.")]
        private LocationLayout _location1;

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
        [Tooltip("Screen-space build panel shown next to a plot the character stands at.")]
        private BuildPanelView _buildPanel;

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
            IGameplayInstaller[] installers =
            {
                new EconomyInstaller(),
                player,
                serviceLoop,
                new BuildingInstaller(serviceLoop, player),
                new HudInstaller(),
            };

            for (int i = 0; i < installers.Length; i++)
            {
                installers[i].Install(context);
            }

            InitializeServices();
            StartTicking();

            _logger.Info("[Gameplay] Ready. Balance: " + MoneyFormatter.Format(_container.Resolve<IWalletService>().Balance));
        }

        private void OnDestroy()
        {
            StopTicking();
            DisposeOwned();
            _container?.Dispose();
            _container = null;
        }

        private GameplaySceneRefs CreateSceneRefs()
        {
            return new GameplaySceneRefs(
                _inputActions,
                _camera,
                _cameraRig,
                _player,
                _clickMarker,
                _interactableMask,
                _groundMask,
                _occluderMask,
                _location1,
                _carVisuals,
                _carPoolRoot,
                _balanceView,
                _buildPanel);
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
