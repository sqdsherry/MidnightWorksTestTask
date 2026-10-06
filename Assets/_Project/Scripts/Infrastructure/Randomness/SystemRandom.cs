using System;
using AutoService.Services.Core;

namespace AutoService.Infrastructure.Randomness
{
    /// <summary>
    /// <see cref="IRandom"/> backed by <see cref="System.Random"/>.
    /// Named without "Unity" to avoid confusion with <c>UnityEngine.Random</c>; unlike the latter it is an instance,
    /// so it can be seeded independently and holds no global state.
    /// </summary>
    public sealed class SystemRandom : IRandom
    {
        private readonly System.Random _random;

        /// <summary>Creates a generator with a time-dependent seed.</summary>
        public SystemRandom()
        {
            _random = new System.Random();
        }

        /// <summary>Creates a generator with a fixed seed (reproducible sequences).</summary>
        /// <param name="seed">Seed value.</param>
        public SystemRandom(int seed)
        {
            _random = new System.Random(seed);
        }

        /// <inheritdoc />
        public float Value()
        {
            // Why: casting NextDouble() to float can round values just below 1 up to exactly 1f; clamp to keep [0, 1).
            float value = (float)_random.NextDouble();
            return value < 1f ? value : 0.99999994f;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minInclusive"/> &gt; <paramref name="maxExclusive"/>.</exception>
        public int Range(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }
}
