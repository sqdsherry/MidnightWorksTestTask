using System;

namespace AutoService.Presentation.Controls
{
    /// <summary>Sends the Esc key of <see cref="GameplayInput"/> to the scene's <see cref="EscapeRouter"/>.</summary>
    public sealed class EscapeInputBinding : IDisposable
    {
        private readonly GameplayInput _input;
        private readonly EscapeRouter _router;
        private bool _disposed;

        /// <summary>Subscribes the router to the Esc key.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public EscapeInputBinding(GameplayInput input, EscapeRouter router)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _input.CancelPressed += _router.HandleEscape;
        }

        /// <summary>Unsubscribes. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _input.CancelPressed -= _router.HandleEscape;
        }
    }
}
