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

        [SerializeField, Min(0)]
        [Tooltip("Spawn weight of the \"parking only\" plan: pays at the main entrance, parks, leaves.")]
        private int _parkOnlyWeight = 35;

        [SerializeField, Min(0)]
        [Tooltip("Spawn weight of the \"service only\" plan: gets a service, leaves by the top road.")]
        private int _serviceOnlyWeight = 35;

        [SerializeField, Min(0)]
        [Tooltip("Spawn weight of the \"service, then parking\" plan: gets a service, then parks through the service entrance.")]
        private int _serviceThenParkWeight = 30;

        [SerializeField, Min(0f)]
        [Tooltip("Shortest parking stay, in seconds. The stay is rolled and paid for at the entrance barrier.")]
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

        /// <summary>Spawn weight of the "parking only" plan.</summary>
        public int ParkOnlyWeight => _parkOnlyWeight;

        /// <summary>Spawn weight of the "service only" plan.</summary>
        public int ServiceOnlyWeight => _serviceOnlyWeight;

        /// <summary>Spawn weight of the "service, then parking" plan.</summary>
        public int ServiceThenParkWeight => _serviceThenParkWeight;

        /// <summary>Shortest parking stay, in seconds.</summary>
        public float ParkingStayMin => _parkingStayMin;

        /// <summary>Longest parking stay, in seconds.</summary>
        public float ParkingStayMax => _parkingStayMax;

        /// <summary>Clamps inconsistent inspector input. Called from <see cref="GameConfig"/>'s <c>OnValidate</c>.</summary>
        public void Sanitize()
        {
            // Why: [Min] cannot express "max >= min"; fixing it while editing beats a config exception at Play.
            _parkingStayMax = Mathf.Max(_parkingStayMin, _parkingStayMax);

            // Why: all-zero weights would leave no plan to pick; fall back to parking only rather than fail at Play.
            if (_parkOnlyWeight + _serviceOnlyWeight + _serviceThenParkWeight <= 0)
            {
                _parkOnlyWeight = 1;
            }
        }
    }
}
