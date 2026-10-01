namespace AutoService.Services.Supplies
{
    /// <summary>Bus event: a box was put into a point's stock (onboarding, sound). Published by <see cref="SupplyService"/>.</summary>
    public readonly struct SupplyDeliveredEvent
    {
        /// <summary>Creates the event.</summary>
        public SupplyDeliveredEvent(string pointId, string supplyTypeId, int units, bool byPlayer)
        {
            PointId = pointId;
            SupplyTypeId = supplyTypeId;
            Units = units;
            ByPlayer = byPlayer;
        }

        /// <summary>Receiving point.</summary>
        public string PointId { get; }

        /// <summary>Delivered consumable.</summary>
        public string SupplyTypeId { get; }

        /// <summary>Delivered units.</summary>
        public int Units { get; }

        /// <summary>True when the player delivered it, false for the storekeeper.</summary>
        public bool ByPlayer { get; }
    }
}
