using AutoService.Domain.Common;
using AutoService.Domain.Points;

namespace AutoService.Services.Points
{
    /// <summary>
    /// Bus event: an order was accepted at a point and its price has just been credited (for "+$" popups and sound).
    /// Published by <see cref="ServicePointService"/>.
    /// </summary>
    public readonly struct OrderAcceptedEvent
    {
        /// <summary>Creates the event.</summary>
        public OrderAcceptedEvent(string pointId, string serviceTypeId, PointKind kind, Money price)
        {
            PointId = pointId;
            ServiceTypeId = serviceTypeId;
            Kind = kind;
            Price = price;
        }

        /// <summary>Id of the point that accepted the order.</summary>
        public string PointId { get; }

        /// <summary>Service type of the point.</summary>
        public string ServiceTypeId { get; }

        /// <summary>Barrier (parking fee) or real service.</summary>
        public PointKind Kind { get; }

        /// <summary>Credited amount.</summary>
        public Money Price { get; }
    }
}
