using System;
using AutoService.Presentation.Building;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// Build plots of location 1: the unlock gate (A1: everything unlocked), the build service, the binder that turns
    /// built plots into points/slots, and the build panel.
    /// </summary>
    internal sealed class BuildingInstaller : IGameplayInstaller
    {
        private readonly ServiceLoopInstaller _serviceLoop;
        private readonly PlayerInstaller _player;

        /// <summary>Creates the installer on top of the service loop and the player modules.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public BuildingInstaller(ServiceLoopInstaller serviceLoop, PlayerInstaller player)
        {
            _serviceLoop = serviceLoop ?? throw new ArgumentNullException(nameof(serviceLoop));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <summary>The unlock gate shared by every level-gated purchase, or null when building was skipped.</summary>
        public IUnlockGate Gate { get; private set; }

        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            if (_serviceLoop.Points == null || _serviceLoop.Traffic == null)
            {
                context.Logger.Warning("[Gameplay] Building skipped: the service loop of location 1 is not running.");
                return;
            }

            IConfigProvider config = context.Resolve<IConfigProvider>();
            IWalletService wallet = context.Resolve<IWalletService>();
            GameplaySceneRefs scene = context.Scene;

            // TODO(07-progression): replace with the level-based gate.
            var gate = new AlwaysUnlockedGate();
            var build = new BuildService(wallet, gate, context.Resolve<IEventBus>());
            context.Register<IUnlockGate>(gate);
            context.Register<IBuildService>(build);
            Gate = gate;

            // Plots were validated against the config together with the layout (ValidateBuildPlots).
            BuildPlotView[] plots = scene.Location1.BuildPlots;
            for (int i = 0; i < plots.Length; i++)
            {
                if (config.TryGetBuildable(plots[i].PlotId, out BuildableSettings settings))
                {
                    build.Register(settings.PlotDefinition);
                }
            }

            // TODO(08b-save): build.RestoreBuilt(saved ids) — before or after the binder, it handles both.
            context.Track(new BuildableBinder(build, config, _serviceLoop.Registrar, _serviceLoop.Traffic, scene.Location1, context.Logger));

            if (scene.BuildPanel == null || scene.Camera == null)
            {
                context.Logger.Warning("[Gameplay] _buildPanel or _camera is not assigned; build plots cannot be bought.");
                return;
            }

            context.Register(new BuildPanelPresenter(build, config, wallet, scene.BuildPanel, scene.Camera, plots, _player.Input));
        }
    }
}
