using System.Collections.Generic;
using AutoService.Services.Core;

namespace AutoService.Tests.EditMode
{
    /// <summary>Scripted <see cref="IRandom"/>: returns queued values, then defaults (0.5 for Value, the minimum for Range).</summary>
    public sealed class FakeRandom : IRandom
    {
        /// <summary>Values returned by <see cref="Value"/> before falling back to 0.5.</summary>
        public Queue<float> Values { get; } = new Queue<float>();

        /// <summary>Values returned by <see cref="Range"/> before falling back to the minimum.</summary>
        public Queue<int> Ranges { get; } = new Queue<int>();

        /// <inheritdoc />
        public float Value() => Values.Count > 0 ? Values.Dequeue() : 0.5f;

        /// <inheritdoc />
        public int Range(int minInclusive, int maxExclusive) => Ranges.Count > 0 ? Ranges.Dequeue() : minInclusive;
    }
}
