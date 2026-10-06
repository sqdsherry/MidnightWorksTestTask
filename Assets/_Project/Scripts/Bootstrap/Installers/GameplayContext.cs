using System;
using System.Collections.Generic;
using AutoService.Services.Core;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// What a <see cref="IGameplayInstaller"/> works with: the scene's <see cref="ServiceContainer"/>, the lifecycle
    /// registration (initialize / tick / dispose), the logger and the scene references of <see cref="GameplayEntryPoint"/>.
    /// </summary>
    /// <remarks>
    /// The installers and this context are part of the Composition Root together with the entry point, so seeing the
    /// container here does not turn it into a Service Locator: nothing outside <c>Bootstrap</c> ever gets the context or
    /// the container, every service still receives its dependencies through its constructor or <c>Construct(...)</c>.
    /// <para>The lifecycle lists belong to the entry point (it starts and stops ticking, disposes tracked instances);
    /// the context only appends to them.</para>
    /// </remarks>
    internal sealed class GameplayContext
    {
        private readonly ServiceContainer _container;
        private readonly List<IInitializable> _initializables;
        private readonly List<ITickable> _tickables;
        private readonly List<TickPhase> _tickPhases;
        private readonly List<IDisposable> _ownedDisposables;

        /// <summary>Creates the context.</summary>
        /// <param name="container">Scene container (child of the project container).</param>
        /// <param name="logger">Logger for setup problems.</param>
        /// <param name="scene">Scene references of the entry point.</param>
        /// <param name="ownerName">Name of the entry point object, for "assign X on Y" messages.</param>
        /// <param name="initializables">Entry point's list of services to initialize.</param>
        /// <param name="tickables">Entry point's list of services to tick, parallel to <paramref name="tickPhases"/>.</param>
        /// <param name="tickPhases">Phase of every element of <paramref name="tickables"/>.</param>
        /// <param name="ownedDisposables">Entry point's list of tracked instances it disposes on unload.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public GameplayContext(
            ServiceContainer container,
            IGameLogger logger,
            GameplaySceneRefs scene,
            string ownerName,
            List<IInitializable> initializables,
            List<ITickable> tickables,
            List<TickPhase> tickPhases,
            List<IDisposable> ownedDisposables)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            OwnerName = ownerName ?? string.Empty;
            _initializables = initializables ?? throw new ArgumentNullException(nameof(initializables));
            _tickables = tickables ?? throw new ArgumentNullException(nameof(tickables));
            _tickPhases = tickPhases ?? throw new ArgumentNullException(nameof(tickPhases));
            _ownedDisposables = ownedDisposables ?? throw new ArgumentNullException(nameof(ownedDisposables));
        }

        /// <summary>Logger for setup problems.</summary>
        public IGameLogger Logger { get; }

        /// <summary>Scene references assigned on the entry point.</summary>
        public GameplaySceneRefs Scene { get; }

        /// <summary>Name of the entry point object (where missing references have to be assigned).</summary>
        public string OwnerName { get; }

        /// <summary>
        /// Registers a service under contract <typeparamref name="T"/>; the container disposes it on unload.
        /// Its <see cref="IInitializable"/> / <see cref="ITickable"/> are tracked once per instance.
        /// </summary>
        /// <param name="service">The instance.</param>
        /// <param name="phase">When it ticks, if it is an <see cref="ITickable"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown when <typeparamref name="T"/> is already registered in the scene.</exception>
        public void Register<T>(T service, TickPhase phase = TickPhase.Presentation) where T : class
        {
            _container.Register(service);
            AddLifecycle(service, phase);
        }

        /// <summary>
        /// Tracks an instance that is NOT put into the container (several instances of one type): it is initialized,
        /// ticked in its phase and disposed by the entry point.
        /// </summary>
        /// <param name="service">The instance.</param>
        /// <param name="phase">When it ticks, if it is an <see cref="ITickable"/>.</param>
        public void Track(object service, TickPhase phase = TickPhase.Presentation)
        {
            AddLifecycle(service, phase);
            if (service is IDisposable disposable && !ContainsReference(_ownedDisposables, disposable))
            {
                _ownedDisposables.Add(disposable);
            }
        }

        /// <summary>Resolves <typeparamref name="T"/> from the scene or project container.</summary>
        /// <exception cref="InvalidOperationException">Thrown when <typeparamref name="T"/> is not registered.</exception>
        public T Resolve<T>() where T : class => _container.Resolve<T>();

        /// <summary>Tries to resolve <typeparamref name="T"/> from the scene or project container.</summary>
        public bool TryResolve<T>(out T instance) where T : class => _container.TryResolve(out instance);

        /// <summary>Logs an error when a scene reference is missing.</summary>
        /// <param name="reference">The serialized reference.</param>
        /// <param name="fieldName">Its field name on the entry point.</param>
        /// <returns>True when assigned.</returns>
        public bool HasReference(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Logger.Error("[Gameplay] " + fieldName + " is not assigned on " + OwnerName + ".");
            return false;
        }

        private void AddLifecycle(object service, TickPhase phase)
        {
            if (service is IInitializable initializable && !ContainsReference(_initializables, initializable))
            {
                _initializables.Add(initializable);
            }

            if (service is ITickable tickable && !ContainsReference(_tickables, tickable))
            {
                _tickables.Add(tickable);
                _tickPhases.Add(phase);
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
    }
}
