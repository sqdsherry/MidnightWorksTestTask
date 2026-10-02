using AutoService.Domain.Economy;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>Wallet of the session: <see cref="IWalletService"/> over a domain <see cref="Wallet"/>.</summary>
    internal sealed class EconomyInstaller : IGameplayInstaller
    {
        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            IConfigProvider config = context.Resolve<IConfigProvider>();

            // TODO(08b-save): start from the saved balance when a save exists.
            var wallet = new Wallet(config.Economy.StartingMoney);
            context.Register<IWalletService>(new WalletService(wallet, context.Resolve<IEventBus>()));
        }
    }
}
