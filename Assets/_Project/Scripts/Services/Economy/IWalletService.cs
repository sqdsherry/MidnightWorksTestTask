using System;
using AutoService.Domain.Common;

namespace AutoService.Services.Economy
{
    /// <summary>
    /// Application-level facade over the domain Wallet. Game systems and presenters depend on this
    /// interface, never on the Wallet entity directly.
    /// </summary>
    public interface IWalletService
    {
        /// <summary>Current balance.</summary>
        Money Balance { get; }

        /// <summary>Raised after any balance change with the new balance.</summary>
        event Action<Money> BalanceChanged;

        /// <summary>Returns true if the balance covers <paramref name="cost"/>.</summary>
        bool CanAfford(Money cost);

        /// <summary>Spends <paramref name="cost"/> if affordable; returns false and changes nothing otherwise.</summary>
        bool TrySpend(Money cost);

        /// <summary>Adds income to the balance.</summary>
        void Add(Money amount);

        /// <summary>Sets the balance to a saved value (raises <see cref="BalanceChanged"/> if it changes). For the save module only.</summary>
        void Restore(Money balance);
    }
}
