using System;

namespace AutoService.Services.Save
{
    /// <summary>
    /// Saved state of one service point; an element of <see cref="SaveData.points"/>.
    /// </summary>
    /// <remarks>
    /// Public fields for the same reason as <see cref="SaveData"/>: JsonUtility serializes only public fields here,
    /// because <c>UnityEngine</c> (and so <c>[SerializeField]</c>) is not available in the Services assembly.
    /// </remarks>
    [Serializable]
    public sealed class PointSaveData
    {
        /// <summary>Value of <see cref="supply"/> when the point's supply was not saved.</summary>
        public const int UnsavedSupply = -1;

        /// <summary>Id of the point this entry belongs to.</summary>
        public string pointId;

        /// <summary>Purchased speed upgrade level.</summary>
        public int speedLevel;

        /// <summary>Purchased price upgrade level.</summary>
        public int priceLevel;

        /// <summary>Remaining supply, or <see cref="UnsavedSupply"/> if it was not saved.</summary>
        public int supply = UnsavedSupply;

        /// <summary>True if a worker is hired for the point.</summary>
        public bool hasWorker;
    }
}
