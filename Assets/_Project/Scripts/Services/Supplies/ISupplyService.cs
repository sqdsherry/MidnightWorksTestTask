using AutoService.Domain.Common;
using AutoService.Domain.Points;
using AutoService.Domain.Supplies;

namespace AutoService.Services.Supplies
{
    /// <summary>
    /// Warehouse and deliveries: which point needs a box most, buying a box, delivering it, and the storekeeper's
    /// "box on its way" reservations.
    /// </summary>
    /// <remarks>
    /// A bought box is NOT bound to a point: the player may carry it to any point of the same consumable. Only the
    /// storekeeper's box counts as incoming (<see cref="MarkIncoming"/>), so two boxes are not sent to one nearly full point.
    /// </remarks>
    public interface ISupplyService
    {
        /// <summary>
        /// Point of the location with the lowest fill among points whose stock can take a whole box,
        /// counting boxes already on their way (storekeeper targets).
        /// </summary>
        /// <returns>The point, or null when every stock is full enough.</returns>
        ServicePoint FindHungriest(string locationId);

        /// <summary>Hungriest point (as <see cref="FindHungriest"/>) among those that use <paramref name="box"/>'s consumable and can take it.</summary>
        /// <returns>The point, or null when none can take the box.</returns>
        ServicePoint FindRestockTarget(string locationId, in SupplyBox box);

        /// <summary>Price of one box of <paramref name="supplyTypeId"/>.</summary>
        /// <exception cref="System.ArgumentException">Thrown for an unknown consumable.</exception>
        Money GetBoxPrice(string supplyTypeId);

        /// <summary>Charges the wallet and returns a box for <see cref="FindHungriest"/>'s consumable.</summary>
        /// <param name="locationId">Location of the warehouse.</param>
        /// <param name="byPlayer">True for the player, false for the storekeeper (reported in <see cref="BoxBoughtEvent"/>).</param>
        /// <param name="box">The bought box, or <see cref="SupplyBox.None"/>.</param>
        /// <param name="target">
        /// The hungriest point the box was picked for. Null only when there is nothing to restock; when the purchase fails
        /// for lack of money it is still set, so the caller can show the price ("Need $15").
        /// </param>
        /// <returns>False (nothing charged) when there is nothing to restock or the balance does not cover the box.</returns>
        bool TryBuyBoxForHungriest(string locationId, bool byPlayer, out SupplyBox box, out ServicePoint target);

        /// <summary>
        /// Charges the wallet and returns a box of <paramref name="pointId"/>'s consumable (the storekeeper buys for the
        /// point it reserved). Does not check whether the box still fits — the caller decided that.
        /// </summary>
        /// <param name="pointId">Point with a stock.</param>
        /// <param name="byPlayer">True for the player, false for the storekeeper.</param>
        /// <param name="box">The bought box, or <see cref="SupplyBox.None"/>.</param>
        /// <returns>False (nothing charged) for a point without a stock or when the balance does not cover the box.</returns>
        bool TryBuyBoxFor(string pointId, bool byPlayer, out SupplyBox box);

        /// <summary>Units the storekeepers carry to <paramref name="pointId"/> right now (<see cref="MarkIncoming"/>); 0 for unknown points.</summary>
        int GetIncoming(string pointId);

        /// <summary>True when <paramref name="pointId"/> uses <paramref name="box"/>'s consumable and its stock can take the whole box.</summary>
        bool CanDeliver(in SupplyBox box, string pointId);

        /// <summary>Puts the box into the point's stock (when <see cref="CanDeliver"/>).</summary>
        /// <param name="box">The delivered box.</param>
        /// <param name="pointId">Receiving point.</param>
        /// <param name="byPlayer">True for the player, false for the storekeeper (reported in <see cref="SupplyDeliveredEvent"/>).</param>
        /// <returns>False (nothing changes) when the box does not fit or is of another consumable.</returns>
        bool TryDeliver(in SupplyBox box, string pointId, bool byPlayer);

        /// <summary>A storekeeper is going to bring <paramref name="units"/> to the point (counted by <see cref="FindHungriest"/>).</summary>
        void MarkIncoming(string pointId, int units);

        /// <summary>Removes a reservation of <see cref="MarkIncoming"/> (delivered, redirected or dropped).</summary>
        void ClearIncoming(string pointId, int units);
    }
}
