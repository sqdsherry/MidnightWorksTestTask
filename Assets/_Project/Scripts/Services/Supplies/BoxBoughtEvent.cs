using AutoService.Domain.Common;

namespace AutoService.Services.Supplies
{
    /// <summary>Bus event: a box was bought at a warehouse (onboarding, sound). Published by <see cref="SupplyService"/>.</summary>
    public readonly struct BoxBoughtEvent
    {
        /// <summary>Creates the event.</summary>
        public BoxBoughtEvent(string locationId, string supplyTypeId, Money price, bool byPlayer)
        {
            LocationId = locationId;
            SupplyTypeId = supplyTypeId;
            Price = price;
            ByPlayer = byPlayer;
        }

        /// <summary>Location of the warehouse.</summary>
        public string LocationId { get; }

        /// <summary>Consumable in the box.</summary>
        public string SupplyTypeId { get; }

        /// <summary>Charged price.</summary>
        public Money Price { get; }

        /// <summary>True when the player bought it, false for the storekeeper.</summary>
        public bool ByPlayer { get; }
    }
}
