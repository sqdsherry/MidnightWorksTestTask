using System;

namespace AutoService.Services.Save
{
    /// <summary>
    /// Versioned snapshot of the player's progress, written to disk as JSON.
    /// Every <see cref="ISaveable"/> writes and reads its own slice of it.
    /// </summary>
    /// <remarks>
    /// This DTO is the only kind of type in the project with public fields: JsonUtility serializes only public or
    /// <c>[SerializeField]</c> fields, and <c>UnityEngine</c> (where <c>SerializeField</c> lives) is not available
    /// in the Services assembly. Field names are lower camelCase because they double as the JSON keys.
    /// JsonUtility has no dictionaries, so keyed data is stored as arrays (e.g. <see cref="points"/>).
    /// </remarks>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Format version written by this build. Bump it together with a new step in the save migration.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Format version of this snapshot; stamped by the save service on write.</summary>
        public int version;

        /// <summary>UTC time of the write in <see cref="DateTime.Ticks"/>; stamped by the save service on write.</summary>
        public long savedAtUtcTicks;

        /// <summary>Wallet balance in whole dollars.</summary>
        public long money;

        /// <summary>Experience collected towards the next level.</summary>
        public int xp;

        /// <summary>Player level.</summary>
        public int level;

        /// <summary>Ids of build plots that have been built.</summary>
        public string[] builtPlotIds;

        /// <summary>Per-point upgrades, supplies and staff.</summary>
        public PointSaveData[] points;

        /// <summary>Ids of locations with a hired storekeeper.</summary>
        public string[] storekeeperLocationIds;

        /// <summary>Ids of unlocked locations.</summary>
        public string[] unlockedLocationIds;

        /// <summary>VIP client loyalty.</summary>
        public float vipLoyalty;

        /// <summary>Index of the current onboarding step.</summary>
        public int tutorialStep;

        /// <summary>Creates a snapshot of the current version with every array empty (never null).</summary>
        public static SaveData CreateEmpty()
        {
            return new SaveData
            {
                version = CurrentVersion,
                builtPlotIds = Array.Empty<string>(),
                points = Array.Empty<PointSaveData>(),
                storekeeperLocationIds = Array.Empty<string>(),
                unlockedLocationIds = Array.Empty<string>(),
            };
        }
    }
}
