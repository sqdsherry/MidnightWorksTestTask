using System;
using System.Collections.Generic;
using AutoService.Domain.Economy;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Formatting;
using UnityEngine;

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

        /// <summary>Registers a service and tracks its lifecycle interfaces.</summary>
        private void Register<T>(T service) where T : class
        {
            _container.Register(service);

            if (service is IInitializable initializable)
            {
                _initializables.Add(initializable);
            }

            if (service is ITickable tickable)
            {
                _tickables.Add(tickable);
            }
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
