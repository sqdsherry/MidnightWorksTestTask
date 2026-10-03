using System;
using AutoService.Domain.Common;
using AutoService.Domain.Points;

namespace AutoService.Services.Config
{
    /// <summary>
    /// Immutable settings of one service type (parking, wash...). Every point in the scene references a type by <see cref="Id"/>.
    /// </summary>
    public sealed class ServiceTypeSettings
    {
        /// <summary>Creates and validates the settings.</summary>
        /// <param name="id">Unique id referenced by scene points.</param>
        /// <param name="displayName">Player-facing name (English).</param>
        /// <param name="kind">Whether points of this type are barriers or real services.</param>
        /// <param name="basePrice">Price before the car type multiplier.</param>
        /// <param name="pricePerSecond">Extra dollars per second of the car's stay (parking entrance fee; 0 = flat price).</param>
        /// <param name="serviceDuration">Seconds of occupied work per car.</param>
        /// <param name="acceptDelay">Seconds of presence before the order is accepted.</param>
        /// <param name="clearDelay">Seconds the point stays unavailable after a car was served.</param>
        /// <param name="supplyTypeId">Consumable every order uses; empty = none (the barriers).</param>
        /// <param name="supplyCapacity">Units a point's stock holds; must be &gt; 0 when <paramref name="supplyTypeId"/> is set.</param>
        /// <param name="worker">Hiring settings of the point worker, or null when points of this type cannot hire one.</param>
        /// <exception cref="ArgumentException">
        /// Thrown for an empty id, a negative/NaN price per second, negative/NaN durations or a supply type without capacity.
        /// </exception>
        public ServiceTypeSettings(
            string id,
            string displayName,
            PointKind kind,
            Money basePrice,
            double pricePerSecond,
            float serviceDuration,
            float acceptDelay,
            float clearDelay,
            string supplyTypeId = "",
            int supplyCapacity = 0,
            PointWorkerSettings worker = null,
            int xpReward = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Service type id must not be empty.", nameof(id));
            }

            // Why: the negated comparison also rejects NaN.
            if (!(pricePerSecond >= 0.0))
            {
                throw new ArgumentException("Price per second must be non-negative, got " + pricePerSecond + ".", nameof(pricePerSecond));
            }

            RequireDuration(serviceDuration, nameof(serviceDuration));
            RequireDuration(acceptDelay, nameof(acceptDelay));
            RequireDuration(clearDelay, nameof(clearDelay));
            bool needsSupply = !string.IsNullOrWhiteSpace(supplyTypeId);
            if (needsSupply && supplyCapacity <= 0)
            {
                throw new ArgumentException(
                    "Supply capacity must be positive for supply type '" + supplyTypeId + "', got " + supplyCapacity + ".", nameof(supplyCapacity));
            }

            if (xpReward < 0)
            {
                throw new ArgumentException("XP reward must be non-negative.", nameof(xpReward));
            }

            Id = id;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            BasePrice = basePrice;
            PricePerSecond = pricePerSecond;
            ServiceDuration = serviceDuration;
            AcceptDelay = acceptDelay;
            ClearDelay = clearDelay;
            SupplyTypeId = needsSupply ? supplyTypeId : string.Empty;
            SupplyCapacity = needsSupply ? supplyCapacity : 0;
            Worker = worker;
            XpReward = xpReward;
        }

        /// <summary>Unique id referenced by scene points.</summary>
        public string Id { get; }

        /// <summary>Player-facing name.</summary>
        public string DisplayName { get; }

        /// <summary>Whether points of this type are barriers or real services.</summary>
        public PointKind Kind { get; }

        /// <summary>Price before the car type multiplier.</summary>
        public Money BasePrice { get; }

        /// <summary>Extra dollars per second of the car's stay; used by the parking entrances' time-based fee.</summary>
        public double PricePerSecond { get; }

        /// <summary>Seconds of occupied work per car.</summary>
        public float ServiceDuration { get; }

        /// <summary>Seconds of presence before the order is accepted.</summary>
        public float AcceptDelay { get; }

        /// <summary>Seconds the point stays unavailable after a car was served.</summary>
        public float ClearDelay { get; }

        /// <summary>Consumable every order uses; empty when points of this type need none.</summary>
        public string SupplyTypeId { get; }

        /// <summary>Units a point's stock holds (0 without a consumable).</summary>
        public int SupplyCapacity { get; }

        /// <summary>Hiring settings of the point worker, or null when points of this type cannot hire one.</summary>
        public PointWorkerSettings Worker { get; }

        /// <summary>XP granted to the player when this service is completed.</summary>
        public int XpReward { get; }

        /// <summary>Builds the definition of a concrete point of this type.</summary>
        /// <param name="pointId">Unique point id from the scene.</param>
        /// <param name="locationId">Id of the location the point belongs to.</param>
        /// <exception cref="ArgumentException">Thrown for empty ids.</exception>
        public ServicePointDefinition CreatePointDefinition(string pointId, string locationId)
        {
            return new ServicePointDefinition(
                pointId, locationId, Id, Kind, BasePrice, PricePerSecond, ServiceDuration, AcceptDelay, ClearDelay, SupplyTypeId, SupplyCapacity);
        }

        private static void RequireDuration(float value, string parameterName)
        {
            if (!(value >= 0f))
            {
                throw new ArgumentException("Duration must be a non-negative number of seconds, got " + value + ".", parameterName);
            }
        }
    }
}
