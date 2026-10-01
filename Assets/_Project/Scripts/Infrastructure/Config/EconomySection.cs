using System;
using UnityEngine;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// Economy block of <see cref="GameConfig"/> as authored in the inspector.
    /// </summary>
    [Serializable]
    public sealed class EconomySection
    {
        [SerializeField, Min(0)]
        [Tooltip("Money the player has at the start of a new game, in whole dollars.")]
        private long _startingMoney = 100;

        /// <summary>Money the player has at the start of a new game, in whole dollars.</summary>
        public long StartingMoney => _startingMoney;
    }
}
