using AutoService.Services.Building;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Save;
using AutoService.Services.Staff;
using AutoService.Services.Upgrades;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// Save of the session: the <see cref="SaveCoordinator"/> (autosave every 30 s of game time), one saveable per module,
    /// the triggers that save after important actions, and restoring the saved game.
    /// </summary>
    /// <remarks>
    /// Runs after every installer that builds the world (so restoring sees every service and view) and before the HUD
    /// (the pause menu needs <see cref="IGameSaver"/>); the entry point calls Initialize and starts ticking only after
    /// all installers, so the first frame already shows the restored game. Without a save nothing is restored: a new game.
    /// </remarks>
    internal sealed class SaveInstaller : IGameplayInstaller
    {
        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            IGameLogger logger = context.Logger;
            var coordinator = new SaveCoordinator(context.Resolve<ISaveService>(), logger, SaveCoordinator.DefaultAutosaveIntervalSeconds);

            // Why: this order is the restore order —
            // Build before Points: restoring built plots registers their bays synchronously (BuiltRestored), and only then
            //   do the upgrade and supply services know those points;
            // Points before Staff: workers are restored per point, storekeepers need the location's points.
            coordinator.Add(new WalletSaveable(context.Resolve<IWalletService>(), logger));

            // TODO(07-progression): coordinator.Add(new ProgressionSaveable(progression)) — after Wallet, before Build (level gates may matter on restore).
            if (context.TryResolve(out IBuildService build))
            {
                coordinator.Add(new BuildSaveable(build, logger));
            }

            bool hasPoints = context.TryResolve(out IServicePointService points);
            bool hasStaff = context.TryResolve(out IStaffService staff);
            if (hasPoints && hasStaff && context.TryResolve(out IUpgradeService upgrades))
            {
                coordinator.Add(new PointsSaveable(points, upgrades, staff, logger));
                coordinator.Add(new StaffSaveable(staff, points, logger));
            }
            else
            {
                context.Logger.Warning("[Gameplay] Points and staff are not saved: their modules are not running.");
            }

            coordinator.TryRestore();

            // Why: registered as itself (the entry point saves on quit) and as IGameSaver (the pause menu); the container
            // disposes the one instance once, the lifecycle ticks it once — after the frame's changes.
            context.Register(coordinator, TickPhase.Presentation);
            context.Register<IGameSaver>(coordinator);
            context.Register(new SaveTriggers(context.Resolve<IEventBus>(), coordinator));
        }
    }
}
