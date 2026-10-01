using System;

namespace AutoService.Services.Config
{
    /// <summary>Immutable car flow settings.</summary>
    public sealed class TrafficSettings
    {
        /// <summary>Creates and validates the settings.</summary>
        /// <param name="spawnInterval">Average seconds between spawn attempts (&gt; 0).</param>
        /// <param name="spawnIntervalJitter">Random ± deviation of the interval in seconds (&gt;= 0).</param>
        /// <param name="maxCarsAlive">Upper bound of cars present in a location at once (&gt;= 1).</param>
        /// <exception cref="ArgumentException">Thrown for out-of-range values.</exception>
        public TrafficSettings(float spawnInterval, float spawnIntervalJitter, int maxCarsAlive)
        {
            if (!(spawnInterval > 0f))
            {
                throw new ArgumentException("Spawn interval must be positive, got " + spawnInterval + ".", nameof(spawnInterval));
            }

            if (!(spawnIntervalJitter >= 0f))
            {
                throw new ArgumentException("Spawn interval jitter must be non-negative, got " + spawnIntervalJitter + ".", nameof(spawnIntervalJitter));
            }

            if (maxCarsAlive < 1)
            {
                throw new ArgumentException("Max cars alive must be at least 1, got " + maxCarsAlive + ".", nameof(maxCarsAlive));
            }

            SpawnInterval = spawnInterval;
            SpawnIntervalJitter = spawnIntervalJitter;
            MaxCarsAlive = maxCarsAlive;
        }

        /// <summary>Average seconds between spawn attempts.</summary>
        public float SpawnInterval { get; }

        /// <summary>Random ± deviation of the interval in seconds.</summary>
        public float SpawnIntervalJitter { get; }

        /// <summary>Upper bound of cars present in a location at once.</summary>
        public int MaxCarsAlive { get; }
    }
}
