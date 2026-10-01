using System;
using System.Collections.Generic;
using AutoService.Domain.Economy;
using AutoService.Domain.Points;
using AutoService.Presentation.CameraControl;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Player;
using AutoService.Presentation.Points;
using AutoService.Presentation.Traffic;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Formatting;
using AutoService.Services.Points;
using AutoService.Services.Traffic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoService.Bootstrap
{
    /// <summary>
    /// Composition Root of the <c>Gameplay</c> scene. Builds all gameplay services on top of the project container,
    /// initializes them and starts ticking.
    /// </summary>
    /// <remarks>
    /// Build order: configs → domain → services → (save load) → views/presenters → Initialize() → ticking.
    /// Every module adds its own <c>Register*</c> step below.
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

        // Why: lifecycle lists are filled while registering, so every service created here is initialized
        // and ticked without each module having to remember to wire itself in.
        private readonly List<IInitializable> _initializables = new List<IInitializable>();
        private readonly List<ITickable> _tickables = new List<ITickable>();

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

            IConfigProvider config = _container.Resolve<IConfigProvider>();
            IEventBus eventBus = _container.Resolve<IEventBus>();

            RegisterEconomy(config, eventBus);
            RegisterPlayer(_container.Resolve<IPauseService>());
            RegisterServiceLoop(config, eventBus, _container.Resolve<IRandom>());
            RegisterHud();

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

        private void RegisterEconomy(IConfigProvider config, IEventBus eventBus)
        {
            // TODO(08-save): start from the saved balance when a save exists.
            var wallet = new Wallet(config.Economy.StartingMoney);
            Register<IWalletService>(new WalletService(wallet, eventBus));
        }

        // Why: a scene with missing references should still run the rest of the game and say exactly what is missing,
        // instead of failing with a NullReferenceException deep inside a constructor.
        private void RegisterPlayer(IPauseService pause)
        {
            // Why: non-short-circuit `|` so every missing reference is reported at once, not one per Play.
            if (!HasReference(_inputActions, nameof(_inputActions))
                | !HasReference(_camera, nameof(_camera))
                | !HasReference(_cameraRig, nameof(_cameraRig))
                | !HasReference(_player, nameof(_player)))
            {
                _logger.Error("[Gameplay] Player module skipped: assign the missing references on " + name + ".");
                return;
            }

            if (_player.Agent == null)
            {
                _logger.Error("[Gameplay] Player module skipped: PlayerView '" + _player.name + "' has no NavMeshAgent assigned.");
                return;
            }

            if (_interactableMask.value == 0)
            {
                _logger.Warning("[Gameplay] " + nameof(_interactableMask) + " is empty; interactables cannot be clicked.");
            }

            if (_groundMask.value == 0)
            {
                _logger.Warning("[Gameplay] " + nameof(_groundMask) + " is empty; clicks on the ground are ignored.");
            }

            GameplayInput input;
            try
            {
                input = new GameplayInput(_inputActions);
            }
            catch (InvalidOperationException exception)
            {
                _logger.Error("[Gameplay] Player module skipped: " + exception.Message);
                return;
            }

            // Why: Presentation classes have no contracts of their own; they are registered under their concrete types
            // so the container disposes them (presenter and motor first, input last — reverse registration order).
            Register(input);
            input.Enable();

            var raycaster = new PointerRaycaster(_camera, _interactableMask, _groundMask, _occluderMask);
            Register(new PlayerMotor(_player));
            Register(new PlayerInputPresenter(input, raycaster, _player, pause, _clickMarker));
            _cameraRig.Construct(input, _player);
        }

        /// <summary>Points, traffic and their presenters. Tick order: point service → traffic → car agents → point presenters.</summary>
        private void RegisterServiceLoop(IConfigProvider config, IEventBus eventBus, IRandom random)
        {
            var presenters = new List<ServicePointPresenter>();
            ServicePointService points = RegisterPoints(config, eventBus, presenters);
            if (points != null)
            {
                RegisterTraffic(points, config, random, eventBus);
            }

            // Why: presenters are tracked last so they render the state produced by this frame's simulation.
            for (int i = 0; i < presenters.Count; i++)
            {
                Track(presenters[i]);
            }
        }

        /// <returns>The point service, or null when the location cannot run (traffic is skipped then).</returns>
        private ServicePointService RegisterPoints(IConfigProvider config, IEventBus eventBus, List<ServicePointPresenter> presenters)
        {
            if (!HasReference(_location1, nameof(_location1)))
            {
                _logger.Error("[Gameplay] Service loop skipped: assign the location layout on " + name + ".");
                return null;
            }

            if (!_location1.Validate(out string problem))
            {
                _logger.Error("[Gameplay] Service loop skipped: LocationLayout '" + _location1.name + "': " + problem + ".");
                return null;
            }

            var points = new ServicePointService(_container.Resolve<IWalletService>(), eventBus);
            Register<IServicePointService>(points);

            // Why: every parking visit pays at one of the two entrances, so both must be Barrier-kind points; without them
            // the traffic cannot run (the kind is checked inside TryRegisterPoint). Non-short-circuit `|` reports both.
            if (!TryRegisterPoint(points, config, _location1.MainEntrance, PointKind.Barrier, presenters)
                | !TryRegisterPoint(points, config, _location1.ServiceEntrance, PointKind.Barrier, presenters))
            {
                _logger.Error("[Gameplay] Traffic skipped: location '" + _location1.LocationId + "' needs two valid parking entrances.");
                return null;
            }

            ServicePointView[] servicePoints = _location1.ServicePoints;
            for (int i = 0; i < servicePoints.Length; i++)
            {
                TryRegisterPoint(points, config, servicePoints[i], PointKind.Service, presenters);
            }

            return points;
        }

        private bool TryRegisterPoint(
            ServicePointService points,
            IConfigProvider config,
            ServicePointView view,
            PointKind expectedKind,
            List<ServicePointPresenter> presenters)
        {
            if (!config.TryGetServiceType(view.ServiceTypeId, out ServiceTypeSettings settings))
            {
                _logger.Error("[Gameplay] ServicePointView '" + view.name + "': unknown service type '" + view.ServiceTypeId
                    + "'; add it to GameConfig. Point skipped.");
                return false;
            }

            if (settings.Kind != expectedKind)
            {
                _logger.Error("[Gameplay] ServicePointView '" + view.name + "': service type '" + settings.Id + "' is "
                    + settings.Kind + ", expected " + expectedKind + ". Point skipped.");
                return false;
            }

            ServicePoint point;
            try
            {
                point = points.Register(settings.CreatePointDefinition(view.PointId, _location1.LocationId));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                // Empty or duplicate point id.
                _logger.Error("[Gameplay] ServicePointView '" + view.name + "': " + exception.Message + " Point skipped.");
                return false;
            }

            view.Construct(points);
            presenters.Add(new ServicePointPresenter(view, point));
            return true;
        }

        private void RegisterTraffic(IServicePointService points, IConfigProvider config, IRandom random, IEventBus eventBus)
        {
            if (!HasReference(_carVisuals, nameof(_carVisuals)))
            {
                _logger.Error("[Gameplay] Traffic skipped: assign the car visual catalog on " + name + ".");
                return;
            }

            if (config.CarTypes.Count == 0)
            {
                _logger.Warning("[Gameplay] GameConfig has no car types; no cars will spawn.");
            }

            var definition = new LocationTrafficDefinition(
                _location1.LocationId,
                _location1.MainEntrance.PointId,
                _location1.ServiceEntrance.PointId,
                _location1.QueueSlotCount,
                _location1.ParkingSlotCount,
                _location1.ServiceBufferCapacity);
            var agents = new CarAgents(_location1, _carVisuals, _carPoolRoot);

            LocationTraffic traffic;
            try
            {
                traffic = new LocationTraffic(definition, points, agents, config, random, eventBus);
            }
            catch (ArgumentException exception)
            {
                agents.Dispose();
                _logger.Error("[Gameplay] Traffic skipped: " + exception.Message);
                return;
            }

            // Why: tracked, not registered — module 11 adds a second location with its own traffic and agents.
            Track(traffic);
            Track(agents);
        }

        private void RegisterHud()
        {
            if (_balanceView == null)
            {
                _logger.Warning("[Gameplay] " + nameof(_balanceView) + " is not assigned; the balance is not shown.");
                return;
            }

            Register(new BalancePresenter(_container.Resolve<IWalletService>(), _balanceView));
        }

        private bool HasReference(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            _logger.Error("[Gameplay] " + fieldName + " is not assigned on " + name + ".");
            return false;
        }

        /// <summary>Registers a service and tracks its lifecycle interfaces.</summary>
        /// <remarks>
        /// The same instance may be registered under several contracts; it is tracked once,
        /// so <see cref="IInitializable.Initialize"/> and <see cref="ITickable.Tick"/> run once per instance.
        /// </remarks>
        private void Register<T>(T service) where T : class
        {
            _container.Register(service);

            if (service is IInitializable initializable && !ContainsReference(_initializables, initializable))
            {
                _initializables.Add(initializable);
            }

            if (service is ITickable tickable && !ContainsReference(_tickables, tickable))
            {
                _tickables.Add(tickable);
            }
        }

        /// <summary>
        /// Tracks the lifecycle of an instance that is NOT put into the container (several instances of one type):
        /// it is initialized, ticked in tracking order and disposed by this entry point.
        /// </summary>
        private void Track(object service)
        {
            if (service is IInitializable initializable && !ContainsReference(_initializables, initializable))
            {
                _initializables.Add(initializable);
            }

            if (service is ITickable tickable && !ContainsReference(_tickables, tickable))
            {
                _tickables.Add(tickable);
            }

            if (service is IDisposable disposable && !ContainsReference(_ownedDisposables, disposable))
            {
                _ownedDisposables.Add(disposable);
            }
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

        // Why: reference identity, not Equals — a service with overridden equality is still one instance to track.
        private static bool ContainsReference<TItem>(List<TItem> list, TItem item) where TItem : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                {
                    return true;
                }
            }

            return false;
        }

        private void InitializeServices()
        {
            for (int i = 0; i < _initializables.Count; i++)
            {
                _initializables[i].Initialize();
            }
        }

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

            for (int i = 0; i < _tickables.Count; i++)
            {
                _gameLoop.Add(_tickables[i]);
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
        }
    }
}
