using AutoService.Domain.Common;

namespace AutoService.Services.Economy
{
    /// <summary>
    /// Bus event published after every wallet balance change.
    /// </summary>
    public readonly struct BalanceChangedEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="balance">Balance after the change.</param>
        /// <param name="delta">Signed change: positive for income, negative for spending.</param>
        public BalanceChangedEvent(Money balance, long delta)
        {
            Balance = balance;
            Delta = delta;
        }

        /// <summary>Balance after the change.</summary>
        public Money Balance { get; }

        /// <summary>Signed change: positive for income, negative for spending (used by audio and onboarding).</summary>
        public long Delta { get; }
    }
}
