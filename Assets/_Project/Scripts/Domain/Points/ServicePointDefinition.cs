using System;
using AutoService.Domain.Common;

namespace AutoService.Domain.Points
{
    /// <summary>
    /// Immutable description of one service point, assembled from its service type config and the scene.
    /// </summary>
    public sealed class ServicePointDefinition
    {
        /// <summary>Creates and validates a definition.</summary>
        /// <param name="id">Unique point id (from the scene), e.g. "loc1_wash_1".</param>
        /// <param name="locationId">Id of the location the point belongs to.</param>
        /// <param name="serviceTypeId">Id of the service the point provides (from config).</param>
        /// <param name="kind">Barrier or real service.</param>
        /// <param name="basePrice">Price before the car type multiplier.</param>
        /// <param name="pricePerSecond">Extra dollars per second of the car's stay (time-based fee of the parking entrances; 0 = flat price).</param>
        /// <param name="serviceDuration">Seconds of occupied work needed to finish the service.</param>
        /// <param name="acceptDelay">Seconds the work spot must be occupied before the order is accepted.</param>
        /// <param name="clearDelay">Seconds the point stays unavailable after a car has been served.</param>
        /// <exception cref="ArgumentException">Thrown for empty ids, a negative/NaN price per second or negative/NaN durations.</exception>
        public ServicePointDefinition(
            string id,
            string locationId,
            string serviceTypeId,
            PointKind kind,
            Money basePrice,
            double pricePerSecond,
            float serviceDuration,
            float acceptDelay,
            float clearDelay)
        {
            RequireId(id, nameof(id));
            RequireId(locationId, nameof(locationId));
            RequireId(serviceTypeId, nameof(serviceTypeId));
            RequirePricePerSecond(pricePerSecond, nameof(pricePerSecond));
            RequireDuration(serviceDuration, nameof(serviceDuration));
            RequireDuration(acceptDelay, nameof(acceptDelay));
            RequireDuration(clearDelay, nameof(clearDelay));

            Id = id;
            LocationId = locationId;
            ServiceTypeId = serviceTypeId;
            Kind = kind;
            BasePrice = basePrice;
            PricePerSecond = pricePerSecond;
            ServiceDuration = serviceDuration;
            AcceptDelay = acceptDelay;
            ClearDelay = clearDelay;
        }

        /// <summary>Unique point id.</summary>
        public string Id { get; }

        /// <summary>Id of the location the point belongs to.</summary>
        public string LocationId { get; }

        /// <summary>Id of the service the point provides.</summary>
        public string ServiceTypeId { get; }

        /// <summary>Barrier or real service.</summary>
        public PointKind Kind { get; }

        /// <summary>Price before the car type multiplier.</summary>
        public Money BasePrice { get; }

        /// <summary>Extra dollars per second of the car's stay; only the parking entrances use it (see <c>PriceFormula.TimeBased</c>).</summary>
        public double PricePerSecond { get; }

        /// <summary>Seconds of occupied work needed to finish the service (0 = instant).</summary>
        public float ServiceDuration { get; }

        /// <summary>Seconds the work spot must stay occupied before the order is accepted ("taking the order" feel).</summary>
        public float AcceptDelay { get; }

        /// <summary>Seconds the point stays unavailable after the car has been served (it is driving away).</summary>
        public float ClearDelay { get; }

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Id must not be empty.", parameterName);
            }
        }

        private static void RequirePricePerSecond(double value, string parameterName)
        {
            // Why: the negated comparison also rejects NaN.
            if (!(value >= 0.0))
            {
                throw new ArgumentException("Price per second must be non-negative, got " + value + ".", parameterName);
            }
        }

        private static void RequireDuration(float value, string parameterName)
        {
            // Why: the negated comparison also rejects NaN.
            if (!(value >= 0f))
            {
                throw new ArgumentException("Duration must be a non-negative number of seconds, got " + value + ".", parameterName);
            }
        }
    }
}
