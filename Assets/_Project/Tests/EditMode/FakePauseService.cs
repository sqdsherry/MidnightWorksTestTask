using System;
using AutoService.Services.Core;

namespace AutoService.Tests.EditMode
{
    /// <summary><see cref="IPauseService"/> that counts requests and calls.</summary>
    public sealed class FakePauseService : IPauseService
    {
        /// <summary>Active pause requests.</summary>
        public int Requests { get; private set; }

        /// <summary>Number of <see cref="Push"/> calls.</summary>
        public int PushCount { get; private set; }

        /// <summary>Number of <see cref="Pop"/> calls.</summary>
        public int PopCount { get; private set; }

        /// <summary>Number of <see cref="ResetAll"/> calls.</summary>
        public int ResetCount { get; private set; }

        /// <inheritdoc />
        public event Action<bool> PausedChanged;

        /// <inheritdoc />
        public bool IsPaused => Requests > 0;

        /// <inheritdoc />
        public void Push()
        {
            PushCount++;
            Requests++;
            if (Requests == 1)
            {
                PausedChanged?.Invoke(true);
            }
        }

        /// <inheritdoc />
        public void Pop()
        {
            PopCount++;
            if (Requests == 0)
            {
                return;
            }

            Requests--;
            if (Requests == 0)
            {
                PausedChanged?.Invoke(false);
            }
        }

        /// <inheritdoc />
        public void ResetAll()
        {
            ResetCount++;
            bool wasPaused = Requests > 0;
            Requests = 0;
            if (wasPaused)
            {
                PausedChanged?.Invoke(false);
            }
        }
    }
}
