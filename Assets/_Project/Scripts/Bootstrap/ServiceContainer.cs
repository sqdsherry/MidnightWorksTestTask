using System;
using System.Collections.Generic;

namespace AutoService.Bootstrap
{
    /// <summary>
    /// Minimal hand-written dependency container with parent lookup.
    /// </summary>
    /// <remarks>
    /// The container is used ONLY inside entry points (<see cref="ProjectEntryPoint"/>, scene entry points).
    /// Everything else receives its dependencies through constructors (C# classes) or <c>Construct(...)</c> methods
    /// (MonoBehaviours) — this is a Composition Root, not a Service Locator. Never pass the container itself around.
    /// <para>
    /// A scene container is created as a child of the project container, so <see cref="Resolve{T}"/> falls back to
    /// project-wide services, while disposing the scene container releases only the scene's own services.
    /// </para>
    /// </remarks>
    public sealed class ServiceContainer : IDisposable
    {
        private readonly ServiceContainer _parent;
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();

        // Why: kept separately in registration order so disposal can run in reverse (dependents before dependencies).
        private readonly List<object> _registrationOrder = new List<object>();
        private bool _disposed;

        /// <summary>Creates a container.</summary>
        /// <param name="parent">Optional parent consulted when a contract is not registered here.</param>
        public ServiceContainer(ServiceContainer parent = null)
        {
            _parent = parent;
        }

        /// <summary>Registers an instance under contract <typeparamref name="T"/>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="T"/> is already registered in THIS container.</exception>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose"/>.</exception>
        public void Register<T>(T instance) where T : class
        {
            ThrowIfDisposed();

            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance), "Cannot register null for " + typeof(T).FullName + ".");
            }

            Type contract = typeof(T);
            if (_instances.ContainsKey(contract))
            {
                throw new InvalidOperationException("Service " + contract.FullName + " is already registered in this container.");
            }

            _instances.Add(contract, instance);
            _registrationOrder.Add(instance);
        }

        /// <summary>Resolves <typeparamref name="T"/> from this container or its parents.</summary>
        /// <exception cref="InvalidOperationException">Thrown when <typeparamref name="T"/> is not registered anywhere in the chain; the message names the missing type.</exception>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose"/>.</exception>
        public T Resolve<T>() where T : class
        {
            if (TryResolve(out T instance))
            {
                return instance;
            }

            throw new InvalidOperationException("Service " + typeof(T).FullName + " is not registered.");
        }

        /// <summary>Tries to resolve <typeparamref name="T"/> from this container or its parents.</summary>
        /// <returns>True if found; otherwise false and <paramref name="instance"/> is null.</returns>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose"/>.</exception>
        public bool TryResolve<T>(out T instance) where T : class
        {
            ThrowIfDisposed();

            for (ServiceContainer container = this; container != null; container = container._parent)
            {
                if (container._instances.TryGetValue(typeof(T), out object found))
                {
                    instance = (T)found;
                    return true;
                }
            }

            instance = null;
            return false;
        }

        /// <summary>
        /// Disposes every registered <see cref="IDisposable"/> exactly once (same instance under several contracts → once),
        /// in reverse registration order. Parents are not disposed. Safe to call repeatedly.
        /// </summary>
        /// <exception cref="AggregateException">Thrown after all services were disposed if any of them threw.</exception>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            List<Exception> errors = null;
            var disposed = new List<IDisposable>(_registrationOrder.Count);

            for (int i = _registrationOrder.Count - 1; i >= 0; i--)
            {
                if (!(_registrationOrder[i] is IDisposable disposable) || ContainsReference(disposed, disposable))
                {
                    continue;
                }

                disposed.Add(disposable);
                try
                {
                    disposable.Dispose();
                }
                catch (Exception exception)
                {
                    // Why: one failing service must not leave the others (event subscriptions, files) undisposed.
                    errors ??= new List<Exception>();
                    errors.Add(exception);
                }
            }

            _instances.Clear();
            _registrationOrder.Clear();

            if (errors != null)
            {
                throw new AggregateException("One or more services threw while being disposed.", errors);
            }
        }

        // Why: reference identity, not Equals — a service with overridden equality must still be disposed once per instance.
        private static bool ContainsReference(List<IDisposable> list, IDisposable item)
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ServiceContainer));
            }
        }
    }
}
