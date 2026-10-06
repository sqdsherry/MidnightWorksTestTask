using System;
using AutoService.Domain.Common;
using AutoService.Services.Core;
using AutoService.Services.Save;

namespace AutoService.Services.Economy
{
    /// <summary>Saves and restores the wallet balance (<see cref="SaveData.money"/>).</summary>
    public sealed class WalletSaveable : ISaveable
    {
        private readonly IWalletService _wallet;
        private readonly IGameLogger _logger;

        /// <summary>Creates the saveable.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public WalletSaveable(IWalletService wallet, IGameLogger logger)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void Capture(SaveData data)
        {
            data.money = _wallet.Balance.Amount;
        }

        /// <inheritdoc />
        public void Restore(SaveData data)
        {
            // Why: a hand-edited or corrupted file may hold a negative amount, which Money cannot represent.
            if (data.money < 0L)
            {
                _logger.Warning("[Save] Negative balance " + data.money + " in the save; restored as 0.");
                _wallet.Restore(Money.Zero);
                return;
            }

            _wallet.Restore(new Money(data.money));
        }
    }
}
