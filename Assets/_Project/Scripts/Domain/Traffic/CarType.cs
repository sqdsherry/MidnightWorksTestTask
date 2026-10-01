using System;

namespace AutoService.Domain.Traffic
{
    /// <summary>Immutable data of a car type (sedan, SUV, sports car...).</summary>
    public sealed class CarType
    {
        /// <summary>Creates and validates a car type.</summary>
        /// <param name="id">Unique id (from config), also used to pick the visual prefab.</param>
        /// <param name="spawnWeight">Relative spawn chance (&gt; 0).</param>
        /// <param name="priceMultiplier">Multiplier applied to every price this car pays (&gt; 0).</param>
        /// <param name="patience">Seconds the car is willing to wait in total (&gt; 0).</param>
        /// <exception cref="ArgumentException">Thrown for an empty id or out-of-range values.</exception>
        public CarType(string id, int spawnWeight, double priceMultiplier, float patience)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Car type id must not be empty.", nameof(id));
            }

            if (spawnWeight <= 0)
            {
                throw new ArgumentException("Spawn weight must be positive, got " + spawnWeight + ".", nameof(spawnWeight));
            }

            // Why: the negated comparisons also reject NaN.
            if (!(priceMultiplier > 0.0) || double.IsInfinity(priceMultiplier))
            {
                throw new ArgumentException("Price multiplier must be a positive finite number, got " + priceMultiplier + ".", nameof(priceMultiplier));
            }

            if (!(patience > 0f))
            {
                throw new ArgumentException("Patience must be positive, got " + patience + ".", nameof(patience));
            }

            Id = id;
            SpawnWeight = spawnWeight;
            PriceMultiplier = priceMultiplier;
            Patience = patience;
        }

        /// <summary>Unique id.</summary>
        public string Id { get; }

        /// <summary>Relative spawn chance.</summary>
        public int SpawnWeight { get; }

        /// <summary>Multiplier applied to every price this car pays.</summary>
        public double PriceMultiplier { get; }

        /// <summary>Seconds the car is willing to wait in total.</summary>
        public float Patience { get; }
    }
}
