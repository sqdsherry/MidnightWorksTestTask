using System.Collections.Generic;
using AutoService.Domain.Traffic;
using AutoService.Domain.Upgrades;

namespace AutoService.Services.Config
{
    /// <summary>
    /// Read-only game configuration exposed to the game in engine-agnostic form.
    /// Domain and Services never see ScriptableObjects; Infrastructure maps them into these settings once.
    /// </summary>
    public interface IConfigProvider
    {
        /// <summary>Global economy settings.</summary>
        EconomySettings Economy { get; }

        /// <summary>All service types (parking, wash...), in config order. Ids are unique.</summary>
        IReadOnlyList<ServiceTypeSettings> ServiceTypes { get; }

        /// <summary>All car types, in config order. Ids are unique.</summary>
        IReadOnlyList<CarType> CarTypes { get; }

        /// <summary>Car flow settings.</summary>
        TrafficSettings Traffic { get; }

        /// <summary>All buildables (bays, extra parking slots), in config order. Ids are unique.</summary>
        IReadOnlyList<BuildableSettings> Buildables { get; }

        /// <summary>All consumables (shampoo, oil...), in config order. Ids are unique; every service type's supply id is one of them.</summary>
        IReadOnlyList<SupplyTypeSettings> SupplyTypes { get; }

        /// <summary>Point upgrades, at most one per <see cref="UpgradeKind"/>, in config order.</summary>
        IReadOnlyList<UpgradeSettings> Upgrades { get; }

        /// <summary>Location-wide staff settings (storekeeper).</summary>
        StaffSettings Staff { get; }

        /// <summary>Player progression settings.</summary>
        ProgressionSettings Progression { get; }

        /// <summary>Looks up a service type by id.</summary>
        /// <returns>False (and null) when no type has this id.</returns>
        bool TryGetServiceType(string id, out ServiceTypeSettings settings);

        /// <summary>Looks up a buildable by id.</summary>
        /// <returns>False (and null) when no buildable has this id.</returns>
        bool TryGetBuildable(string id, out BuildableSettings settings);

        /// <summary>Looks up a consumable by id.</summary>
        /// <returns>False (and null) when no consumable has this id.</returns>
        bool TryGetSupplyType(string id, out SupplyTypeSettings settings);

        /// <summary>Looks up the upgrade of a kind.</summary>
        /// <returns>False (and null) when the config has no upgrade of this kind.</returns>
        bool TryGetUpgrade(UpgradeKind kind, out UpgradeSettings settings);
    }
}
