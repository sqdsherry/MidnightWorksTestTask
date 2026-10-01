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
        /// <param name="parkOnlyChance">Chance 0..1 that a spawned car only wants to park (no service).</param>
        /// <param name="parkingStayMin">Shortest parking stay in seconds (&gt;= 0).</param>
        /// <param name="parkingStayMax">Longest parking stay in seconds (&gt;= <paramref name="parkingStayMin"/>).</param>
        /// <exception cref="ArgumentException">Thrown for out-of-range values.</exception>
        public TrafficSettings(
            float spawnInterval,
            float spawnIntervalJitter,
            int maxCarsAlive,
            float parkOnlyChance,
            float parkingStayMin,
            float parkingStayMax)
        {
            // Why: the negated comparisons also reject NaN.
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

            if (!(parkOnlyChance >= 0f && parkOnlyChance <= 1f))
            {
                throw new ArgumentException("Park-only chance must be within 0..1, got " + parkOnlyChance + ".", nameof(parkOnlyChance));
            }

            if (!(parkingStayMin >= 0f))
            {
                throw new ArgumentException("Parking stay min must be non-negative, got " + parkingStayMin + ".", nameof(parkingStayMin));
            }

            if (!(parkingStayMax >= parkingStayMin) || float.IsInfinity(parkingStayMax))
            {
                throw new ArgumentException(
                    "Parking stay max (" + parkingStayMax + ") must be finite and not less than min (" + parkingStayMin + ").",
                    nameof(parkingStayMax));
            }

            SpawnInterval = spawnInterval;
            SpawnIntervalJitter = spawnIntervalJitter;
            MaxCarsAlive = maxCarsAlive;
            ParkOnlyChance = parkOnlyChance;
            ParkingStayMin = parkingStayMin;
            ParkingStayMax = parkingStayMax;
        }

        /// <summary>Average seconds between spawn attempts.</summary>
        public float SpawnInterval { get; }

        /// <summary>Random ± deviation of the interval in seconds.</summary>
        public float SpawnIntervalJitter { get; }

        /// <summary>Upper bound of cars present in a location at once.</summary>
        public int MaxCarsAlive { get; }

        /// <summary>Chance 0..1 that a spawned car only wants to park (no service).</summary>
        public float ParkOnlyChance { get; }

        /// <summary>Shortest parking stay in seconds.</summary>
        public float ParkingStayMin { get; }

        /// <summary>Longest parking stay in seconds.</summary>
        public float ParkingStayMax { get; }
    }
}
