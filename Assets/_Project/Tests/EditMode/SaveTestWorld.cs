using System;
using AutoService.Domain.Building;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Domain.Points;
using AutoService.Domain.Upgrades;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Save;
using AutoService.Services.Staff;
using AutoService.Services.Supplies;
using AutoService.Services.Upgrades;

namespace AutoService.Tests.EditMode
{
    /// <summary>
    /// A small game world on the real services (wallet, points, build, upgrades, supplies, staff with fake bodies) for
    /// the save tests: one wash bay from the start and a second one on a build plot. Building the plot registers the bay,
    /// as <c>BuildableBinder</c> does in the scene.
    /// </summary>
    public sealed class SaveTestWorld : IDisposable
    {
        /// <summary>Location of every point.</summary>
        public const string LocationId = "loc1";

        /// <summary>Wash bay that exists from the start.</summary>
        public const string Wash1 = "loc1_wash_1";

        /// <summary>Wash bay that appears when <see cref="Plot2"/> is built.</summary>
        public const string Wash2 = "loc1_wash_2";

        /// <summary>Build plot of <see cref="Wash2"/>.</summary>
        public const string Plot2 = "plot_wash_2";

        /// <summary>Price of <see cref="Plot2"/>.</summary>
        public const long PlotCost = 500;

        /// <summary>Supply capacity of a wash bay.</summary>
        public const int SupplyCapacity = 10;

        private readonly ServiceTypeSettings _washType;

        /// <summary>Builds the world with <paramref name="startingMoney"/> in the wallet.</summary>
        public SaveTestWorld(long startingMoney = 100000)
        {
            Logger = new FakeGameLogger();
            Bus = new EventBus(new FakeGameLogger());
            Wallet = new WalletService(new Wallet(new Money(startingMoney)), Bus);
            Points = new ServicePointService(Wallet, Bus);
            Agents = new FakeStaffAgents();
            var gate = new FakeUnlockGate();

            Config = new FakeConfigProvider();
            Config.SupplyTypeList.Add(new SupplyTypeSettings("shampoo", "Shampoo", new Money(5), 5));
            Config.UpgradeList.Add(new UpgradeSettings(UpgradeKind.Speed, "Speed", "-10%", new Money(100), 1.35, 3, 0.10, 0));
            Config.UpgradeList.Add(new UpgradeSettings(UpgradeKind.Price, "Price", "+15%", new Money(100), 1.35, 3, 0.15, 0));
            _washType = new ServiceTypeSettings("wash", "Wash", PointKind.Service, new Money(12), 0.0, 1f, 0f, 0f, "shampoo", SupplyCapacity,
                new PointWorkerSettings("Washer", new Money(300), 0));
            Config.ServiceTypeList.Add(_washType);

            Points.Register(_washType.CreatePointDefinition(Wash1, LocationId));
            Build = new BuildService(Wallet, gate, Bus);
            Build.Register(new BuildPlotDefinition(Plot2, BuildableKind.ServicePoint, Wash2, new Money(PlotCost), 0, 0.0));
            Build.Built += OnBuilt;
            Build.BuiltRestored += OnBuilt;

            Upgrades = new UpgradeService(Points, Wallet, gate, Config, Bus);
            Supplies = new SupplyService(Points, Wallet, Config, Bus);
            Staff = new StaffService(Points, Supplies, Wallet, gate, Agents, Config, Bus, Logger);
        }

        /// <summary>Collects warnings of the saveables.</summary>
        public FakeGameLogger Logger { get; }

        /// <summary>Event bus.</summary>
        public EventBus Bus { get; }

        /// <summary>Wallet.</summary>
        public WalletService Wallet { get; }

        /// <summary>Point registry.</summary>
        public ServicePointService Points { get; }

        /// <summary>Build plots.</summary>
        public BuildService Build { get; }

        /// <summary>Upgrades.</summary>
        public UpgradeService Upgrades { get; }

        /// <summary>Supplies.</summary>
        public SupplyService Supplies { get; }

        /// <summary>Staff.</summary>
        public StaffService Staff { get; }

        /// <summary>Fake staff bodies.</summary>
        public FakeStaffAgents Agents { get; }

        /// <summary>Config of the world.</summary>
        public FakeConfigProvider Config { get; }

        /// <summary>The point <paramref name="pointId"/> (must exist).</summary>
        public ServicePoint Point(string pointId)
        {
            Points.TryGet(pointId, out ServicePoint point);
            return point;
        }

        /// <summary>A coordinator over this world's saveables, in the game's order (see <c>SaveInstaller</c>).</summary>
        public SaveCoordinator CreateCoordinator(ISaveService saveService)
        {
            var coordinator = new SaveCoordinator(saveService, Logger);
            coordinator.Add(new WalletSaveable(Wallet, Logger));
            coordinator.Add(new BuildSaveable(Build, Logger));
            coordinator.Add(new PointsSaveable(Points, Upgrades, Staff, Logger));
            coordinator.Add(new StaffSaveable(Staff, Points, Logger));
            return coordinator;
        }

        /// <summary>Disposes the services in reverse order.</summary>
        public void Dispose()
        {
            Build.Built -= OnBuilt;
            Build.BuiltRestored -= OnBuilt;
            Staff.Dispose();
            Supplies.Dispose();
            Upgrades.Dispose();
            Points.Dispose();
            Wallet.Dispose();
        }

        private void OnBuilt(BuildPlot plot)
        {
            Points.Register(_washType.CreatePointDefinition(plot.Definition.TargetId, LocationId));
        }
    }
}
