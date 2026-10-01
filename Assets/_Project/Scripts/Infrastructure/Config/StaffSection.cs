using System;
using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Staff block of <see cref="GameConfig"/> as authored in the inspector: the storekeeper offer and its restocking rule.
    /// Point workers are configured per service type (<see cref="ServiceTypeConfig"/>).
    /// </summary>
    [Serializable]
    public sealed class StaffSection
    {
        [SerializeField]
        [Tooltip("Player-facing title of the storekeeper (English).")]
        private string _storekeeperTitle = "Storekeeper";

        [SerializeField, TextArea(2, 4)]
        [Tooltip("Player-facing description of the storekeeper offer (English).")]
        private string _storekeeperDescription = "Carries boxes to the hungriest bay";

        [SerializeField, Min(0)]
        [Tooltip("One-time hiring price of the storekeeper, in whole dollars.")]
        private long _storekeeperCost = 600;

        [SerializeField, Min(0)]
        [Tooltip("Player level needed to hire the storekeeper.")]
        private int _storekeeperRequiredLevel = 3;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("The storekeeper goes for a box when a point's stock drops below this share of its capacity.")]
        private float _restockThreshold = 0.5f;

        /// <summary>Player-facing title of the storekeeper.</summary>
        public string StorekeeperTitle => _storekeeperTitle;

        /// <summary>Player-facing description of the storekeeper offer.</summary>
        public string StorekeeperDescription => _storekeeperDescription;

        /// <summary>One-time hiring price, in whole dollars.</summary>
        public long StorekeeperCost => _storekeeperCost;

        /// <summary>Player level needed to hire the storekeeper.</summary>
        public int StorekeeperRequiredLevel => _storekeeperRequiredLevel;

        /// <summary>Fill level (0..1) below which the storekeeper restocks a point.</summary>
        public float RestockThreshold => _restockThreshold;
    }
}
