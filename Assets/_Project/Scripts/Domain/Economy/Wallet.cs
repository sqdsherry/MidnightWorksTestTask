using System;
using AutoService.Domain.Common;

namespace AutoService.Domain.Economy
{
    /// <summary>
    /// The player's single currency balance. Pure domain entity: owns the balance invariant
    /// (never negative) and notifies listeners about every actual change.
    /// </summary>
    public sealed class Wallet
    {
        /// <summary>Creates a wallet with the given starting balance.</summary>
        /// <param name="initialBalance">Balance the wallet starts with.</param>
        public Wallet(Money initialBalance)
        {
            Balance = initialBalance;
        }

        /// <summary>Raised after any balance change with the new balance. Not raised for no-op operations.</summary>
        public event Action<Money> BalanceChanged;

        /// <summary>Current balance.</summary>
        public Money Balance { get; private set; }

        /// <summary>Returns true if the wallet holds at least <paramref name="cost"/>.</summary>
        public bool CanAfford(Money cost) => Balance >= cost;

        /// <summary>
        /// Spends <paramref name="cost"/> if affordable.
        /// </summary>
        /// <returns>
        /// True if the money was spent (or <paramref name="cost"/> is zero); false if the balance is insufficient,
        /// in which case the balance is left untouched and no event is raised.
        /// </returns>
        public bool TrySpend(Money cost)
        {
            if (!CanAfford(cost))
            {
                return false;
            }

            if (cost == Money.Zero)
            {
                // Why: a free purchase always succeeds but changes nothing, so listeners are not bothered.
                return true;
            }

            Balance -= cost;
            BalanceChanged?.Invoke(Balance);
            return true;
        }

        /// <summary>Adds income to the balance (saturating at <see cref="Money.MaxValue"/>). Zero is a no-op.</summary>
        public void Add(Money amount)
        {
            if (amount == Money.Zero)
            {
                return;
            }

            Money previous = Balance;
            Balance += amount;

            // Why: when the balance is already saturated the addition changes nothing — do not report a fake change.
            if (Balance != previous)
            {
                BalanceChanged?.Invoke(Balance);
            }
        }

        /// <summary>
        /// Sets the balance to a saved value and raises <see cref="BalanceChanged"/> if it differs from the current one.
        /// </summary>
        /// <remarks>Why it exists: only the save module may set the balance directly; gameplay goes through
        /// <see cref="TrySpend"/> and <see cref="Add"/>.</remarks>
        /// <param name="balance">The saved balance.</param>
        public void Restore(Money balance)
        {
            if (balance == Balance)
            {
                return;
            }

            Balance = balance;
            BalanceChanged?.Invoke(Balance);
        }
    }
}
