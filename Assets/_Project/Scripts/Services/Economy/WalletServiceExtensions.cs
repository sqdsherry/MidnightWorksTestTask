using AutoService.Domain.Common;

namespace AutoService.Services.Economy
{
    /// <summary>
    /// Helper extension methods for <see cref="IWalletService"/>.
    /// </summary>
    public static class WalletServiceExtensions
    {
        /// <summary>Adds a non-negative amount of money in dollars.</summary>
        public static void AddMoney(this IWalletService wallet, long amount)
        {
            if (wallet != null && amount > 0)
            {
                wallet.Add(new Money(amount));
            }
        }
    }
}
