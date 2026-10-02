using AutoService.Presentation.Controls;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Pause;
using AutoService.Presentation.Settings;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Menu;
using AutoService.Services.Scenes;
using AutoService.Services.Settings;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>Screen HUD: the balance label and the pause (button, menu, settings screen).</summary>
    internal sealed class HudInstaller : IGameplayInstaller
    {
        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            InstallBalance(context);
            InstallPause(context);
        }

        private static void InstallBalance(GameplayContext context)
        {
            BalanceView balanceView = context.Scene.BalanceView;
            if (balanceView == null)
            {
                context.Logger.Warning("[Gameplay] _balanceView is not assigned; the balance is not shown.");
                return;
            }

            context.Register(new BalancePresenter(context.Resolve<IWalletService>(), balanceView));
        }

        private static void InstallPause(GameplayContext context)
        {
            GameplaySceneRefs scene = context.Scene;
            if (scene.PauseMenu == null)
            {
                context.Logger.Warning("[Gameplay] _pauseMenu is not assigned on " + context.OwnerName + "; the game cannot be paused.");
                return;
            }

            if (scene.PauseButton == null)
            {
                context.Logger.Warning("[Gameplay] _pauseButton is not assigned; the pause opens with Esc only.");
            }

            // Why: settings first — the container disposes in reverse order, so the pause presenter (which closes the
            // settings) goes before them.
            SettingsPresenter settings = null;
            if (scene.SettingsPanel != null)
            {
                settings = new SettingsPresenter(context.Resolve<ISettingsService>(), scene.SettingsPanel);
                context.Register(settings);
            }
            else
            {
                context.Logger.Warning("[Gameplay] _settingsPanel is not assigned; Settings in the pause menu does nothing.");
            }

            // The router exists only when the player module (input) is running; without it the pause is button-only.
            context.TryResolve(out EscapeRouter escape);

            var model = new PauseMenuModel(context.Resolve<IPauseService>(), context.Resolve<ISceneLoader>());
            context.Register(model);
            context.Register(new PauseMenuPresenter(model, scene.PauseMenu, scene.PauseButton, settings, escape));
        }
    }
}
