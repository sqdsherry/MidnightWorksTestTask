using System;
using AutoService.Domain.Points;
using AutoService.Presentation.Points;
using AutoService.Presentation.Traffic;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Traffic;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// Service points of location 1 (both entrances and the bays built from the start), its traffic with the car agents,
    /// and the point presenters. Ticks: point service (Simulation) → traffic (Traffic) → car agents (Agents) →
    /// point presenters (Presentation).
    /// </summary>
    internal sealed class ServiceLoopInstaller : IGameplayInstaller
    {
        /// <summary>The point registry, or null when location 1 cannot run.</summary>
        public ServicePointService Points { get; private set; }

        /// <summary>Registers scene points (also the ones built later), or null when location 1 cannot run.</summary>
        public PointRegistrar Registrar { get; private set; }

        /// <summary>Traffic of location 1, or null when it was skipped.</summary>
        public LocationTraffic Traffic { get; private set; }

        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            IConfigProvider config = context.Resolve<IConfigProvider>();
            IEventBus eventBus = context.Resolve<IEventBus>();
            if (InstallPoints(context, config, eventBus))
            {
                Traffic = InstallTraffic(context, config, eventBus);
            }

            // Why: tracked even when the traffic was skipped, so the presenters of the points registered so far are still
            // ticked and disposed; their phase makes them render the state produced by this frame's simulation.
            if (Registrar != null)
            {
                context.Track(Registrar, TickPhase.Presentation);
            }
        }

        /// <returns>False when the location cannot run (traffic is skipped then).</returns>
        private bool InstallPoints(GameplayContext context, IConfigProvider config, IEventBus eventBus)
        {
            LocationLayout layout = context.Scene.Location1;
            if (!context.HasReference(layout, "_location1"))
            {
                context.Logger.Error("[Gameplay] Service loop skipped: assign the location layout on " + context.OwnerName + ".");
                return false;
            }

            if (!layout.Validate(out string problem) || !layout.ValidateBuildPlots(config, out problem))
            {
                context.Logger.Error("[Gameplay] Service loop skipped: LocationLayout '" + layout.name + "': " + problem + ".");
                return false;
            }

            var points = new ServicePointService(context.Resolve<IWalletService>(), eventBus);
            context.Register<IServicePointService>(points, TickPhase.Simulation);
            var registrar = new PointRegistrar(points, config, context.Logger);
            Points = points;
            Registrar = registrar;

            // Why: every parking visit pays at one of the two entrances, so both must be Barrier-kind points; without them
            // the traffic cannot run. Non-short-circuit `|` reports both.
            if (!registrar.TryRegister(layout.MainEntrance, layout.LocationId, PointKind.Barrier, out _)
                | !registrar.TryRegister(layout.ServiceEntrance, layout.LocationId, PointKind.Barrier, out _))
            {
                context.Logger.Error("[Gameplay] Traffic skipped: location '" + layout.LocationId + "' needs two valid parking entrances.");
                return false;
            }

            // Why: points that are still build plots join later, when built (BuildableBinder).
            ServicePointView[] servicePoints = layout.ServicePoints;
            for (int i = 0; i < servicePoints.Length; i++)
            {
                if (layout.IsBuiltAtStart(servicePoints[i]))
                {
                    registrar.TryRegister(servicePoints[i], layout.LocationId, PointKind.Service, out _);
                }
            }

            return true;
        }

        /// <returns>The traffic, or null when it was skipped.</returns>
        private static LocationTraffic InstallTraffic(GameplayContext context, IConfigProvider config, IEventBus eventBus)
        {
            GameplaySceneRefs scene = context.Scene;
            LocationLayout layout = scene.Location1;
            if (!context.HasReference(scene.CarVisuals, "_carVisuals"))
            {
                context.Logger.Error("[Gameplay] Traffic skipped: assign the car visual catalog on " + context.OwnerName + ".");
                return null;
            }

            if (config.CarTypes.Count == 0)
            {
                context.Logger.Warning("[Gameplay] GameConfig has no car types; no cars will spawn.");
            }

            var definition = new LocationTrafficDefinition(
                layout.LocationId,
                layout.MainEntrance.PointId,
                layout.ServiceEntrance.PointId,
                layout.QueueSlotCount,
                layout.InitialParkingCapacity(config),
                layout.ServiceBufferCapacity);
            var agents = new CarAgents(layout, scene.CarVisuals, scene.CarPoolRoot);

            LocationTraffic traffic;
            try
            {
                traffic = new LocationTraffic(
                    definition, context.Resolve<IServicePointService>(), agents, config, context.Resolve<IRandom>(), eventBus);
            }
            catch (ArgumentException exception)
            {
                agents.Dispose();
                context.Logger.Error("[Gameplay] Traffic skipped: " + exception.Message);
                return null;
            }

            // Debug only: Scene view labels "#id plan state" above the cars.
            agents.SetDebugTraffic(traffic);

            // Why: tracked, not registered — module 11 adds a second location with its own traffic and agents.
            context.Track(traffic, TickPhase.Traffic);
            context.Track(agents, TickPhase.Agents);
            return traffic;
        }
    }
}
