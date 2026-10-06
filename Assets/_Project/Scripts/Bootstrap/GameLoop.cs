using System;
using System.Collections.Generic;
using AutoService.Services.Core;
using UnityEngine;

namespace AutoService.Bootstrap
{
    /// <summary>
    /// The single gameplay <c>Update</c> in the project: ticks every registered <see cref="ITickable"/> once per frame
    /// with the scaled delta time (0 while paused).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Tick order = order of <see cref="Add"/> calls.</item>
    /// <item><see cref="Add"/>/<see cref="Remove"/> called from inside a tick are deferred and applied at the start of the next <c>Update</c>;
    /// outside a tick they apply immediately.</item>
    /// <item>Steady state is allocation-free: tickables live in an array iterated by index (no interface enumerator),
    /// the array grows by doubling, and the pending-operation list keeps its capacity.</item>
    /// </list>
    /// </remarks>
    public sealed class GameLoop : MonoBehaviour
    {
        private const int InitialCapacity = 16;

        private readonly List<PendingOperation> _pending = new List<PendingOperation>();
        private ITickable[] _tickables = new ITickable[InitialCapacity];
        private int _count;
        private bool _isTicking;

        /// <summary>Adds <paramref name="tickable"/> to the end of the tick order. Adding an already added tickable has no effect.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tickable"/> is null.</exception>
        public void Add(ITickable tickable)
        {
            if (tickable == null)
            {
                throw new ArgumentNullException(nameof(tickable));
            }

            if (_isTicking)
            {
                _pending.Add(new PendingOperation(tickable, isAdd: true));
                return;
            }

            AddNow(tickable);
        }

        /// <summary>Removes <paramref name="tickable"/>, preserving the order of the others. Unknown tickables are ignored.</summary>
        public void Remove(ITickable tickable)
        {
            if (tickable == null)
            {
                return;
            }

            if (_isTicking)
            {
                _pending.Add(new PendingOperation(tickable, isAdd: false));
                return;
            }

            RemoveNow(tickable);
        }

        private void Update()
        {
            ApplyPending();

            float deltaTime = Time.deltaTime;
            _isTicking = true;
            try
            {
                for (int i = 0; i < _count; i++)
                {
                    _tickables[i].Tick(deltaTime);
                }
            }
            finally
            {
                // Why: an exception in one tickable must not leave the loop stuck in "deferred" mode forever.
                _isTicking = false;
            }
        }

        private void ApplyPending()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _pending.Count; i++)
            {
                PendingOperation operation = _pending[i];
                if (operation.IsAdd)
                {
                    AddNow(operation.Tickable);
                }
                else
                {
                    RemoveNow(operation.Tickable);
                }
            }

            _pending.Clear();
        }

        private void AddNow(ITickable tickable)
        {
            if (IndexOf(tickable) >= 0)
            {
                return;
            }

            if (_count == _tickables.Length)
            {
                Array.Resize(ref _tickables, _tickables.Length * 2);
            }

            _tickables[_count++] = tickable;
        }

        private void RemoveNow(ITickable tickable)
        {
            int index = IndexOf(tickable);
            if (index < 0)
            {
                return;
            }

            // Why: shift instead of swap-with-last so the documented tick order is preserved.
            _count--;
            Array.Copy(_tickables, index + 1, _tickables, index, _count - index);
            _tickables[_count] = null;
        }

        private int IndexOf(ITickable tickable)
        {
            for (int i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_tickables[i], tickable))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>An add/remove request made during a tick.</summary>
        private readonly struct PendingOperation
        {
            public PendingOperation(ITickable tickable, bool isAdd)
            {
                Tickable = tickable;
                IsAdd = isAdd;
            }

            public ITickable Tickable { get; }

            public bool IsAdd { get; }
        }
    }
}
