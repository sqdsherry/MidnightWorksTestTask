using System;
using AutoService.Domain.Common;
using AutoService.Services.Economy;
using AutoService.Services.Formatting;

namespace AutoService.Presentation.Hud
{
    /// <summary>
    /// Writes the wallet balance into a <see cref="BalanceView"/>. Event-driven: the text is formatted only when the balance
    /// changes (formatting allocates a string), never per frame.
    /// </summary>
    public sealed class BalancePresenter : IDisposable
    {
        private readonly IWalletService _wallet;
        private readonly BalanceView _view;
        private bool _disposed;

        /// <summary>Shows the current balance and starts listening for changes.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public BalancePresenter(IWalletService wallet, BalanceView view)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));

            _wallet.BalanceChanged += OnBalanceChanged;
            OnBalanceChanged(_wallet.Balance);
        }

        /// <summary>Stops listening to the wallet. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _wallet.BalanceChanged -= OnBalanceChanged;
        }

        private void OnBalanceChanged(Money balance)
        {
            // Why: the view may already be destroyed while the scene unloads, before this presenter is disposed.
            if (_view != null)
            {
                _view.SetText(MoneyFormatter.Format(balance));
            }
        }
    }
}
