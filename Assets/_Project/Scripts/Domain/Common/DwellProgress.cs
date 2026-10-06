using System;

namespace AutoService.Domain.Common
{
    /// <summary>
    /// "Stand here for a moment" timer: fills while someone dwells, drains faster once they leave, and reports
    /// completion once per visit. Shared by build plots, point panels and incidents.
    /// </summary>
    /// <remarks>
    /// Draining instead of resetting means a player who steps away for a second does not start over,
    /// while walking past a plot does not open it by accident.
    /// </remarks>
    public sealed class DwellProgress
    {
        private readonly float _duration;
        private readonly float _decayMultiplier;
        private bool _completed;

        /// <summary>Creates an empty timer.</summary>
        /// <param name="duration">Seconds of dwelling needed to complete (&gt;= 0; 0 completes on the first tick).</param>
        /// <param name="decayMultiplier">How many times faster the progress drains than it fills (&gt; 0).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative/NaN duration or a non-positive/NaN multiplier.</exception>
        public DwellProgress(float duration, float decayMultiplier = 3f)
        {
            // Why: the negated comparisons also reject NaN.
            if (!(duration >= 0f) || float.IsInfinity(duration))
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Dwell duration must be a finite non-negative number of seconds.");
            }

            if (!(decayMultiplier > 0f) || float.IsInfinity(decayMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(decayMultiplier), decayMultiplier, "Decay multiplier must be a finite positive number.");
            }

            _duration = duration;
            _decayMultiplier = decayMultiplier;
        }

        /// <summary>True between <see cref="Begin"/> and <see cref="End"/>.</summary>
        public bool IsDwelling { get; private set; }

        /// <summary>Progress 0..1.</summary>
        public float Progress01 { get; private set; }

        /// <summary>Someone started dwelling; the progress fills from its current value.</summary>
        public void Begin()
        {
            IsDwelling = true;
        }

        /// <summary>They left; the progress starts draining and the next visit can complete again.</summary>
        public void End()
        {
            IsDwelling = false;
            _completed = false;
        }

        /// <summary>Drops the progress to 0 without changing <see cref="IsDwelling"/>.</summary>
        public void Reset()
        {
            Progress01 = 0f;
            _completed = false;
        }

        /// <summary>Advances the timer.</summary>
        /// <param name="deltaTime">Seconds since the last tick (0 while paused).</param>
        /// <returns>True exactly once per visit: on the tick the progress reaches 1.</returns>
        public bool Tick(float deltaTime)
        {
            if (!(deltaTime > 0f))
            {
                return false;
            }

            if (!IsDwelling)
            {
                if (Progress01 > 0f)
                {
                    float drain = _duration > 0f ? deltaTime * _decayMultiplier / _duration : 1f;
                    Progress01 = Math.Max(0f, Progress01 - drain);
                }

                return false;
            }

            if (_completed)
            {
                return false;
            }

            Progress01 = _duration > 0f ? Math.Min(1f, Progress01 + deltaTime / _duration) : 1f;
            if (Progress01 < 1f)
            {
                return false;
            }

            _completed = true;
            return true;
        }
    }
}
