using System;
using AutoService.Services.Core;
using UnityEngine;

namespace AutoService.Infrastructure.Pause
{
    /// <summary>
    /// <see cref="IPauseService"/> that pauses the game by setting <c>Time.timeScale</c> to 0.
    /// Gameplay then stops by itself (it receives a zero delta), while UI animations keep running on unscaled time.
    /// </summary>
    public sealed class TimeScalePauseService : IPauseService
    {
        private readonly IGameLogger _logger;
        private int _requestCount;

        /// <summary>Creates the service.</summary>
        /// <param name="logger">Receives a warning on unbalanced <see cref="Pop"/> calls.</param>
        public TimeScalePauseService(IGameLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public event Action<bool> PausedChanged;

        /// <inheritdoc />
        public bool IsPaused => _requestCount > 0;

        /// <inheritdoc />
        public void Push()
        {
            _requestCount++;
            if (_requestCount == 1)
            {
                SetTimeScale(0f);
                PausedChanged?.Invoke(true);
            }
        }

        /// <inheritdoc />
        public void Pop()
        {
            if (_requestCount == 0)
            {
                _logger.Warning("[Pause] Pop() called without a matching Push(); ignored.");
                return;
            }

            _requestCount--;
            if (_requestCount == 0)
            {
                SetTimeScale(1f);
                PausedChanged?.Invoke(false);
            }
        }

        /// <inheritdoc />
        public void ResetAll()
        {
            bool wasPaused = _requestCount > 0;
            _requestCount = 0;

            // Why: set even when not paused — whatever left the time scale off, the next scene must start at normal speed.
            SetTimeScale(1f);
            if (wasPaused)
            {
                PausedChanged?.Invoke(false);
            }
        }

        private static void SetTimeScale(float scale) => Time.timeScale = scale;
    }
}
