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
        /// <exception cref="ArgumentException">Thrown for an empty id, a negative/NaN price per second or negative/NaN durations.</exception>
        public ServiceTypeSettings(
            string id,
            string displayName,
            PointKind kind,
            Money basePrice,
            double pricePerSecond,
            float serviceDuration,
            float acceptDelay,
            float clearDelay)
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

            Id = id;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            BasePrice = basePrice;
            PricePerSecond = pricePerSecond;
            ServiceDuration = serviceDuration;
            AcceptDelay = acceptDelay;
            ClearDelay = clearDelay;
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

        /// <summary>Builds the definition of a concrete point of this type.</summary>
        /// <param name="pointId">Unique point id from the scene.</param>
        /// <param name="locationId">Id of the location the point belongs to.</param>
        /// <exception cref="ArgumentException">Thrown for empty ids.</exception>
        public ServicePointDefinition CreatePointDefinition(string pointId, string locationId)
        {
            return new ServicePointDefinition(pointId, locationId, Id, Kind, BasePrice, PricePerSecond, ServiceDuration, AcceptDelay, ClearDelay);
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
