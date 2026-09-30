using AutoService.Domain.Common;

namespace AutoService.Services.Config
{
    /// <summary>
    /// Immutable economy settings.
    /// </summary>
    public sealed class EconomySettings
    {
        /// <summary>Creates the settings.</summary>
        /// <param name="startingMoney">Balance of a new game.</param>
        public EconomySettings(Money startingMoney)
        {
            StartingMoney = startingMoney;
        }

        /// <summary>Balance of a new game.</summary>
        public Money StartingMoney { get; }
    }
}
