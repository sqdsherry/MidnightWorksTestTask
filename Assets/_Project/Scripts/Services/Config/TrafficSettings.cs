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
        /// <param name="parkOnlyWeight">Spawn weight of the "parking only" plan (&gt;= 0).</param>
        /// <param name="serviceOnlyWeight">Spawn weight of the "service only" plan (&gt;= 0).</param>
        /// <param name="serviceThenParkWeight">Spawn weight of the "service, then parking" plan (&gt;= 0).</param>
        /// <param name="parkingStayMin">Shortest parking stay in seconds (&gt;= 0).</param>
        /// <param name="parkingStayMax">Longest parking stay in seconds (&gt;= <paramref name="parkingStayMin"/>).</param>
        /// <exception cref="ArgumentException">Thrown for out-of-range values or when all plan weights are zero.</exception>
        public TrafficSettings(
            float spawnInterval,
            float spawnIntervalJitter,
            int maxCarsAlive,
            int parkOnlyWeight,
            int serviceOnlyWeight,
            int serviceThenParkWeight,
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

            if (parkOnlyWeight < 0 || serviceOnlyWeight < 0 || serviceThenParkWeight < 0)
            {
                throw new ArgumentException(
                    "Visit plan weights must be non-negative, got " + parkOnlyWeight + "/" + serviceOnlyWeight + "/" + serviceThenParkWeight + ".",
                    nameof(parkOnlyWeight));
            }

            // Why: long sum, so absurdly large weights cannot overflow into a "valid" total.
            long totalPlanWeight = (long)parkOnlyWeight + serviceOnlyWeight + serviceThenParkWeight;
            if (totalPlanWeight <= 0 || totalPlanWeight > int.MaxValue)
            {
                throw new ArgumentException("Visit plan weights must sum to 1.." + int.MaxValue + ", got " + totalPlanWeight + ".", nameof(parkOnlyWeight));
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
            ParkOnlyWeight = parkOnlyWeight;
            ServiceOnlyWeight = serviceOnlyWeight;
            ServiceThenParkWeight = serviceThenParkWeight;
            ParkingStayMin = parkingStayMin;
            ParkingStayMax = parkingStayMax;
        }

        /// <summary>Average seconds between spawn attempts.</summary>
        public float SpawnInterval { get; }

        /// <summary>Random ± deviation of the interval in seconds.</summary>
        public float SpawnIntervalJitter { get; }

        /// <summary>Upper bound of cars present in a location at once.</summary>
        public int MaxCarsAlive { get; }

        /// <summary>Spawn weight of <c>CarVisitPlan.ParkOnly</c>.</summary>
        public int ParkOnlyWeight { get; }

        /// <summary>Spawn weight of <c>CarVisitPlan.WashOnly</c> (any service, then leave).</summary>
        public int ServiceOnlyWeight { get; }

        /// <summary>Spawn weight of <c>CarVisitPlan.WashThenPark</c> (any service, then park).</summary>
        public int ServiceThenParkWeight { get; }

        /// <summary>Shortest parking stay in seconds (rolled when the car is booked in at an entrance).</summary>
        public float ParkingStayMin { get; }

        /// <summary>Longest parking stay in seconds.</summary>
        public float ParkingStayMax { get; }
    }
}
