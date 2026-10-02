using AutoService.Domain.Common;
using AutoService.Domain.Upgrades;

namespace AutoService.Services.Upgrades
{
    /// <summary>
    /// Bus event: the player bought a point upgrade level (XP, onboarding, sound). Not published for restored levels.
    /// Published by <see cref="UpgradeService"/>.
    /// </summary>
    public readonly struct UpgradePurchasedEvent
    {
        /// <summary>Creates the event.</summary>
        public UpgradePurchasedEvent(string pointId, UpgradeKind kind, int newLevel, Money cost)
        {
            PointId = pointId;
            Kind = kind;
            NewLevel = newLevel;
            Cost = cost;
        }

        /// <summary>Upgraded point.</summary>
        public string PointId { get; }

        /// <summary>Upgrade kind.</summary>
        public UpgradeKind Kind { get; }

        /// <summary>Level after the purchase.</summary>
        public int NewLevel { get; }

        /// <summary>Paid price.</summary>
        public Money Cost { get; }
    }
}
