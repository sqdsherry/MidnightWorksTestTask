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
        [Tooltip("Hiring price of the first storekeeper, in whole dollars.")]
        private long _storekeeperCost = 600;

        [SerializeField, Min(0)]
        [Tooltip("Player level needed to hire the storekeeper.")]
        private int _storekeeperRequiredLevel = 3;

        [SerializeField, Min(0)]
        [Tooltip("A storekeeper goes for a box when a point holds this many units or fewer (boxes on their way included).")]
        private int _restockAtOrBelow = 5;

        [SerializeField, Min(1)]
        [Tooltip("Storekeepers that can be hired per location.")]
        private int _maxStorekeepers = 3;

        [SerializeField, Min(1f)]
        [Tooltip("Price multiplier per storekeeper already hired: cost × growth^hired (600 → 900 → 1350 at 1.5).")]
        private float _storekeeperCostGrowth = 1.5f;

        /// <summary>Player-facing title of the storekeeper.</summary>
        public string StorekeeperTitle => _storekeeperTitle;

        /// <summary>Player-facing description of the storekeeper offer.</summary>
        public string StorekeeperDescription => _storekeeperDescription;

        /// <summary>Hiring price of the first storekeeper, in whole dollars.</summary>
        public long StorekeeperCost => _storekeeperCost;

        /// <summary>Player level needed to hire the storekeeper.</summary>
        public int StorekeeperRequiredLevel => _storekeeperRequiredLevel;

        /// <summary>A point holding this many units or fewer gets restocked.</summary>
        public int RestockAtOrBelow => _restockAtOrBelow;

        /// <summary>Storekeepers per location.</summary>
        public int MaxStorekeepers => _maxStorekeepers;

        /// <summary>Price multiplier per storekeeper already hired.</summary>
        public float StorekeeperCostGrowth => _storekeeperCostGrowth;
    }
}
