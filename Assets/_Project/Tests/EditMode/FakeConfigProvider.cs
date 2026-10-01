using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Traffic;
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

        /// <inheritdoc />
        public TrafficSettings Traffic { get; set; } = new TrafficSettings(7f, 0f, 12, 0f, 0f, 0f);

        /// <inheritdoc />
        public bool TryGetServiceType(string id, out ServiceTypeSettings settings)
        {
            settings = ServiceTypeList.Find(type => string.Equals(type.Id, id, StringComparison.Ordinal));
            return settings != null;
        }
    }
}
