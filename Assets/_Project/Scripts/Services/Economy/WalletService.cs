using System;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Services.Events;

namespace AutoService.Services.Economy
{
    /// <summary>
    /// Default <see cref="IWalletService"/>: delegates to the domain <see cref="Wallet"/>, re-raises its change event
    /// and publishes <see cref="BalanceChangedEvent"/> to the bus for cross-cutting listeners.
    /// </summary>
    public sealed class WalletService : IWalletService, IDisposable
    {
        private readonly Wallet _wallet;
        private readonly IEventBus _eventBus;
        private Money _lastBalance;
        private bool _disposed;

        /// <summary>Creates the service and starts listening to <paramref name="wallet"/>.</summary>
        public WalletService(Wallet wallet, IEventBus eventBus)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _lastBalance = _wallet.Balance;
            _wallet.BalanceChanged += OnWalletBalanceChanged;
        }

        /// <inheritdoc />
        public event Action<Money> BalanceChanged;

        /// <inheritdoc />
        public Money Balance => _wallet.Balance;

        /// <inheritdoc />
        public bool CanAfford(Money cost) => _wallet.CanAfford(cost);

        /// <inheritdoc />
        public bool TrySpend(Money cost) => _wallet.TrySpend(cost);

        /// <inheritdoc />
        public void Add(Money amount) => _wallet.Add(amount);

        /// <inheritdoc />
        public void Restore(Money balance) => _wallet.Restore(balance);

        /// <summary>Stops listening to the wallet.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _wallet.BalanceChanged -= OnWalletBalanceChanged;
        }

        private void OnWalletBalanceChanged(Money balance)
        {
            // Why: both balances are non-negative longs, so their difference always fits in a long.
            long delta = balance.Amount - _lastBalance.Amount;
            _lastBalance = balance;

            BalanceChanged?.Invoke(balance);
            _eventBus.Publish(new BalanceChangedEvent(balance, delta));
        }
    }
}
