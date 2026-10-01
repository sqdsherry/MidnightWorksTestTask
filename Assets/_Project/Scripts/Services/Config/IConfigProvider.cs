using System.Collections.Generic;
using AutoService.Domain.Traffic;

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

        /// <summary>Looks up a service type by id.</summary>
        /// <returns>False (and null) when no type has this id.</returns>
        bool TryGetServiceType(string id, out ServiceTypeSettings settings);
    }
}
