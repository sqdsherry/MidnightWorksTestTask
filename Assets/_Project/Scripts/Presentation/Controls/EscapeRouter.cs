using System;
using System.Collections.Generic;

namespace AutoService.Presentation.Controls
{
    /// <summary>
    /// One Esc for the whole scene: it goes to the most recently opened <see cref="IEscapeHandler"/>, and only when
    /// nobody takes it, <see cref="Unhandled"/> is raised (the pause menu opens). So Esc closes an open panel first and
    /// pauses otherwise.
    /// </summary>
    /// <remarks>
    /// Plain C# with no input dependency — <see cref="HandleEscape"/> is the input (wired to the Esc key by
    /// <see cref="EscapeInputBinding"/>), which keeps the routing testable.
    /// </remarks>
    public sealed class EscapeRouter
    {
        // Why: a list used as a stack — a closed panel is removed from wherever it is, not only from the top.
        private readonly List<IEscapeHandler> _handlers = new List<IEscapeHandler>();

        /// <summary>Raised when Esc was pressed and no handler took it.</summary>
        public event Action Unhandled;

        /// <summary>Number of registered handlers.</summary>
        public int Count => _handlers.Count;

        /// <summary>Puts <paramref name="handler"/> on top; a handler already registered moves to the top.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
        public void Push(IEscapeHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Remove(handler);
            _handlers.Add(handler);
        }

        /// <summary>Removes <paramref name="handler"/>. Unknown or null handlers are ignored.</summary>
        public void Remove(IEscapeHandler handler)
        {
            int index = IndexOf(handler);
            if (index >= 0)
            {
                _handlers.RemoveAt(index);
            }
        }

        /// <summary>Gives Esc to the handlers from the top down; raises <see cref="Unhandled"/> if none took it.</summary>
        public void HandleEscape()
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                // Why: a handler may close (remove) others while handling; skip indices that no longer exist.
                if (i >= _handlers.Count)
                {
                    continue;
                }

                if (_handlers[i].TryHandleEscape())
                {
                    return;
                }
            }

            Unhandled?.Invoke();
        }

        // Why: reference identity, not Equals — every registered object is its own entry.
        private int IndexOf(IEscapeHandler handler)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (ReferenceEquals(_handlers[i], handler))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
