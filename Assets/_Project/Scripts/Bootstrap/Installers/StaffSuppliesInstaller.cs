using System;
using System.Collections.Generic;
using AutoService.Presentation.Characters;
using AutoService.Presentation.Player;
using AutoService.Presentation.Points;
using AutoService.Presentation.Points.Panel;
using AutoService.Presentation.Staff;
using AutoService.Presentation.Supplies;
using AutoService.Presentation.Traffic;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Staff;
using AutoService.Services.Supplies;
using AutoService.Services.Upgrades;
using UnityEngine;

namespace AutoService.Bootstrap.Installers
{
    /// <summary>
    /// Module A2 of all locations: the player's hands, supplies and the warehouse, point upgrades, income per minute,
    /// staff (NPC bodies + hiring logic), the point views' box hand-over, the point panel and the storekeeper offer.
    /// Ticks: staff service (Staff) → staff agents (Agents, after the cars) → income tracker (Trackers) → presenters.
    /// </summary>
    internal sealed class StaffSuppliesInstaller : IGameplayInstaller
    {
        private readonly ServiceLoopInstaller _serviceLoop;
        private readonly BuildingInstaller _building;
        private readonly PlayerInstaller _player;

        /// <summary>Creates the installer on top of the service loop, building and player modules.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public StaffSuppliesInstaller(ServiceLoopInstaller serviceLoop, BuildingInstaller building, PlayerInstaller player)
        {
            _serviceLoop = serviceLoop ?? throw new ArgumentNullException(nameof(serviceLoop));
            _building = building ?? throw new ArgumentNullException(nameof(building));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        /// <inheritdoc />
        public void Install(GameplayContext context)
        {
            IUnlockGate gate = _building.Gate;
            if (_serviceLoop.Points == null || _serviceLoop.Registrar == null || gate == null)
            {
                context.Logger.Warning("[Gameplay] Staff & supplies skipped: the service loop is not running.");
                return;
            }

            IConfigProvider config = context.Resolve<IConfigProvider>();
            IEventBus eventBus = context.Resolve<IEventBus>();
            IWalletService wallet = context.Resolve<IWalletService>();
            IServicePointService points = _serviceLoop.Points;
            GameplaySceneRefs scene = context.Scene;
            LocationLayout[] locations = scene.Locations;

            if (locations == null || locations.Length == 0)
            {
                context.Logger.Warning("[Gameplay] Staff & supplies skipped: no locations found on scene.");
                return;
            }

            var carry = new PlayerCarry();
            var supplies = new SupplyService(points, wallet, config, eventBus);
            var upgrades = new UpgradeService(points, wallet, gate, config, eventBus);
            var income = new PointIncomeTracker(points, eventBus);
            context.Register<IPlayerCarry>(carry);
            context.Register<ISupplyService>(supplies);
            context.Register<IUpgradeService>(upgrades);
            context.Register(income, TickPhase.Trackers);

            if (scene.StaffPrefab == null)
            {
                context.Logger.Warning("[Gameplay] _staffPrefab is not assigned on " + context.OwnerName
                    + ": hired NPCs will be invisible (their jobs still run).");
            }

            var staff = new StaffService(points, supplies, wallet, gate, null, config, eventBus, context.Logger);
            context.Register<IStaffService>(staff, TickPhase.Staff);

            if (scene.PlayerCarry != null)
            {
                scene.PlayerCarry.Construct(carry, scene.SupplyVisuals);
            }
            else
            {
                context.Logger.Warning("[Gameplay] _playerCarry is not assigned; the carried box is not shown.");
            }

            var animator = scene.Player != null ? scene.Player.GetComponentInChildren<Animator>() : null;
            if (animator != null && scene.Player != null && scene.Player.Agent != null)
            {
                var charAnim = new CharacterAnimator(animator, scene.Player.Agent);
                context.Register(new PlayerAnimatorPresenter(charAnim, carry), TickPhase.Presentation);
            }

            for (int i = 0; i < locations.Length; i++)
            {
                LocationLayout layout = locations[i];
                if (layout == null)
                {
                    continue;
                }

                var agents = new StaffAgents(layout, scene.StaffPrefab, scene.StaffVisuals, scene.SupplyVisuals, scene.StaffRoot);
                context.Track(agents, TickPhase.Agents);
                staff.RegisterAgents(layout.LocationId, agents);

                context.Track(new PointStaffSuppliesBinder(_serviceLoop.Registrar, layout, supplies, carry, staff));

                if (layout.Warehouse != null)
                {
                    layout.Warehouse.Construct(supplies, carry);
                    context.Track(layout.Warehouse);
                }
            }

            InstallPanels(context, points, upgrades, staff, wallet, gate, config, income, locations);
        }

        private void InstallPanels(
            GameplayContext context,
            IServicePointService points,
            IUpgradeService upgrades,
            IStaffService staff,
            IWalletService wallet,
            IUnlockGate gate,
            IConfigProvider config,
            PointIncomeTracker income,
            LocationLayout[] locations)
        {
            GameplaySceneRefs scene = context.Scene;
            if (scene.Camera == null)
            {
                context.Logger.Warning("[Gameplay] _camera is not assigned; the point and storekeeper panels cannot open.");
                return;
            }

            var allManagePads = new List<ManagePadView>();
            for (int i = 0; i < locations.Length; i++)
            {
                LocationLayout layout = locations[i];
                if (layout != null && layout.ManagePads != null)
                {
                    allManagePads.AddRange(layout.ManagePads);
                }
            }

            if (context.HasReference(scene.PointPanel, "_pointPanel"))
            {
                context.Register(new PointPanelPresenter(
                    points, upgrades, staff, wallet, gate, config, income, scene.PointPanel, scene.Camera, allManagePads, _player.Escape));
            }

            if (context.HasReference(scene.StorekeeperPanel, "_storekeeperPanel"))
            {
                bool registeredFirst = false;
                for (int i = 0; i < locations.Length; i++)
                {
                    LocationLayout layout = locations[i];
                    if (layout != null && layout.WarehousePad != null)
                    {
                        var presenter = new StorekeeperOfferPresenter(
                            staff, wallet, gate, config, scene.StorekeeperPanel, scene.Camera, layout.WarehousePad, _player.Escape);
                        if (!registeredFirst)
                        {
                            context.Register(presenter);
                            registeredFirst = true;
                        }
                        else
                        {
                            context.Track(presenter);
                        }
                    }
                }
            }
        }
    }
}
