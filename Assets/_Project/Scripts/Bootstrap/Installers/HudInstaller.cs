using AutoService.Presentation.Hud;
using AutoService.Services.Economy;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>Screen HUD: the balance label (until the full HUD of module 09).</summary>
    internal sealed class HudInstaller : IGameplayInstaller
    {
        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            BalanceView balanceView = context.Scene.BalanceView;
            if (balanceView == null)
            {
                context.Logger.Warning("[Gameplay] _balanceView is not assigned; the balance is not shown.");
                return;
            }

            context.Register(new BalancePresenter(context.Resolve<IWalletService>(), balanceView));
        }
    }
}
