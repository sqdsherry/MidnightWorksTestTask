using System;
using System.Collections.Generic;
using AutoService.Services.Core;

namespace AutoService.Services.Events
{
    /// <summary>
    /// Allocation-free <see cref="IEventBus"/> implementation.
    /// </summary>
    /// <remarks>
    /// Each event type gets its own typed channel holding a handler list plus an immutable array snapshot.
    /// <see cref="Publish{T}"/> iterates the snapshot by index, so it allocates nothing and handlers may freely
    /// subscribe or unsubscribe during delivery: a change builds a new snapshot, the ongoing delivery keeps using the old one.
    /// Allocation happens only on subscribe/unsubscribe, which are rare compared to publishing.
    /// </remarks>
    public sealed class EventBus : IEventBus
    {
        // Why: object values hold Channel<T> instances; the cast back is a reference cast, so no boxing occurs.
        private readonly Dictionary<Type, object> _channels = new Dictionary<Type, object>();
        private readonly IGameLogger _logger;

        /// <summary>Creates the bus.</summary>
        /// <param name="logger">Receives exceptions thrown by handlers.</param>
        public EventBus(IGameLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            GetOrCreateChannel<T>().Add(handler);
        }

        /// <inheritdoc />
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                return;
            }

            if (_channels.TryGetValue(typeof(T), out object channel))
            {
                ((Channel<T>)channel).Remove(handler);
            }
        }

        /// <inheritdoc />
        /// <remarks>An exception thrown by one handler is logged and does not prevent delivery to the others.</remarks>
        public void Publish<T>(in T gameEvent) where T : struct
        {
            if (!_channels.TryGetValue(typeof(T), out object channel))
            {
                return;
            }

            Action<T>[] snapshot = ((Channel<T>)channel).Snapshot;
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i](gameEvent);
                }
                catch (Exception exception)
                {
                    _logger.Error("[EventBus] Handler for " + typeof(T).Name + " threw: " + exception);
                }
            }
        }

        private Channel<T> GetOrCreateChannel<T>() where T : struct
        {
            if (_channels.TryGetValue(typeof(T), out object existing))
            {
                return (Channel<T>)existing;
            }

            var created = new Channel<T>();
            _channels.Add(typeof(T), created);
            return created;
        }

        /// <summary>Subscribers of a single event type.</summary>
        private sealed class Channel<T> where T : struct
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>();

            /// <summary>Immutable copy of the handlers used for delivery.</summary>
            public Action<T>[] Snapshot { get; private set; } = Array.Empty<Action<T>>();

            public void Add(Action<T> handler)
            {
                if (_handlers.Contains(handler))
                {
                    return;
                }

                _handlers.Add(handler);
                Snapshot = _handlers.ToArray();
            }

            public void Remove(Action<T> handler)
            {
                if (_handlers.Remove(handler))
                {
                    Snapshot = _handlers.Count == 0 ? Array.Empty<Action<T>>() : _handlers.ToArray();
                }
            }
        }
    }
}
