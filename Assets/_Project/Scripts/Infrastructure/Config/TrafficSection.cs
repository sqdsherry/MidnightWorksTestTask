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

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance that a spawned car only wants to park (pays the parking fee, stays, leaves without a service).")]
        private float _parkOnlyChance = 0.4f;

        [SerializeField, Min(0f)]
        [Tooltip("Shortest time a car stays parked before it goes to a service point or leaves, in seconds.")]
        private float _parkingStayMin = 6f;

        [SerializeField, Min(0f)]
        [Tooltip("Longest parking stay, in seconds. Must not be less than the minimum.")]
        private float _parkingStayMax = 15f;

        /// <summary>Average seconds between car spawn attempts.</summary>
        public float SpawnInterval => _spawnInterval;

        /// <summary>Random ± deviation of the spawn interval, in seconds.</summary>
        public float SpawnIntervalJitter => _spawnIntervalJitter;

        /// <summary>Maximum number of cars present in a location at once.</summary>
        public int MaxCarsAlive => _maxCarsAlive;

        /// <summary>Chance 0..1 that a spawned car only wants to park.</summary>
        public float ParkOnlyChance => _parkOnlyChance;

        /// <summary>Shortest parking stay, in seconds.</summary>
        public float ParkingStayMin => _parkingStayMin;

        /// <summary>Longest parking stay, in seconds.</summary>
        public float ParkingStayMax => _parkingStayMax;

        /// <summary>Clamps inconsistent inspector input. Called from <see cref="GameConfig"/>'s <c>OnValidate</c>.</summary>
        public void Sanitize()
        {
            // Why: [Min] cannot express "max >= min"; fixing it while editing beats a config exception at Play.
            _parkingStayMax = Mathf.Max(_parkingStayMin, _parkingStayMax);
        }
    }
}
