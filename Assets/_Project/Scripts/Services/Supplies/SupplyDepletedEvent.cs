namespace AutoService.Services.Supplies
{
    /// <summary>
    /// Bus event: a point's stock has just become empty — it stops accepting orders until a box arrives
    /// (onboarding hint, speech bubbles). Published by <see cref="SupplyService"/>.
    /// </summary>
    public readonly struct SupplyDepletedEvent
    {
        /// <summary>Creates the event.</summary>
        public SupplyDepletedEvent(string pointId, string supplyTypeId)
        {
            PointId = pointId;
            SupplyTypeId = supplyTypeId;
        }

        /// <summary>Point that ran out.</summary>
        public string PointId { get; }

        /// <summary>Consumable it needs.</summary>
        public string SupplyTypeId { get; }
    }
}
