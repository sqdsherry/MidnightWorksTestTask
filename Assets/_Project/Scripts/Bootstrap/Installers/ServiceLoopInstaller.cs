using System;
using System.Collections.Generic;
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
    /// Service points of all locations (both entrances and the bays built from the start), their traffic with the car agents,
    /// and the point presenters. Ticks: point service (Simulation) → traffic (Traffic) → car agents (Agents) →
    /// point presenters (Presentation).
    /// </summary>
    internal sealed class ServiceLoopInstaller : IGameplayInstaller
    {
        private readonly Dictionary<string, LocationTraffic> _traffics = new Dictionary<string, LocationTraffic>(StringComparer.Ordinal);
        private readonly Dictionary<string, CarAgents> _carAgents = new Dictionary<string, CarAgents>(StringComparer.Ordinal);

        /// <summary>The point registry, or null when locations cannot run.</summary>
        public ServicePointService Points { get; private set; }

        /// <summary>Registers scene points (also the ones built later), or null when locations cannot run.</summary>
        public PointRegistrar Registrar { get; private set; }

        /// <summary>Traffic of the primary location, or null when it was skipped.</summary>
        public LocationTraffic Traffic { get; private set; }

        /// <summary>Traffic instances keyed by location id.</summary>
        public IReadOnlyDictionary<string, LocationTraffic> Traffics => _traffics;

        /// <summary>Car agent instances keyed by location id.</summary>
        public IReadOnlyDictionary<string, CarAgents> Agents => _carAgents;

        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            IConfigProvider config = context.Resolve<IConfigProvider>();
            IEventBus eventBus = context.Resolve<IEventBus>();
            LocationLayout[] locations = context.Scene.Locations;

            if (locations == null || locations.Length == 0)
            {
                context.Logger.Error("[Gameplay] Service loop skipped: no locations found on " + context.OwnerName + ".");
                return;
            }

            var points = new ServicePointService(context.Resolve<IWalletService>(), eventBus);
            context.Register<IServicePointService>(points, TickPhase.Simulation);
            var registrar = new PointRegistrar(points, config, context.Logger);
            Points = points;
            Registrar = registrar;

            if (!context.HasReference(context.Scene.CarVisuals, "_carVisuals"))
            {
                context.Logger.Error("[Gameplay] Traffic skipped: assign the car visual catalog on " + context.OwnerName + ".");
            }

            if (config.CarTypes.Count == 0)
            {
                context.Logger.Warning("[Gameplay] GameConfig has no car types; no cars will spawn.");
            }

            for (int i = 0; i < locations.Length; i++)
            {
                LocationLayout layout = locations[i];
                if (layout == null)
                {
                    continue;
                }

                if (!layout.Validate(out string problem) || !layout.ValidateBuildPlots(config, out problem) || !layout.ValidateStaff(config, out problem))
                {
                    context.Logger.Error("[Gameplay] Service loop skipped for '" + layout.name + "': " + problem + ".");
                    continue;
                }

                bool entrancesValid = true;
                if (layout.MainEntrance != null || layout.ServiceEntrance != null)
                {
                    entrancesValid = registrar.TryRegister(layout.MainEntrance, layout.LocationId, PointKind.Barrier, out _)
                        & registrar.TryRegister(layout.ServiceEntrance, layout.LocationId, PointKind.Barrier, out _);
                }

                if (!entrancesValid)
                {
                    context.Logger.Error("[Gameplay] Traffic skipped: location '" + layout.LocationId + "' needs two valid parking entrances.");
                    continue;
                }

                ServicePointView[] servicePoints = layout.ServicePoints;
                for (int s = 0; s < servicePoints.Length; s++)
                {
                    if (layout.IsBuiltAtStart(servicePoints[s]))
                    {
                        registrar.TryRegister(servicePoints[s], layout.LocationId, PointKind.Service, out _);
                    }
                }

                if (context.Scene.CarVisuals == null)
                {
                    continue;
                }

                var definition = new LocationTrafficDefinition(
                    layout.LocationId,
                    layout.MainEntrance?.PointId,
                    layout.ServiceEntrance?.PointId,
                    layout.QueueSlotCount,
                    layout.InitialParkingCapacity(config),
                    layout.ServiceBufferCapacity);
                var agents = new CarAgents(layout, context.Scene.CarVisuals, context.Scene.CarPoolRoot);

                LocationTraffic traffic;
                try
                {
                    traffic = new LocationTraffic(
                        definition, points, agents, config, context.Resolve<IRandom>(), eventBus);
                }
                catch (ArgumentException exception)
                {
                    agents.Dispose();
                    context.Logger.Error("[Gameplay] Traffic skipped for '" + layout.LocationId + "': " + exception.Message);
                    continue;
                }

                agents.SetDebugTraffic(traffic);

                context.Track(traffic, TickPhase.Traffic);
                context.Track(agents, TickPhase.Agents);

                _traffics[layout.LocationId] = traffic;
                _carAgents[layout.LocationId] = agents;

                if (Traffic == null)
                {
                    Traffic = traffic;
                }
            }

            if (Registrar != null)
            {
                context.Track(Registrar, TickPhase.Presentation);
            }

            if (context.Scene.CarVisuals != null && _carAgents.Count > 0)
            {
                var customizationPresenter = new CarCustomizationPresenter(eventBus, context.Scene.CarVisuals, _carAgents.Values);
                context.Track(customizationPresenter);
            }
        }
    }
}
