using AutoService.Presentation.Controls;
using AutoService.Presentation.Hud;
using AutoService.Presentation.Pause;
using AutoService.Presentation.Player;
using AutoService.Presentation.Popups;
using AutoService.Presentation.Settings;
using AutoService.Presentation.Ui;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Menu;
using AutoService.Services.Progression;
using AutoService.Services.Save;
using AutoService.Services.Scenes;
using AutoService.Services.Settings;
using UnityEngine;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>Screen HUD: the balance label, progression, pause, popups, and debug cheats.</summary>
    internal sealed class HudInstaller : IGameplayInstaller
    {
        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            InstallBalance(context);
            InstallProgression(context);
            InstallPause(context);
            InstallPopups(context);
            InstallDebugCheats(context);
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

        private static void InstallProgression(GameplayContext context)
        {
            ProgressionView progressionView = context.Scene.ProgressionView;
            if (progressionView == null)
            {
                context.Logger.Warning("[Gameplay] _progressionView is not assigned; the progression is not shown.");
                return;
            }

            if (!context.TryResolve(out AutoService.Services.Progression.IProgressionService progressionService))
            {
                context.Logger.Warning("[Gameplay] IProgressionService not found; progression is not shown.");
                return;
            }

            context.Register(new ProgressionPresenter(progressionService, progressionView));
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

            // Null when the save module did not run: the pause still works, it just does not save.
            context.TryResolve(out IGameSaver saver);
            var model = new PauseMenuModel(context.Resolve<IPauseService>(), context.Resolve<ISceneLoader>(), saver);
            context.Register(model);
            context.Register(new PauseMenuPresenter(model, scene.PauseMenu, scene.PauseButton, settings, escape));
        }

        private static void InstallPopups(GameplayContext context)
        {
            GameplaySceneRefs scene = context.Scene;
            context.TryResolve(out EscapeRouter escape);

            LevelUpPopupView levelUpView = scene.LevelUpPopup;
            if (levelUpView == null)
            {
                context.Logger.Warning("[Gameplay] _levelUpPopup is not assigned; no level-up popup.");
            }
            else if (context.TryResolve(out IProgressionService progression))
            {
                levelUpView.SetEscapeRouter(escape);
                context.Register(new LevelUpPresenter(progression, levelUpView, context.Resolve<IConfigProvider>().Buildables));
            }

            Location2WelcomePopupView welcomeView = scene.Loc2WelcomePopup;
            if (welcomeView == null)
            {
                context.Logger.Warning("[Gameplay] _loc2WelcomePopup is not assigned; no welcome popup for location 2.");
            }
            else if (scene.Player != null && scene.Locations.Length >= 2)
            {
                welcomeView.SetEscapeRouter(escape);
                context.Track(new Location2WelcomePresenter(
                    scene.Player, welcomeView, scene.Locations[0].transform.position, scene.Locations[1].transform.position));
            }
        }

        private static void InstallDebugCheats(GameplayContext context)
        {
            DebugCheatView cheatView = context.Scene.DebugCheatView;
            if (cheatView == null)
            {
                context.Logger.Warning("[Gameplay] _debugCheatView is not assigned; no debug panel.");
                return;
            }

            // Why: TryResolve throughout — the panel is a debug tool and must not block the scene when a module is off.
            context.TryResolve(out EscapeRouter escape);
            context.TryResolve(out IWalletService wallet);
            context.TryResolve(out IProgressionService progression);
            context.TryResolve(out PlayerTeleporter teleporter);
            context.TryResolve(out ISaveService saveService);
            context.TryResolve(out IGameSaver gameSaver);
            context.TryResolve(out ISceneLoader sceneLoader);
            context.TryResolve(out IPauseService pauseService);

            cheatView.Construct(wallet, progression, teleporter, saveService, gameSaver, sceneLoader, pauseService, escape);
            context.Track(cheatView);
        }
    }
}
