using System;

namespace AutoService.Services.Events
{
    /// <summary>
    /// Publish/subscribe channel for cross-cutting game events that many unrelated systems listen to
    /// (XP, onboarding, audio, speech bubbles). Events are structs so publishing does not allocate.
    /// </summary>
    /// <remarks>
    /// Use a plain C# event on the source service instead when the listener already knows its source.
    /// </remarks>
    public interface IEventBus
    {
        /// <summary>Subscribes <paramref name="handler"/> to events of type <typeparamref name="T"/>. Subscribing the same delegate twice has no effect.</summary>
        void Subscribe<T>(Action<T> handler) where T : struct;

        /// <summary>Removes a previously subscribed <paramref name="handler"/>. Unknown handlers are ignored.</summary>
        void Unsubscribe<T>(Action<T> handler) where T : struct;

        /// <summary>Delivers <paramref name="gameEvent"/> to every current subscriber of <typeparamref name="T"/>.</summary>
        void Publish<T>(in T gameEvent) where T : struct;
    }
}
