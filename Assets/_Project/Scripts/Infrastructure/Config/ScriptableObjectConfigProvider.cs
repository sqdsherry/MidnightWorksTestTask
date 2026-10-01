using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Traffic;
using AutoService.Services.Config;

namespace AutoService.Infrastructure.Config
{
    /// <summary>
    /// <see cref="IConfigProvider"/> that maps a <see cref="GameConfig"/> asset into engine-agnostic settings.
    /// </summary>
    /// <remarks>
    /// Mapping happens once in the constructor: consumers get immutable snapshots, and editing the asset
    /// during Play Mode cannot change the rules underneath running systems.
    /// <para>
    /// Why fail fast: a broken config (empty slot, duplicate or empty id, invalid value) is an authoring error that would
    /// otherwise surface much later as a car that never spawns. The exception names the offending asset.
    /// </para>
    /// </remarks>
    public sealed class ScriptableObjectConfigProvider : IConfigProvider
    {
        private readonly List<ServiceTypeSettings> _serviceTypes = new List<ServiceTypeSettings>();
        private readonly List<CarType> _carTypes = new List<CarType>();
        private readonly Dictionary<string, ServiceTypeSettings> _serviceTypesById = new Dictionary<string, ServiceTypeSettings>(StringComparer.Ordinal);

        /// <summary>Maps <paramref name="config"/> into settings.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null (e.g. not assigned in the inspector).</exception>
        /// <exception cref="InvalidOperationException">Thrown when the config contains empty slots, empty/duplicate ids or invalid values.</exception>
        public ScriptableObjectConfigProvider(GameConfig config)
        {
            // Why: Unity's overloaded == also catches a missing/destroyed asset, not just a null reference.
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "GameConfig is not assigned.");
            }

            Economy = new EconomySettings(new Money(config.Economy.StartingMoney));
            MapServiceTypes(config);
            MapCarTypes(config);
            Traffic = MapTraffic(config);
        }

        /// <inheritdoc />
        public EconomySettings Economy { get; }

        /// <inheritdoc />
        public IReadOnlyList<ServiceTypeSettings> ServiceTypes => _serviceTypes;

        /// <inheritdoc />
        public IReadOnlyList<CarType> CarTypes => _carTypes;

        /// <inheritdoc />
        public TrafficSettings Traffic { get; }

        /// <inheritdoc />
        public bool TryGetServiceType(string id, out ServiceTypeSettings settings)
        {
            if (id == null)
            {
                settings = null;
                return false;
            }

            return _serviceTypesById.TryGetValue(id, out settings);
        }

        private void MapServiceTypes(GameConfig config)
        {
            ServiceTypeConfig[] assets = config.ServiceTypes ?? Array.Empty<ServiceTypeConfig>();
            for (int i = 0; i < assets.Length; i++)
            {
                ServiceTypeConfig asset = assets[i];
                if (asset == null)
                {
                    throw Error(config, "Service Types element " + i + " is empty.");
                }

                ServiceTypeSettings settings;
                try
                {
                    settings = new ServiceTypeSettings(
                        asset.Id,
                        asset.DisplayName,
                        asset.Kind,
                        new Money(asset.BasePrice),
                        asset.ServiceDuration,
                        asset.AcceptDelay,
                        asset.ClearDelay);
                }
                catch (ArgumentException exception)
                {
                    throw Error(config, "Service type '" + asset.name + "' is invalid: " + exception.Message, exception);
                }

                if (_serviceTypesById.ContainsKey(settings.Id))
                {
                    throw Error(config, "Service type '" + asset.name + "' has duplicate id '" + settings.Id + "'.");
                }

                _serviceTypesById.Add(settings.Id, settings);
                _serviceTypes.Add(settings);
            }
        }

        private void MapCarTypes(GameConfig config)
        {
            CarTypeConfig[] assets = config.CarTypes ?? Array.Empty<CarTypeConfig>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < assets.Length; i++)
            {
                CarTypeConfig asset = assets[i];
                if (asset == null)
                {
                    throw Error(config, "Car Types element " + i + " is empty.");
                }

                CarType carType;
                try
                {
                    carType = new CarType(asset.Id, asset.SpawnWeight, asset.PriceMultiplier, asset.Patience);
                }
                catch (ArgumentException exception)
                {
                    throw Error(config, "Car type '" + asset.name + "' is invalid: " + exception.Message, exception);
                }

                if (!ids.Add(carType.Id))
                {
                    throw Error(config, "Car type '" + asset.name + "' has duplicate id '" + carType.Id + "'.");
                }

                _carTypes.Add(carType);
            }
        }

        private static TrafficSettings MapTraffic(GameConfig config)
        {
            TrafficSection section = config.Traffic ?? new TrafficSection();
            try
            {
                return new TrafficSettings(section.SpawnInterval, section.SpawnIntervalJitter, section.MaxCarsAlive);
            }
            catch (ArgumentException exception)
            {
                throw Error(config, "Traffic section is invalid: " + exception.Message, exception);
            }
        }

        private static InvalidOperationException Error(GameConfig config, string message, Exception inner = null)
        {
            return new InvalidOperationException("GameConfig '" + config.name + "': " + message, inner);
        }
    }
}
