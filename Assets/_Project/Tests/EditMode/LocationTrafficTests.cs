using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Domain.Points;
using AutoService.Domain.Traffic;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Traffic;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="LocationTraffic"/> (layout v3) with real points, bus and wallet and a fake physical layer.
    /// </summary>
    /// <remarks>
    /// The spawn interval is huge, so a car spawns on the first tick and the next one only on <see cref="SpawnNext"/>.
    /// Accept and clear delays are zero to keep the tick arithmetic readable. The plan is forced through the weights
    /// (<see cref="UseTraffic"/>) or, with mixed weights, through queued <see cref="FakeRandom.Ranges"/>
    /// (per spawn: car type, plan roll 0 = park / 1 = wash / 2 = wash then park, service type).
    /// Both entrances charge <see cref="EntranceBasePrice"/> + <see cref="EntrancePricePerSecond"/> per planned second;
    /// whole-dollar rates keep the expected fees away from rounding midpoints.
    /// </remarks>
    public sealed class LocationTrafficTests
    {
        private const string LocationId = "loc1";
        private const string MainId = "loc1_entrance_main";
        private const string ServiceId = "loc1_entrance_service";
        private const string WashId = "loc1_wash_1";
        private const string WashType = "wash";
        private const string OilId = "loc1_oil";
        private const string OilType = "oil";
        private const string ParkingType = "parking";
        private const float SpawnInterval = 1000f;
        private const float WashDuration = 6f;
        private const float EntranceDuration = 1f;
        private const float Step = 0.1f;
        private const float ParkingStay = 5f;
        private const long EntranceBasePrice = 2;
        private const double EntrancePricePerSecond = 1.0;
        private const int BlockingCar = 999;

        private const int PlanPark = 0;
        private const int PlanWash = 1;
        private const int PlanWashThenPark = 2;

        private EventBus _bus;
        private WalletService _wallet;
        private ServicePointService _points;
        private FakeCarAgents _agents;
        private FakeRandom _random;
        private FakeConfigProvider _config;
        private LocationTraffic _traffic;
        private ServicePoint _wash;
        private ServicePoint _main;
        private ServicePoint _service;
        private ServiceTypeSettings _parkingType;

        private readonly List<ServiceCompletedEvent> _completed = new List<ServiceCompletedEvent>();
        private readonly List<CarLeftEvent> _left = new List<CarLeftEvent>();
        private readonly List<CarSpawnedEvent> _spawned = new List<CarSpawnedEvent>();
        private readonly List<ParkingRefusedEvent> _refused = new List<ParkingRefusedEvent>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(new FakeGameLogger());
            _wallet = new WalletService(new Wallet(Money.Zero), _bus);
            _points = new ServicePointService(_wallet, _bus);
            _agents = new FakeCarAgents();
            _random = new FakeRandom();
            _config = new FakeConfigProvider();
            UseTraffic(0, 1, 0);

            var parking = new ServiceTypeSettings(
                ParkingType, "Parking", PointKind.Barrier, new Money(EntranceBasePrice), EntrancePricePerSecond, EntranceDuration, 0f, 0f);
            var wash = new ServiceTypeSettings(WashType, "Wash", PointKind.Service, new Money(12), 0.0, WashDuration, 0f, 0f);
            _config.ServiceTypeList.Add(parking);
            _config.ServiceTypeList.Add(wash);
            _config.CarTypeList.Add(new CarType("sedan", 1, 1.0, 60f));
            _config.CarTypeList.Add(new CarType("suv", 1, 1.5, 60f));

            _parkingType = parking;
            _main = _points.Register(parking.CreatePointDefinition(MainId, LocationId));
            _service = _points.Register(parking.CreatePointDefinition(ServiceId, LocationId));
            _wash = _points.Register(wash.CreatePointDefinition(WashId, LocationId));

            _completed.Clear();
            _left.Clear();
            _spawned.Clear();
            _refused.Clear();
            _bus.Subscribe<ServiceCompletedEvent>(OnCompleted);
            _bus.Subscribe<CarLeftEvent>(OnLeft);
            _bus.Subscribe<CarSpawnedEvent>(OnSpawned);
            _bus.Subscribe<ParkingRefusedEvent>(OnRefused);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Unsubscribe<ServiceCompletedEvent>(OnCompleted);
            _bus.Unsubscribe<CarLeftEvent>(OnLeft);
            _bus.Unsubscribe<CarSpawnedEvent>(OnSpawned);
            _bus.Unsubscribe<ParkingRefusedEvent>(OnRefused);
            _traffic?.Dispose();
            _points.Dispose();
            _wallet.Dispose();
        }

        [Test]
        public void Spawn_JoinsQueueWithAPlanOfThisLocation()
        {
            CreateTraffic();

            Tick(Step);

            Assert.AreEqual(1, _traffic.QueueCount);
            Assert.AreEqual(1, _traffic.CarsAlive);
            Assert.AreEqual(Destination.QueueSlot(0), Describe(0));
            Assert.IsTrue(_traffic.TryGetCar(0, out Car car));
            Assert.AreEqual(CarVisitPlan.WashOnly, car.Plan);
            Assert.AreEqual(WashType, car.RequestedServiceTypeId, "Barrier types are never requested.");
            Assert.AreEqual(1, _spawned.Count);
            Assert.AreEqual(LocationId, _spawned[0].LocationId);
        }

        [Test]
        public void Constructor_RejectsEntrancesThatAreNotBarriers()
        {
            Assert.Throws<ArgumentException>(() => new LocationTraffic(
                new LocationTrafficDefinition(LocationId, WashId, ServiceId, 4, 4, 2), _points, _agents, _config, _random, _bus));
            Assert.Throws<ArgumentException>(() => new LocationTraffic(
                new LocationTrafficDefinition(LocationId, MainId, "missing", 4, 4, 2), _points, _agents, _config, _random, _bus));
            Assert.Throws<ArgumentException>(() => new LocationTrafficDefinition(LocationId, MainId, MainId, 4, 4, 2));
        }

        [Test]
        public void PlanWeights_PickEveryPlan()
        {
            UseTraffic(1, 1, 1);
            CreateTraffic(queueCapacity: 3);
            QueueSpawn(PlanPark);
            QueueSpawn(PlanWash);
            QueueSpawn(PlanWashThenPark);

            int park = SpawnNext();
            int wash = SpawnNext();
            int washThenPark = SpawnNext();

            Assert.AreEqual(CarVisitPlan.ParkOnly, Plan(park));
            Assert.IsNull(_spawned[0].ServiceTypeId);
            Assert.AreEqual(CarVisitPlan.WashOnly, Plan(wash));
            Assert.AreEqual(CarVisitPlan.WashThenPark, Plan(washThenPark));
        }

        // ── Parking only ──────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ParkOnly_PaysForThePlannedStayAtTheMainEntrance_ParksAndLeavesByItself()
        {
            UseTraffic(1, 0, 0);
            CreateTraffic();
            int car = SpawnAtHead();

            Tick(Step);
            Assert.AreEqual(Destination.Entrance(MainId), Describe(car));
            Assert.AreEqual(car, _main.CarId);
            Assert.AreEqual(new Money(7), _main.CurrentPrice, "2 + 1 $/s × 5 s planned.");
            Assert.AreEqual(3, _traffic.ParkingFree, "The slot is reserved together with the barrier.");
            Assert.AreEqual(Money.Zero, _wallet.Balance, "Paid when the worker accepts.");
            Assert.AreEqual(ServicePointState.Idle, _wash.State);

            PayAtEntrance(car, MainId);
            Assert.AreEqual(new Money(7), _wallet.Balance);
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(car));

            _agents.Arrive(car);
            Tick(ParkingStay - 1f);
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(car), "Still staying.");

            Tick(1f);
            Assert.AreEqual(Destination.Exit(), Describe(car), "Automatic exit: no barrier on the way out.");
            Assert.AreEqual(4, _traffic.ParkingFree);

            _agents.Arrive(car);
            Assert.AreEqual(0, _traffic.CarsAlive);
            Assert.AreEqual(1, _left.Count);
            Assert.AreEqual(CarLeaveReason.Served, _left[0].Reason);
            Assert.AreEqual(1, _completed.Count);
            Assert.AreEqual(PointKind.Barrier, _completed[0].Kind);
            Assert.AreEqual(MainId, _completed[0].PointId);
            Assert.AreEqual(new Money(7), _wallet.Balance, "Nothing is charged on the way out.");
        }

        [Test]
        public void ParkingFee_DependsOnTheRolledStayAndCarType()
        {
            UseTraffic(1, 0, 0, stayMin: 0f, stayMax: 10f);
            CreateTraffic();
            _random.Ranges.Enqueue(1);   // car type → suv (×1.5)
            _random.Values.Enqueue(0.5f); // spawn interval
            _random.Values.Enqueue(0.6f); // stay → 6 s
            int car = SpawnAtHead();

            Tick(Step);
            PayAtEntrance(car, MainId);

            Assert.AreEqual(new Money(12), _wallet.Balance, "(2 + 1 $/s × 6 s) × 1.5.");
            Assert.IsTrue(_traffic.TryGetCar(car, out Car parked));
            Assert.AreEqual(6f, parked.PlannedStay, 0.001f);
        }

        [Test]
        public void ParkOnlyHead_WaitsForABusyMainEntranceOrAFullLot()
        {
            UseTraffic(1, 0, 0);
            CreateTraffic(parkingCapacity: 1);
            Assert.IsTrue(_main.TryReserve(BlockingCar, Money.Zero));
            int car = SpawnAtHead();

            Tick(Step);
            Assert.AreEqual(Destination.QueueSlot(0), Describe(car), "Barrier busy.");
            Assert.AreEqual(1, _traffic.ParkingFree, "Nothing reserved while waiting.");

            _main.CancelReservation(BlockingCar);
            Tick(Step);
            Assert.AreEqual(Destination.Entrance(MainId), Describe(car));

            int second = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.QueueSlot(0), Describe(second), "The only slot is taken.");
        }

        [Test]
        public void WithoutEntranceWorker_NoFeeAndTheCarDoesNotDriveIn()
        {
            UseTraffic(1, 0, 0);
            CreateTraffic();
            int car = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(car);

            Tick(30f);

            Assert.AreEqual(ServicePointState.AwaitingAccept, _main.State);
            Assert.AreEqual(Destination.Entrance(MainId), Describe(car));
            Assert.AreEqual(CarState.AtEntrance, State(car));
            Assert.AreEqual(Money.Zero, _wallet.Balance);
        }

        // ── Wash and buffer ───────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void WashOnly_DrivesStraightToTheFreeWashAndLeaves()
        {
            CreateTraffic();
            int car = SendFirstCarToWash();
            Assert.AreEqual(ServicePointState.Reserved, _wash.State);
            Assert.AreEqual(0, _traffic.QueueCount);

            ServeAtWash(car);

            Assert.AreEqual(new Money(12), _wallet.Balance);
            Assert.AreEqual(Destination.Exit(), Describe(car));
            Assert.AreEqual(ServicePointState.Idle, _service.State, "Wash-only never parks.");
            _agents.Arrive(car);
            Assert.AreEqual(0, _traffic.CarsAlive);
        }

        [Test]
        public void BusyWash_HeadWaitsInTheBuffer_AndTheQueueKeepsMovingToParking()
        {
            UseTraffic(1, 1, 1);
            CreateTraffic();
            QueueSpawn(PlanWash);
            QueueSpawn(PlanWash);
            QueueSpawn(PlanPark);
            int first = SendFirstCarToWash();

            int buffered = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.Buffer(WashId, 0), Describe(buffered));
            Assert.AreEqual(0, _traffic.QueueCount, "The buffered car left the entry queue.");
            Assert.AreEqual(1, _traffic.BufferCount(WashId));
            Assert.AreEqual(CarState.ToBuffer, State(buffered));

            int parker = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.Entrance(MainId), Describe(parker), "Parking is not blocked by the wash.");
            Assert.AreEqual(first, _wash.CarId);
        }

        [Test]
        public void Buffer_ShiftsForward_AndItsHeadGetsTheFreedWash()
        {
            CreateTraffic();
            int first = SendFirstCarToWash();
            int a = SpawnAtHead();
            Tick(Step);
            int b = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.Buffer(WashId, 0), Describe(a));
            Assert.AreEqual(Destination.Buffer(WashId, 1), Describe(b));
            _agents.Arrive(a);
            _agents.Arrive(b);

            ServeAtWash(first);
            Tick(Step);

            Assert.AreEqual(Destination.Point(WashId), Describe(a));
            Assert.AreEqual(a, _wash.CarId);
            Assert.AreEqual(Destination.Buffer(WashId, 0), Describe(b), "The rest of the buffer moves up.");
            Assert.IsFalse(GetCar(b).HasArrived, "The moved-up car must drive to its new slot first.");

            _agents.Arrive(b);
            ServeAtWash(a);
            Tick(Step);
            Assert.AreEqual(Destination.Point(WashId), Describe(b));
        }

        [Test]
        public void Head_WaitsWhenTheWashIsBusyAndItsBufferFull()
        {
            CreateTraffic();
            SendFirstCarToWash();
            SpawnAtHead();
            Tick(Step);
            SpawnAtHead();
            Tick(Step);
            int head = SpawnAtHead();

            Tick(Step);

            Assert.AreEqual(Destination.QueueSlot(0), Describe(head));
            Assert.AreEqual(1, _traffic.QueueCount);
            Assert.AreEqual(2, _traffic.BufferCount(WashId));
        }

        [Test]
        public void QueueHead_DoesNotJumpTheBuffer()
        {
            CreateTraffic();
            int first = SendFirstCarToWash();
            int buffered = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.Buffer(WashId, 0), Describe(buffered));

            // The wash frees while the buffered car is still driving in; a new head arrives at the fork.
            ServeAtWash(first);
            int head = SpawnAtHead();
            Tick(Step);

            Assert.AreEqual(ServicePointState.Idle, _wash.State, "The buffer head has not reached its slot yet.");
            Assert.AreEqual(Destination.Buffer(WashId, 1), Describe(head), "Not straight to the free wash: the buffer was first.");

            _agents.Arrive(buffered);
            Tick(Step);
            Assert.AreEqual(buffered, _wash.CarId);
        }

        [Test]
        public void WithoutBuffer_HeadWaitsAtTheFork()
        {
            CreateTraffic(bufferCapacity: 0);
            SendFirstCarToWash();
            int head = SpawnAtHead();

            Tick(Step);

            Assert.AreEqual(Destination.QueueSlot(0), Describe(head));
            Assert.AreEqual(0, _traffic.BufferCount(WashId));
        }

        // ── Wash, then park ───────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void WashThenPark_ParksThroughTheServiceEntrance()
        {
            UseTraffic(0, 0, 1);
            CreateTraffic();
            int car = SendFirstCarToWash();

            ServeAtWash(car);
            Assert.AreEqual(Destination.Entrance(ServiceId), Describe(car));
            Assert.AreEqual(car, _service.CarId);
            Assert.AreEqual(ServicePointState.Idle, _main.State);
            Assert.AreEqual(3, _traffic.ParkingFree);

            PayAtEntrance(car, ServiceId);
            Assert.AreEqual(new Money(19), _wallet.Balance, "Wash 12 + parking 2 + 1 $/s × 5 s.");
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(car));

            _agents.Arrive(car);
            Tick(ParkingStay);
            Assert.AreEqual(Destination.Exit(), Describe(car));
            Assert.AreEqual(0, _refused.Count);
            Assert.AreEqual(2, _completed.Count, "The wash and the service entrance; nothing on the way out.");
        }

        [Test]
        public void WashThenPark_WithAFullLot_LeavesAndReportsTheRefusal()
        {
            UseTraffic(0, 0, 1);
            CreateTraffic(parkingCapacity: 0);
            int car = SendFirstCarToWash();

            ServeAtWash(car);

            Assert.AreEqual(Destination.Exit(), Describe(car));
            Assert.AreEqual(ServicePointState.Idle, _service.State);
            Assert.AreEqual(1, _refused.Count);
            Assert.AreEqual(car, _refused[0].CarId);
            Assert.AreEqual(LocationId, _refused[0].LocationId);
            Assert.AreEqual(new Money(12), _wallet.Balance, "Only the wash.");
        }

        [Test]
        public void WashThenPark_WithABusyServiceEntrance_LeavesAndReportsTheRefusal()
        {
            UseTraffic(0, 0, 1);
            CreateTraffic();
            Assert.IsTrue(_service.TryReserve(BlockingCar, Money.Zero));
            int car = SendFirstCarToWash();

            ServeAtWash(car);

            Assert.AreEqual(Destination.Exit(), Describe(car));
            Assert.AreEqual(1, _refused.Count);
            Assert.AreEqual(4, _traffic.ParkingFree, "No slot is kept for a refused car.");
        }

        // ── Misc ──────────────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void QueueShift_MovesRemainingCarsForward()
        {
            CreateTraffic(queueCapacity: 3);
            int first = SpawnNext();
            int second = SpawnNext();
            Assert.AreEqual(Destination.QueueSlot(1), Describe(second));
            _agents.Arrive(second);
            _agents.Arrive(first);

            Tick(Step);

            Assert.AreEqual(Destination.Point(WashId), Describe(first));
            Assert.AreEqual(Destination.QueueSlot(0), Describe(second));
            Assert.IsFalse(GetCar(second).HasArrived, "The moved-up car must drive to its new slot first.");
        }

        [Test]
        public void FullQueue_RefusesSpawn()
        {
            CreateTraffic(queueCapacity: 2);
            SpawnNext();
            SpawnNext();

            Tick(SpawnInterval);

            Assert.AreEqual(2, _traffic.CarsAlive);
            Assert.AreEqual(2, _agents.Spawned.Count);
        }

        [Test]
        public void WithoutOccupant_NoMoneyIsCredited()
        {
            CreateTraffic();
            int car = SendFirstCarToWash();
            _agents.Arrive(car);

            Tick(30f);

            Assert.AreEqual(ServicePointState.AwaitingAccept, _wash.State);
            Assert.AreEqual(Money.Zero, _wallet.Balance);
        }

        [Test]
        public void Price_UsesCarTypeMultiplier()
        {
            CreateTraffic();
            _random.Ranges.Enqueue(1); // weighted roll → second car type (suv, ×1.5)
            int car = SendFirstCarToWash();

            ServeAtWash(car);

            Assert.AreEqual(new Money(18), _wallet.Balance);
        }

        [Test]
        public void Patience_DrainsWhileAwaitingAcceptOnly()
        {
            CreateTraffic();
            int carId = SendFirstCarToWash();
            _agents.Arrive(carId);
            Car car = GetCar(carId);
            float beforeWait = car.PatienceLeft;

            Tick(2f);
            float afterWait = car.PatienceLeft;
            _points.TryOccupy(WashId, OccupantKind.Player);
            Tick(Step);
            Tick(2f);

            Assert.AreEqual(beforeWait - 2f, afterWait, 0.001f);
            Assert.AreEqual(afterWait, car.PatienceLeft, 0.001f, "Patience is not spent during the service itself.");
        }

        [Test]
        public void Patience_AtTheEntrance_DrainsUntilAccepted_AndNotDuringTheStay()
        {
            UseTraffic(1, 0, 0);
            CreateTraffic();
            int carId = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(carId);
            Car car = GetCar(carId);
            float beforeWait = car.PatienceLeft;

            Tick(2f);
            float afterWait = car.PatienceLeft;
            _points.TryOccupy(MainId, OccupantKind.Worker);
            Tick(Step);
            Tick(EntranceDuration);
            _agents.Arrive(carId);
            Tick(ParkingStay * 0.5f);

            Assert.AreEqual(beforeWait - 2f, afterWait, 0.001f);
            Assert.AreEqual(CarState.Parked, car.State);
            Assert.AreEqual(afterWait, car.PatienceLeft, 0.001f, "Neither taking the fee nor the stay costs patience.");
        }

        [Test]
        public void LocationWithoutServicePoints_SpawnsParkingOnlyCars()
        {
            _points.Register(_parkingType.CreatePointDefinition("loc2_main", "loc2"));
            _points.Register(_parkingType.CreatePointDefinition("loc2_service", "loc2"));
            var agents = new FakeCarAgents();
            var traffic = new LocationTraffic(
                new LocationTrafficDefinition("loc2", "loc2_main", "loc2_service", 4, 4, 2), _points, agents, _config, _random, _bus);

            traffic.Tick(Step);

            Assert.AreEqual(1, traffic.CarsAlive);
            Assert.IsTrue(traffic.TryGetCar(agents.Spawned[0], out Car car));
            Assert.AreEqual(CarVisitPlan.ParkOnly, car.Plan);
            traffic.Dispose();
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            CreateTraffic();

            _traffic.Dispose();

            Assert.AreEqual(0, _agents.SubscriberCount);
        }

        [Test]
        public void AddServicePoint_CarsRequestTheNewService_AndItsBufferWorks()
        {
            CreateTraffic();
            ServicePoint oil = RegisterOil();
            _traffic.AddServicePoint(oil, 2);

            int first = SpawnRequesting(1);
            Tick(Step);
            Assert.AreEqual(OilType, GetCar(first).RequestedServiceTypeId);
            Assert.AreEqual(Destination.Point(OilId), Describe(first));

            int second = SpawnRequesting(1);
            Tick(Step);
            Assert.AreEqual(Destination.Buffer(OilId, 0), Describe(second), "The new bay has its own buffer.");
            Assert.AreEqual(1, _traffic.BufferCount(OilId));

            int washer = SpawnRequesting(0);
            Tick(Step);
            Assert.AreEqual(Destination.Point(WashId), Describe(washer), "The old bay is not blocked by the new one.");
        }

        [Test]
        public void AddServicePoint_RejectsBarriersForeignAndKnownPoints()
        {
            CreateTraffic();
            ServicePoint foreign = _points.Register(
                new ServiceTypeSettings(OilType, "Oil", PointKind.Service, new Money(25), 0.0, 9f, 0f, 0f)
                    .CreatePointDefinition("loc2_oil", "loc2"));

            Assert.Throws<ArgumentException>(() => _traffic.AddServicePoint(_main, 2));
            Assert.Throws<ArgumentException>(() => _traffic.AddServicePoint(foreign, 2));
            Assert.Throws<InvalidOperationException>(() => _traffic.AddServicePoint(_wash, 2));
        }

        [Test]
        public void SetParkingCapacity_OpensTheNextSlot()
        {
            UseTraffic(1, 0, 0);
            CreateTraffic(parkingCapacity: 2);
            for (int slot = 0; slot < 2; slot++)
            {
                int parker = SpawnAtHead();
                Tick(Step);
                PayAtEntrance(parker, MainId);
                Assert.AreEqual(Destination.ParkingSlot(slot), Describe(parker));
            }

            int third = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.QueueSlot(0), Describe(third), "The lot is full.");

            _traffic.SetParkingCapacity(3);
            Assert.AreEqual(3, _traffic.ParkingCapacity);
            Tick(Step);
            PayAtEntrance(third, MainId);

            Assert.AreEqual(Destination.ParkingSlot(2), Describe(third));
            Assert.Throws<ArgumentOutOfRangeException>(() => _traffic.SetParkingCapacity(2), "The lot never shrinks.");
        }

        [Test]
        public void SetFlowMultiplier_DividesTheSpawnInterval()
        {
            CreateTraffic();
            _traffic.SetFlowMultiplier(2.0);

            Tick(Step);
            Assert.AreEqual(1, _agents.Spawned.Count);

            Tick(SpawnInterval / 2f - 1f);
            Assert.AreEqual(1, _agents.Spawned.Count);

            Tick(1f + Step);
            Assert.AreEqual(2, _agents.Spawned.Count, "Twice the flow: the next car comes after half the interval.");
            Assert.AreEqual(2.0, _traffic.FlowMultiplier);
            Assert.Throws<ArgumentOutOfRangeException>(() => _traffic.SetFlowMultiplier(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _traffic.SetFlowMultiplier(double.NaN));
        }

        private ServicePoint RegisterOil()
        {
            var oil = new ServiceTypeSettings(OilType, "Oil Change", PointKind.Service, new Money(25), 0.0, 9f, 0f, 0f);
            _config.ServiceTypeList.Add(oil);
            return _points.Register(oil.CreatePointDefinition(OilId, LocationId));
        }

        /// <summary>
        /// Spawns a service-only car (default weights) that requests the location's service type number
        /// <paramref name="serviceIndex"/> (in the order the points joined the flow) and lets it reach the head slot.
        /// </summary>
        private int SpawnRequesting(int serviceIndex)
        {
            _random.Ranges.Enqueue(0);
            _random.Ranges.Enqueue(0);
            _random.Ranges.Enqueue(serviceIndex);
            return SpawnAtHead();
        }

        private void UseTraffic(int parkOnly, int washOnly, int washThenPark, float stayMin = ParkingStay, float stayMax = ParkingStay)
        {
            _config.Traffic = new TrafficSettings(SpawnInterval, 0f, 12, parkOnly, washOnly, washThenPark, stayMin, stayMax);
        }

        private void CreateTraffic(int queueCapacity = 4, int parkingCapacity = 4, int bufferCapacity = 2)
        {
            var definition = new LocationTrafficDefinition(LocationId, MainId, ServiceId, queueCapacity, parkingCapacity, bufferCapacity);
            _traffic = new LocationTraffic(definition, _points, _agents, _config, _random, _bus);
        }

        /// <summary>Queues the random rolls of one spawn with mixed weights (1/1/1): car type 0, the plan, service type 0.</summary>
        private void QueueSpawn(int planRoll)
        {
            _random.Ranges.Enqueue(0);
            _random.Ranges.Enqueue(planRoll);
            if (planRoll != PlanPark)
            {
                _random.Ranges.Enqueue(0);
            }
        }

        // Same order as the game loop: points first, then traffic.
        private void Tick(float deltaTime)
        {
            _points.Tick(deltaTime);
            _traffic.Tick(deltaTime);
        }

        /// <summary>Spawns the next car (the first one spawns on the first tick) and returns its id.</summary>
        private int SpawnNext()
        {
            int before = _agents.Spawned.Count;
            Tick(_agents.Spawned.Count == 0 ? Step : SpawnInterval);
            Assert.AreEqual(before + 1, _agents.Spawned.Count, "Expected a spawn.");
            return _agents.Spawned[_agents.Spawned.Count - 1];
        }

        /// <summary>Spawns a car into an empty queue and lets it reach the head slot.</summary>
        private int SpawnAtHead()
        {
            int carId = SpawnNext();
            Assert.AreEqual(Destination.QueueSlot(0), Describe(carId));
            _agents.Arrive(carId);
            return carId;
        }

        private int SendFirstCarToWash()
        {
            int carId = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.Point(WashId), Describe(carId));
            return carId;
        }

        /// <summary>The car (already sent to the entrance) arrives, a worker takes the fee and the car drives to its slot.</summary>
        private void PayAtEntrance(int carId, string entranceId)
        {
            Assert.AreEqual(Destination.Entrance(entranceId), Describe(carId));
            _agents.Arrive(carId);
            _points.TryOccupy(entranceId, OccupantKind.Worker);
            Tick(Step);
            Tick(EntranceDuration);
            _points.Vacate(entranceId, OccupantKind.Worker);
            Assert.AreEqual(CarDestinationKind.ParkingSlot, _agents.Destinations[carId].Kind);
        }

        /// <summary>Lets <paramref name="carId"/> (already sent to the wash) arrive, be accepted and served; the wash then clears.</summary>
        private void ServeAtWash(int carId)
        {
            if (!GetCar(carId).HasArrived)
            {
                _agents.Arrive(carId);
            }

            _points.TryOccupy(WashId, OccupantKind.Player);
            Tick(Step);
            Tick(WashDuration);
            _points.Vacate(WashId, OccupantKind.Player);
            Assert.AreEqual(ServicePointState.Clearing, _wash.State);
        }

        private Car GetCar(int carId)
        {
            Assert.IsTrue(_traffic.TryGetCar(carId, out Car car), "Car " + carId + " is gone.");
            return car;
        }

        private CarState State(int carId) => GetCar(carId).State;

        private CarVisitPlan Plan(int carId) => GetCar(carId).Plan;

        private string Describe(int carId) => _agents.Destinations[carId].ToString();

        private void OnCompleted(ServiceCompletedEvent gameEvent) => _completed.Add(gameEvent);

        private void OnLeft(CarLeftEvent gameEvent) => _left.Add(gameEvent);

        private void OnSpawned(CarSpawnedEvent gameEvent) => _spawned.Add(gameEvent);

        private void OnRefused(ParkingRefusedEvent gameEvent) => _refused.Add(gameEvent);

        /// <summary>Expected destinations, compared by their debug text.</summary>
        private static class Destination
        {
            public static string QueueSlot(int index) => CarDestination.QueueSlot(index).ToString();

            public static string Buffer(string pointId, int index) => CarDestination.BufferSlot(pointId, index).ToString();

            public static string Entrance(string pointId) => CarDestination.Entrance(pointId).ToString();

            public static string ParkingSlot(int index) => CarDestination.ParkingSlot(index).ToString();

            public static string Point(string pointId) => CarDestination.Point(pointId).ToString();

            public static string Exit() => CarDestination.Exit().ToString();
        }
    }
}
