using System;
using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Car flow block of <see cref="GameConfig"/> as authored in the inspector.
    /// </summary>
    [Serializable]
    public sealed class TrafficSection
    {
        [SerializeField, Min(0.1f)]
        [Tooltip("Average seconds between car spawn attempts.")]
        private float _spawnInterval = 7f;

        [SerializeField, Min(0f)]
        [Tooltip("Random ± deviation of the spawn interval, in seconds.")]
        private float _spawnIntervalJitter = 2f;

        [SerializeField, Min(1)]
        [Tooltip("Maximum number of cars present in a location at once.")]
        private int _maxCarsAlive = 12;

        /// <summary>Average seconds between car spawn attempts.</summary>
        public float SpawnInterval => _spawnInterval;

        /// <summary>Random ± deviation of the spawn interval, in seconds.</summary>
        public float SpawnIntervalJitter => _spawnIntervalJitter;

        /// <summary>Maximum number of cars present in a location at once.</summary>
        public int MaxCarsAlive => _maxCarsAlive;
    }
}
