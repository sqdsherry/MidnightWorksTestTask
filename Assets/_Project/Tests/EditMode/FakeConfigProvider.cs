using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Traffic;
using AutoService.Domain.Upgrades;
using AutoService.Services.Config;

namespace AutoService.Tests.EditMode
{
    /// <summary>In-memory <see cref="IConfigProvider"/> for tests.</summary>
    public sealed class FakeConfigProvider : IConfigProvider
    {
        /// <inheritdoc />
        public EconomySettings Economy { get; set; } = new EconomySettings(Money.Zero);

        /// <summary>Mutable list behind <see cref="ServiceTypes"/>.</summary>
        public List<ServiceTypeSettings> ServiceTypeList { get; } = new List<ServiceTypeSettings>();

        /// <summary>Mutable list behind <see cref="CarTypes"/>.</summary>
        public List<CarType> CarTypeList { get; } = new List<CarType>();

        /// <inheritdoc />
        public IReadOnlyList<ServiceTypeSettings> ServiceTypes => ServiceTypeList;

        /// <inheritdoc />
        public IReadOnlyList<CarType> CarTypes => CarTypeList;

        /// <summary>Mutable list behind <see cref="Buildables"/>.</summary>
        public List<BuildableSettings> BuildableList { get; } = new List<BuildableSettings>();

        /// <inheritdoc />
        public IReadOnlyList<BuildableSettings> Buildables => BuildableList;

        /// <summary>Mutable list behind <see cref="SupplyTypes"/>.</summary>
        public List<SupplyTypeSettings> SupplyTypeList { get; } = new List<SupplyTypeSettings>();

        /// <inheritdoc />
        public IReadOnlyList<SupplyTypeSettings> SupplyTypes => SupplyTypeList;

        /// <summary>Mutable list behind <see cref="Upgrades"/>.</summary>
        public List<UpgradeSettings> UpgradeList { get; } = new List<UpgradeSettings>();

        /// <inheritdoc />
        public IReadOnlyList<UpgradeSettings> Upgrades => UpgradeList;

        /// <inheritdoc />
        public StaffSettings Staff { get; set; } = new StaffSettings("Storekeeper", "Carries boxes", new Money(600), 3, 5, 3, 1.5);

        /// <inheritdoc />
        public ProgressionSettings Progression { get; set; } = new ProgressionSettings(new[] { 0, 15, 35, 70, 110 }, 50);

        /// <inheritdoc />
        public TrafficSettings Traffic { get; set; } = new TrafficSettings(7f, 0f, 12, 35, 35, 30, 0f, 0f);

        /// <inheritdoc />
        public bool TryGetServiceType(string id, out ServiceTypeSettings settings)
        {
            settings = ServiceTypeList.Find(type => string.Equals(type.Id, id, StringComparison.Ordinal));
            return settings != null;
        }

        /// <inheritdoc />
        public bool TryGetSupplyType(string id, out SupplyTypeSettings settings)
        {
            settings = SupplyTypeList.Find(type => string.Equals(type.Id, id, StringComparison.Ordinal));
            return settings != null;
        }

        /// <inheritdoc />
        public bool TryGetUpgrade(UpgradeKind kind, out UpgradeSettings settings)
        {
            settings = UpgradeList.Find(upgrade => upgrade.Kind == kind);
            return settings != null;
        }

        /// <inheritdoc />
        public bool TryGetBuildable(string id, out BuildableSettings settings)
        {
            settings = BuildableList.Find(buildable => string.Equals(buildable.Id, id, StringComparison.Ordinal));
            return settings != null;
        }
    }
}
