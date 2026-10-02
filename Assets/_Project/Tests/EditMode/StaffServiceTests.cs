using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Domain.Points;
using AutoService.Domain.Staff;
using AutoService.Domain.Supplies;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Staff;
using AutoService.Services.Supplies;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="StaffService"/> with real points, supplies, wallet and bus and fake staff bodies.</summary>
    public sealed class StaffServiceTests
    {
        private const string LocationId = "loc1";
        private const string Wash1 = "loc1_wash_1";
        private const string Wash2 = "loc1_wash_2";
        private const string Oil = "loc1_oil";
        private const long WasherCost = 300;
        private const int WasherLevel = 2;
        private const long StorekeeperCost = 600;
        private const long BoxPrice = 5;

        private EventBus _bus;
        private WalletService _wallet;
        private ServicePointService _points;
        private FakeConfigProvider _config;
        private FakeUnlockGate _gate;
        private FakeStaffAgents _agents;
        private SupplyService _supplies;
        private StaffService _service;
        private FakeGameLogger _logger;

        private readonly List<StaffHiredEvent> _hired = new List<StaffHiredEvent>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(new FakeGameLogger());
            _wallet = new WalletService(new Wallet(new Money(2000)), _bus);
            _points = new ServicePointService(_wallet, _bus);
            _gate = new FakeUnlockGate();
            _agents = new FakeStaffAgents();
            _config = new FakeConfigProvider
            {
                Staff = new StaffSettings("Storekeeper", "Carries boxes", new Money(StorekeeperCost), 3, 0.5f),
            };
            _config.SupplyTypeList.Add(new SupplyTypeSettings("shampoo", "Shampoo", new Money(BoxPrice), 5));
            var wash = new ServiceTypeSettings("wash", "Wash", PointKind.Service, new Money(12), 0.0, 1f, 0f, 0f, "shampoo", 10,
                new PointWorkerSettings("Washer", new Money(WasherCost), WasherLevel));
            var oil = new ServiceTypeSettings("oil", "Oil", PointKind.Service, new Money(25), 0.0, 1f, 0f, 0f);
            _config.ServiceTypeList.Add(wash);
            _config.ServiceTypeList.Add(oil);

            _points.Register(wash.CreatePointDefinition(Wash1, LocationId));
            _points.Register(wash.CreatePointDefinition(Wash2, LocationId));
            _points.Register(oil.CreatePointDefinition(Oil, LocationId));
            _supplies = new SupplyService(_points, _wallet, _config, _bus);
            _logger = new FakeGameLogger();
            _service = new StaffService(_points, _supplies, _wallet, _gate, _agents, _config, _bus, _logger);

            _hired.Clear();
            _bus.Subscribe<StaffHiredEvent>(OnHired);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Unsubscribe<StaffHiredEvent>(OnHired);
            _service.Dispose();
            _supplies.Dispose();
            _points.Dispose();
            _wallet.Dispose();
        }

        // ── Point worker ──────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void HireWorker_SpawnsAndWalksToTheSpot_ThenTakesIt()
        {
            Assert.AreEqual(HireAvailability.Available, _service.GetWorkerAvailability(Wash1));
            Assert.IsTrue(_service.TryHireWorker(Wash1));

            Assert.AreEqual(new Money(2000 - WasherCost), _wallet.Balance);
            Assert.AreEqual(1, _agents.Spawned.Count);
            Assert.AreEqual(StaffRole.PointWorker, _agents.Spawned[0].Value);
            Assert.AreEqual(StaffDestination.WorkSpot(Wash1), _agents.Destinations[0]);
            Assert.IsTrue(_service.HasWorker(Wash1), "Hired from the moment of payment.");
            Assert.AreEqual(OccupantKind.None, Point(Wash1).Occupant, "Still walking.");
            Assert.AreEqual(1, _hired.Count);
            Assert.AreEqual(Wash1, _hired[0].PointId);

            _agents.Arrive(0);

            Assert.AreEqual(OccupantKind.Worker, Point(Wash1).Occupant);
            Assert.AreEqual(StaffState.Working, _service.Staff[0].State);
            Assert.AreEqual(HireAvailability.Hired, _service.GetWorkerAvailability(Wash1));
        }

        [Test]
        public void PlayerOnTheSpot_WorkerWaits_AndTakesItOnceThePlayerLeaves()
        {
            _points.TryOccupy(Wash1, OccupantKind.Player);
            _service.TryHireWorker(Wash1);

            _agents.Arrive(0);
            _service.Tick(1f);
            Assert.AreEqual(StaffState.WaitingForSpot, _service.Staff[0].State);
            Assert.AreEqual(OccupantKind.Player, Point(Wash1).Occupant, "The player is never pushed away.");

            _points.Vacate(Wash1, OccupantKind.Player);
            _service.Tick(0.1f);

            Assert.AreEqual(StaffState.Working, _service.Staff[0].State);
            Assert.AreEqual(OccupantKind.Worker, Point(Wash1).Occupant);
        }

        [Test]
        public void SecondHire_FailsWithoutCharging()
        {
            _service.TryHireWorker(Wash1);

            Assert.IsFalse(_service.TryHireWorker(Wash1));
            Assert.AreEqual(new Money(2000 - WasherCost), _wallet.Balance);
            Assert.AreEqual(1, _agents.Spawned.Count);
        }

        [Test]
        public void WorkerAvailability_ReportsLockAndMissingConfig()
        {
            _gate.UnlockUpTo(WasherLevel - 1);
            Assert.AreEqual(HireAvailability.Locked, _service.GetWorkerAvailability(Wash1));
            Assert.IsFalse(_service.TryHireWorker(Wash1));

            Assert.AreEqual(HireAvailability.NotSupported, _service.GetWorkerAvailability(Oil), "The oil type has no worker config.");
            Assert.AreEqual(HireAvailability.NotSupported, _service.GetWorkerAvailability("unknown"));
            Assert.AreEqual(Money.Zero, _service.GetWorkerCost(Oil));
        }

        [Test]
        public void RestoreWorker_SpawnsWithoutPayment()
        {
            _service.RestoreWorker(Wash2);
            _service.RestoreWorker(Wash2);

            Assert.AreEqual(1, _agents.Spawned.Count);
            Assert.IsTrue(_service.HasWorker(Wash2));
            Assert.AreEqual(new Money(2000), _wallet.Balance);
            Assert.AreEqual(0, _hired.Count, "A restore is not a hire.");
        }

        // ── Storekeeper ──────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Storekeeper_CarriesABoxFromTheWarehouseToTheHungriestPoint()
        {
            Assert.IsTrue(_service.TryHireStorekeeper(LocationId));
            int keeper = _agents.Spawned[0].Key;
            Assert.AreEqual(StaffRole.Storekeeper, _agents.Spawned[0].Value);
            Assert.AreEqual(new Money(2000 - StorekeeperCost), _wallet.Balance);

            _service.Tick(1f);
            Assert.AreEqual(StaffState.Idle, Keeper().State, "Every stock is full.");

            Consume(Wash2, 6);
            _service.Tick(0.1f);
            Assert.AreEqual(StaffState.ToWarehouse, Keeper().State);
            Assert.AreEqual(StaffDestination.Warehouse(LocationId), _agents.Destinations[keeper]);

            _agents.Arrive(keeper);
            Assert.AreEqual(StaffState.ToPoint, Keeper().State);
            Assert.AreEqual(Wash2, Keeper().TargetPointId);
            Assert.AreEqual("shampoo", _agents.Carried[keeper]);
            Assert.AreEqual(StaffDestination.SupplyDrop(Wash2), _agents.Destinations[keeper]);
            Assert.AreEqual(new Money(2000 - StorekeeperCost - BoxPrice), _wallet.Balance);
            Assert.IsNull(_supplies.FindHungriest(LocationId), "The box on its way counts as stock.");

            _agents.Arrive(keeper);
            Assert.AreEqual(9, Point(Wash2).Supply.Current);
            Assert.AreEqual(StaffState.Idle, Keeper().State);
            Assert.IsTrue(Keeper().CarriedBox.IsNone);
            Assert.AreEqual(string.Empty, _agents.Carried[keeper]);
        }

        [Test]
        public void Storekeeper_WithoutMoney_WaitsAndThenContinues()
        {
            _service.TryHireStorekeeper(LocationId);
            int keeper = _agents.Spawned[0].Key;
            _wallet.TrySpend(_wallet.Balance);
            Consume(Wash1, 6);
            _service.Tick(0.1f);

            _agents.Arrive(keeper);
            Assert.AreEqual(StaffState.WaitingForMoney, Keeper().State);

            _service.Tick(StaffService.MoneyRetryInterval * 0.5f);
            _wallet.Add(new Money(BoxPrice));
            Assert.AreEqual(StaffState.WaitingForMoney, Keeper().State, "Retries on its own interval.");

            _service.Tick(StaffService.MoneyRetryInterval * 0.6f);
            Assert.AreEqual(StaffState.ToPoint, Keeper().State);
            Assert.AreEqual(Money.Zero, _wallet.Balance);
        }

        [Test]
        public void Storekeeper_TargetFilledMeanwhile_GoesToAnotherPoint()
        {
            _service.TryHireStorekeeper(LocationId);
            int keeper = _agents.Spawned[0].Key;
            Consume(Wash1, 8);
            Consume(Wash2, 6);
            _service.Tick(0.1f);
            _agents.Arrive(keeper);
            Assert.AreEqual(Wash1, Keeper().TargetPointId);

            // The player brings a box to wash 1 first; 7/10 + 5 no longer fits.
            _supplies.TryDeliver(new SupplyBox("shampoo", 5), Wash1, true);
            _agents.Arrive(keeper);

            Assert.AreEqual(StaffState.ToPoint, Keeper().State);
            Assert.AreEqual(Wash2, Keeper().TargetPointId);
            Assert.AreEqual(StaffDestination.SupplyDrop(Wash2), _agents.Destinations[keeper]);

            _agents.Arrive(keeper);
            Assert.AreEqual(9, Point(Wash2).Supply.Current);
            Assert.AreEqual(StaffState.Idle, Keeper().State);
        }

        [Test]
        public void Storekeeper_AtExactlyTheThreshold_StaysIdle()
        {
            _service.TryHireStorekeeper(LocationId);
            Consume(Wash1, 5);

            _service.Tick(1f);

            Assert.AreEqual(StaffState.Idle, Keeper().State, "5/10 is not below the 0.5 threshold.");
        }

        [Test]
        public void Storekeeper_PointRestockedWhileWalking_ReturnsIdleWithoutBuying()
        {
            _service.TryHireStorekeeper(LocationId);
            int keeper = _agents.Spawned[0].Key;
            Consume(Wash1, 6);
            _service.Tick(0.1f);
            Assert.AreEqual(StaffState.ToWarehouse, Keeper().State);
            Money before = _wallet.Balance;

            _supplies.TryDeliver(new SupplyBox("shampoo", 5), Wash1, true);
            _agents.Arrive(keeper);

            Assert.AreEqual(StaffState.Idle, Keeper().State);
            Assert.AreEqual(before, _wallet.Balance, "No box for a point that is 9/10 now.");
            Assert.IsTrue(Keeper().CarriedBox.IsNone);
        }

        [Test]
        public void Storekeeper_WithNowhereToPutTheBox_DropsIt_AndCanDeliverThereAgain()
        {
            _service.TryHireStorekeeper(LocationId);
            int keeper = _agents.Spawned[0].Key;
            Consume(Wash1, 6);
            _service.Tick(0.1f);
            _agents.Arrive(keeper);
            Assert.AreEqual(Wash1, Keeper().TargetPointId);

            // The player fills wash 1 meanwhile (4 + 5 = 9/10), and wash 2 is full: the box fits nowhere.
            _supplies.TryDeliver(new SupplyBox("shampoo", 5), Wash1, true);
            _agents.Arrive(keeper);

            Assert.AreEqual(StaffState.Idle, Keeper().State);
            Assert.IsTrue(Keeper().CarriedBox.IsNone);
            Assert.AreEqual(string.Empty, _agents.Carried[keeper]);

            // Incoming was cleared: once wash 1 is hungry again, the storekeeper delivers there.
            Consume(Wash1, 5);
            _service.Tick(0.1f);
            _agents.Arrive(keeper);
            Assert.AreEqual(Wash1, Keeper().TargetPointId);
            _agents.Arrive(keeper);
            Assert.AreEqual(9, Point(Wash1).Supply.Current);
        }

        [Test]
        public void StorekeeperPurchase_IsNotByThePlayer()
        {
            var bought = new List<BoxBoughtEvent>();
            Action<BoxBoughtEvent> onBought = bought.Add;
            _bus.Subscribe(onBought);
            _service.TryHireStorekeeper(LocationId);
            Consume(Wash1, 6);
            _service.Tick(0.1f);

            _agents.Arrive(_agents.Spawned[0].Key);
            _bus.Unsubscribe(onBought);

            Assert.AreEqual(1, bought.Count);
            Assert.IsFalse(bought[0].ByPlayer);
        }

        [Test]
        public void RestoreStorekeeper_WithoutStaffSettings_IsSkippedAndLogged()
        {
            _config.Staff = null;

            _service.RestoreStorekeeper(LocationId);

            Assert.AreEqual(0, _agents.Spawned.Count);
            Assert.AreEqual(1, _logger.Warnings.Count);
        }

        [Test]
        public void SecondStorekeeper_IsNotHired()
        {
            Assert.IsTrue(_service.TryHireStorekeeper(LocationId));

            Assert.AreEqual(HireAvailability.Hired, _service.GetStorekeeperAvailability(LocationId));
            Assert.IsFalse(_service.TryHireStorekeeper(LocationId));
            Assert.AreEqual(new Money(2000 - StorekeeperCost), _wallet.Balance);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            Assert.AreEqual(1, _agents.SubscriberCount);

            _service.Dispose();

            Assert.AreEqual(0, _agents.SubscriberCount);
        }

        private StaffMember Keeper()
        {
            for (int i = 0; i < _service.Staff.Count; i++)
            {
                if (_service.Staff[i].Role == StaffRole.Storekeeper)
                {
                    return _service.Staff[i];
                }
            }

            throw new InvalidOperationException("No storekeeper hired.");
        }

        private ServicePoint Point(string pointId)
        {
            Assert.IsTrue(_points.TryGet(pointId, out ServicePoint point));
            return point;
        }

        private void Consume(string pointId, int units)
        {
            SupplyStock stock = Point(pointId).Supply;
            for (int i = 0; i < units; i++)
            {
                stock.TryConsume();
            }
        }

        private void OnHired(StaffHiredEvent gameEvent) => _hired.Add(gameEvent);
    }
}
