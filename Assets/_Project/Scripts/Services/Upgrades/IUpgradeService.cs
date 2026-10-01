using System;
using AutoService.Domain.Common;
using AutoService.Domain.Upgrades;

namespace AutoService.Services.Upgrades
{
    /// <summary>
    /// Speed and price upgrades of every registered point: levels, costs, buying, and applying the effect to the point.
    /// </summary>
    public interface IUpgradeService
    {
        /// <summary>Raised after a level was bought or restored, with the point id and the kind.</summary>
        event Action<string, UpgradeKind> Upgraded;

        /// <summary>Current level (0 = not upgraded; also 0 for a kind missing from the config).</summary>
        /// <exception cref="ArgumentException">Thrown for an unknown point.</exception>
        int GetLevel(string pointId, UpgradeKind kind);

        /// <summary>Price of the next level; zero when maxed or not supported.</summary>
        /// <exception cref="ArgumentException">Thrown for an unknown point.</exception>
        Money GetNextCost(string pointId, UpgradeKind kind);

        /// <summary>Whether the next level can be bought now.</summary>
        /// <exception cref="ArgumentException">Thrown for an unknown point.</exception>
        UpgradeAvailability GetAvailability(string pointId, UpgradeKind kind);

        /// <summary>Pays for and applies the next level if it is <see cref="UpgradeAvailability.Available"/>.</summary>
        /// <returns>False (nothing changes) for an unknown point or an unavailable level.</returns>
        bool TryUpgrade(string pointId, UpgradeKind kind);

        /// <summary>Sets a saved level without payment (clamped) and applies it. Unknown points are ignored (save from an older layout).</summary>
        void Restore(string pointId, UpgradeKind kind, int level);
    }
}
