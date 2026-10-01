using System;
using System.Collections.Generic;
using AutoService.Domain.Economy;
using AutoService.Presentation.CameraControl;
using AutoService.Presentation.Controls;
using AutoService.Presentation.Interaction;
using AutoService.Presentation.Player;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Events;
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

        // Why: lifecycle lists are filled while registering, so every service created here is initialized
        // and ticked without each module having to remember to wire itself in.
        private readonly List<IInitializable> _initializables = new List<IInitializable>();
        private readonly List<ITickable> _tickables = new List<ITickable>();

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

            InitializeServices();
            StartTicking();

            _logger.Info("[Gameplay] Ready. Balance: " + MoneyFormatter.Format(_container.Resolve<IWalletService>().Balance));
        }

        private void OnDestroy()
        {
            StopTicking();
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
