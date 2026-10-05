using System;
using System.Collections.Generic;
using AutoService.Presentation.Building;
using AutoService.Presentation.Traffic;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Traffic;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// Build plots of all locations: the unlock gate, the build service, the binder that turns
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
            if (_serviceLoop.Points == null)
            {
                context.Logger.Warning("[Gameplay] Building skipped: the service loop is not running.");
                return;
            }

            IConfigProvider config = context.Resolve<IConfigProvider>();
            IWalletService wallet = context.Resolve<IWalletService>();
            GameplaySceneRefs scene = context.Scene;
            LocationLayout[] locations = scene.Locations;

            var gate = new AutoService.Services.Progression.LevelUnlockGate(context.Resolve<AutoService.Services.Progression.IProgressionService>());
            var build = new BuildService(wallet, gate, context.Resolve<IEventBus>());
            context.Register<IUnlockGate>(gate);
            context.Track(gate);
            context.Register<IBuildService>(build);
            Gate = gate;

            var allPlots = new List<BuildPlotView>();

            for (int i = 0; i < locations.Length; i++)
            {
                LocationLayout layout = locations[i];
                if (layout == null)
                {
                    continue;
                }

                BuildPlotView[] plots = layout.BuildPlots;
                for (int p = 0; p < plots.Length; p++)
                {
                    BuildPlotView plot = plots[p];
                    if (plot == null)
                    {
                        continue;
                    }

                    allPlots.Add(plot);
                    if (config.TryGetBuildable(plot.PlotId, out BuildableSettings settings))
                    {
                        build.Register(settings.PlotDefinition);
                    }
                }

                _serviceLoop.Traffics.TryGetValue(layout.LocationId, out LocationTraffic traffic);
                context.Track(new BuildableBinder(build, config, _serviceLoop.Registrar, traffic, layout, context.Logger, context.Resolve<AutoService.Services.Progression.IProgressionService>(), wallet));
            }

            if (scene.BuildPanel == null || scene.Camera == null)
            {
                context.Logger.Warning("[Gameplay] _buildPanel or _camera is not assigned; build plots cannot be bought.");
                return;
            }

            context.Register(new BuildPanelPresenter(build, config, wallet, scene.BuildPanel, scene.Camera, allPlots.ToArray(), _player.Escape));
        }
    }
}
