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
    /// Tests for <see cref="LocationTraffic"/> with real points, bus and wallet and a fake physical layer.
    /// </summary>
    /// <remarks>
    /// The spawn interval is huge, so a car spawns on the first tick and the next one only on <see cref="SpawnNext"/>.
    /// Accept and clear delays are zero to keep the tick arithmetic readable. By default every car wants a service and
    /// parks for exactly <see cref="ParkingStay"/> seconds; tests change that through <see cref="UseTraffic"/>.
    /// The parking exit charges <see cref="ExitBasePrice"/> + <see cref="ExitPricePerSecond"/> per second parked;
    /// whole-dollar rates keep the expected fees away from rounding midpoints.
    /// </remarks>
    public sealed class LocationTrafficTests
    {
        private const string LocationId = "loc1";
        private const string ExitId = "loc1_parking_exit";
        private const string WashId = "loc1_wash_1";
        private const string WashType = "wash";
        private const string ParkingType = "parking";
        private const float SpawnInterval = 1000f;
        private const float WashDuration = 6f;
        private const float ExitDuration = 1f;
        private const float Step = 0.1f;
        private const float ParkingStay = 5f;
        private const long ExitBasePrice = 2;
        private const double ExitPricePerSecond = 1.0;
        private const int BlockingCar = 999;

        private EventBus _bus;
        private WalletService _wallet;
        private ServicePointService _points;
        private FakeCarAgents _agents;
        private FakeRandom _random;
        private FakeConfigProvider _config;
        private LocationTraffic _traffic;
        private ServicePoint _wash;
        private ServicePoint _exit;
        private ServiceTypeSettings _parkingType;

        private readonly List<ServiceCompletedEvent> _completed = new List<ServiceCompletedEvent>();
        private readonly List<CarLeftEvent> _left = new List<CarLeftEvent>();
        private readonly List<CarSpawnedEvent> _spawned = new List<CarSpawnedEvent>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(new FakeGameLogger());
            _wallet = new WalletService(new Wallet(Money.Zero), _bus);
            _points = new ServicePointService(_wallet, _bus);
            _agents = new FakeCarAgents();
            _random = new FakeRandom();
            _config = new FakeConfigProvider();
            UseTraffic(parkOnlyChance: 0f, stayMin: ParkingStay, stayMax: ParkingStay);

            var parking = new ServiceTypeSettings(
                ParkingType, "Parking", PointKind.Barrier, new Money(ExitBasePrice), ExitPricePerSecond, ExitDuration, 0f, 0f);
            var wash = new ServiceTypeSettings(WashType, "Wash", PointKind.Service, new Money(12), 0.0, WashDuration, 0f, 0f);
            _config.ServiceTypeList.Add(parking);
            _config.ServiceTypeList.Add(wash);
            _config.CarTypeList.Add(new CarType("sedan", 1, 1.0, 60f));
            _config.CarTypeList.Add(new CarType("suv", 1, 1.5, 60f));

            _parkingType = parking;
            _exit = _points.Register(parking.CreatePointDefinition(ExitId, LocationId));
            _wash = _points.Register(wash.CreatePointDefinition(WashId, LocationId));

            _completed.Clear();
            _left.Clear();
            _spawned.Clear();
            _bus.Subscribe<ServiceCompletedEvent>(OnCompleted);
            _bus.Subscribe<CarLeftEvent>(OnLeft);
            _bus.Subscribe<CarSpawnedEvent>(OnSpawned);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Unsubscribe<ServiceCompletedEvent>(OnCompleted);
            _bus.Unsubscribe<CarLeftEvent>(OnLeft);
            _bus.Unsubscribe<CarSpawnedEvent>(OnSpawned);
            _traffic?.Dispose();
            _points.Dispose();
            _wallet.Dispose();
        }

        [Test]
        public void Spawn_JoinsQueueAndRequestsServiceOfThisLocation()
        {
            CreateTraffic();

            Tick(Step);

            Assert.AreEqual(1, _traffic.QueueCount);
            Assert.AreEqual(1, _traffic.CarsAlive);
            Assert.AreEqual(Destination.QueueSlot(0), Describe(0));
            Assert.IsTrue(_traffic.TryGetCar(0, out Car car));
            Assert.AreEqual(WashType, car.RequestedServiceTypeId, "Barrier types are never requested.");
            Assert.AreEqual(1, _spawned.Count);
            Assert.AreEqual(LocationId, _spawned[0].LocationId);
        }

        [Test]
        public void Constructor_RejectsAParkingExitThatIsNotABarrier()
        {
            Assert.Throws<ArgumentException>(() => new LocationTraffic(
                new LocationTrafficDefinition(LocationId, WashId, 4, 4), _points, _agents, _config, _random, _bus));
            Assert.Throws<ArgumentException>(() => new LocationTraffic(
                new LocationTrafficDefinition(LocationId, "missing", 4, 4), _points, _agents, _config, _random, _bus));
        }

        [Test]
        public void Head_DrivesStraightToFreePoint()
        {
            CreateTraffic();
            SpawnAtHead();

            Tick(Step);

            Assert.AreEqual(Destination.Point(WashId), Describe(0));
            Assert.AreEqual(ServicePointState.Reserved, _wash.State);
            Assert.AreEqual(ServicePointState.Idle, _exit.State);
            Assert.AreEqual(0, _traffic.QueueCount);
            Assert.AreEqual(Money.Zero, _wallet.Balance, "No parking fee on the direct route.");
        }

        [Test]
        public void Head_WaitsUntilItReachesTheFork()
        {
            CreateTraffic();
            Tick(Step);

            Tick(Step);

            Assert.AreEqual(1, _traffic.QueueCount);
            Assert.AreEqual(ServicePointState.Idle, _wash.State);
        }

        [Test]
        public void Head_GoesStraightToParkingWhenPointIsBusy()
        {
            CreateTraffic(parkingCapacity: 2);
            SendFirstCarToWash();
            int second = SpawnAtHead();

            Tick(Step);

            Assert.AreEqual(Destination.ParkingSlot(0), Describe(second));
            Assert.AreEqual(ServicePointState.Idle, _exit.State, "The entry gate is automatic: the exit is not involved.");
            Assert.AreEqual(1, _traffic.ParkingFree);
            Assert.AreEqual(0, _traffic.QueueCount);
            Assert.AreEqual(Money.Zero, _wallet.Balance, "Parking is paid on the way out.");
        }

        [Test]
        public void Head_WaitsWhenParkingIsFull()
        {
            CreateTraffic(parkingCapacity: 0);
            SendFirstCarToWash();
            int second = SpawnAtHead();

            Tick(Step);

            Assert.AreEqual(Destination.QueueSlot(0), Describe(second));
            Assert.AreEqual(1, _traffic.QueueCount);
        }

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
            Assert.IsTrue(_traffic.TryGetCar(second, out Car car));
            Assert.IsFalse(car.HasArrived, "The moved-up car must drive to its new slot first.");
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
        public void ParkOnly_StaysThenPaysForTheTimeAtTheExitAndLeaves()
        {
            UseTraffic(parkOnlyChance: 1f, stayMin: ParkingStay, stayMax: ParkingStay);
            CreateTraffic();
            int car = SpawnAtHead();

            Tick(Step);
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(car), "Never straight to the free wash.");
            Assert.AreEqual(ServicePointState.Idle, _wash.State);

            _agents.Arrive(car);
            Tick(ParkingStay - 1f);
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(car), "Still staying.");
            Assert.AreEqual(ServicePointState.Idle, _exit.State);
            Assert.AreEqual(3, _traffic.ParkingFree);

            Tick(1f);
            Assert.AreEqual(Destination.ParkingExit(), Describe(car));
            Assert.AreEqual(car, _exit.CarId);
            Assert.AreEqual(4, _traffic.ParkingFree, "The slot is released when the car drives off it.");

            PayAtExit(car);
            Assert.AreEqual(new Money(7), _wallet.Balance, "2 + 1 $/s × 5 s parked.");
            Assert.AreEqual(Destination.Exit(), Describe(car));

            _agents.Arrive(car);
            Assert.AreEqual(0, _traffic.CarsAlive);
            Assert.AreEqual(1, _left.Count);
            Assert.AreEqual(CarLeaveReason.Served, _left[0].Reason);
            Assert.AreEqual(1, _completed.Count);
            Assert.AreEqual(PointKind.Barrier, _completed[0].Kind);
            Assert.AreEqual(ExitId, _completed[0].PointId);
            Assert.AreEqual(ServicePointState.Idle, _wash.State);
        }

        [Test]
        public void ParkingFee_GrowsWithTheWholeTimeOnTheLot()
        {
            UseTraffic(parkOnlyChance: 1f, stayMin: ParkingStay, stayMax: ParkingStay);
            CreateTraffic();
            int car = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(car);
            BlockExit();

            Tick(ParkingStay);
            Tick(10f);
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(car), "The exit is busy: the ready car keeps waiting.");

            UnblockExit();
            Tick(Step);
            PayAtExit(car);

            Assert.AreEqual(new Money(17), _wallet.Balance, "2 + 1 $/s × 15.1 s: the wait for the exit is charged too.");
        }

        [Test]
        public void ParkingFee_UsesCarTypeMultiplier()
        {
            UseTraffic(parkOnlyChance: 1f, stayMin: 6f, stayMax: 6f);
            CreateTraffic();
            _random.Ranges.Enqueue(1); // weighted roll → second car type (suv, ×1.5)
            int car = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(car);

            Tick(6f);
            PayAtExit(car);

            Assert.AreEqual(new Money(12), _wallet.Balance, "(2 + 1 $/s × 6 s) × 1.5.");
        }

        [Test]
        public void WithoutExitWorker_CarsDoNotLeaveTheLot()
        {
            UseTraffic(parkOnlyChance: 1f, stayMin: ParkingStay, stayMax: ParkingStay);
            CreateTraffic();
            int first = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(first);

            // The long spawn tick ends the first car's stay: it is sent to the exit.
            int second = SpawnAtHead();
            Assert.AreEqual(Destination.ParkingExit(), Describe(first));
            Tick(Step);
            _agents.Arrive(second);
            _agents.Arrive(first);

            Tick(30f);

            Assert.AreEqual(ServicePointState.AwaitingAccept, _exit.State);
            Assert.AreEqual(Destination.ParkingExit(), Describe(first));
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(second), "The exit is taken: the second car stays in its slot.");
            Assert.AreEqual(2, _traffic.CarsAlive);
            Assert.AreEqual(Money.Zero, _wallet.Balance);
        }

        [Test]
        public void ServiceCar_AfterStay_ReservesExitAndBayTogether_ThenPaysTwice()
        {
            CreateTraffic(parkingCapacity: 1);

            // Keep the wash busy with an unknown car so the first real car has to use the parking.
            _wash.TryReserve(BlockingCar, Money.Zero);
            int car = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(car));
            _agents.Arrive(car);

            Tick(ParkingStay);
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(car), "Ready, but its bay is busy.");
            Assert.AreEqual(ServicePointState.Idle, _exit.State, "The exit is not reserved without the bay.");

            ServeAtWash(BlockingCar);
            Tick(Step);
            Assert.AreEqual(Destination.ParkingExit(), Describe(car));
            Assert.AreEqual(car, _exit.CarId);
            Assert.AreEqual(car, _wash.CarId, "The bay is reserved together with the exit.");
            Assert.AreEqual(ServicePointState.Reserved, _wash.State);
            Assert.AreEqual(1, _traffic.ParkingFree);

            PayAtExit(car);
            Assert.AreEqual(new Money(13), _wallet.Balance, "Parking fee: 2 + 1 $/s × 11.2 s.");
            Assert.AreEqual(Destination.Point(WashId), Describe(car));

            ServeAtWash(car);
            Assert.AreEqual(new Money(25), _wallet.Balance, "Parking fee + wash.");
            Assert.AreEqual(Destination.Exit(), Describe(car));

            _agents.Arrive(car);

            Assert.AreEqual(0, _traffic.CarsAlive);
            CollectionAssert.Contains(_agents.Despawned, car);
            Assert.AreEqual(1, _left.Count);
            Assert.AreEqual(CarLeaveReason.Served, _left[0].Reason);
            ServiceCompletedEvent washCompletion = _completed.Find(e => e.Kind == PointKind.Service);
            Assert.AreEqual(WashId, washCompletion.PointId);
            Assert.AreEqual(car, washCompletion.CarId);
            Assert.AreEqual("sedan", washCompletion.CarTypeId);
            Assert.AreEqual(2, _completed.Count, "One completion for the exit, one for the wash.");
        }

        [Test]
        public void ServiceCar_WaitsWhenTheExitIsBusy_AndKeepsItsBayFromTheQueueHead()
        {
            CreateTraffic(parkingCapacity: 2);
            _wash.TryReserve(BlockingCar, Money.Zero);
            int parked = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(parked);
            BlockExit();
            Tick(ParkingStay);
            ServeAtWash(BlockingCar);
            Tick(Step);
            Assert.AreEqual(ServicePointState.Idle, _wash.State, "The bay is free, the exit is not.");
            Assert.AreEqual(Destination.ParkingSlot(0), Describe(parked));

            int head = SpawnAtHead();
            Tick(Step);

            Assert.AreEqual(CarDestinationKind.ParkingSlot, _agents.Destinations[head].Kind, "The bay is kept for the parked car.");
            Assert.AreEqual(ServicePointState.Idle, _wash.State);

            UnblockExit();
            Tick(Step);

            Assert.AreEqual(Destination.ParkingExit(), Describe(parked));
            Assert.AreEqual(parked, _wash.CarId);
        }

        [Test]
        public void ParkedCar_HasPriorityOverQueueHead()
        {
            // One parking slot: the head cannot park while the parked car waits for the wash.
            CreateTraffic(parkingCapacity: 1);
            int first = SendFirstCarToWash();
            int parked = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(parked);
            Tick(ParkingStay);
            int head = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.QueueSlot(0), Describe(head), "Wash busy and parking full: the head waits.");

            ServeAtWash(first);
            Tick(Step);

            Assert.AreEqual(Destination.ParkingExit(), Describe(parked));
            Assert.AreEqual(parked, _wash.CarId);
            Assert.AreNotEqual(Destination.Point(WashId), Describe(head));
        }

        [Test]
        public void ParkingOnlyCar_IsNotBlockedByAServiceCarWaitingForItsBay()
        {
            UseTraffic(parkOnlyChance: 0.5f, stayMin: ParkingStay, stayMax: ParkingStay);
            CreateTraffic();
            _wash.TryReserve(BlockingCar, Money.Zero);

            // Value() calls in order: interval A, plan A (service), stay A, interval B, plan B (parking only), stay B.
            _random.Values.Enqueue(0.5f);
            _random.Values.Enqueue(0.9f);
            _random.Values.Enqueue(0.5f);
            _random.Values.Enqueue(0.5f);
            _random.Values.Enqueue(0.1f);
            _random.Values.Enqueue(0.5f);

            int serviceCar = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(serviceCar);
            int parkOnlyCar = SpawnAtHead(); // the long spawn tick makes the service car ready (its bay is busy)
            Tick(Step);
            _agents.Arrive(parkOnlyCar);

            Tick(ParkingStay);

            Assert.IsTrue(_traffic.TryGetCar(serviceCar, out Car first) && first.WantsService);
            Assert.IsTrue(_traffic.TryGetCar(parkOnlyCar, out Car second) && !second.WantsService);
            Assert.AreEqual(CarDestinationKind.ParkingSlot, _agents.Destinations[serviceCar].Kind);
            Assert.AreEqual(Destination.ParkingExit(), Describe(parkOnlyCar));
            Assert.AreEqual(parkOnlyCar, _exit.CarId);
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
            _traffic.TryGetCar(carId, out Car car);
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
        public void Patience_AtParkingExit_DrainsOnlyUntilAccepted()
        {
            UseTraffic(parkOnlyChance: 1f, stayMin: ParkingStay, stayMax: ParkingStay);
            CreateTraffic();
            int carId = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(carId);
            Tick(ParkingStay);
            _agents.Arrive(carId);
            _traffic.TryGetCar(carId, out Car car);
            Assert.AreEqual(CarState.AtParkingExit, car.State);
            float beforeWait = car.PatienceLeft;

            Tick(2f);
            float afterWait = car.PatienceLeft;
            _points.TryOccupy(ExitId, OccupantKind.Worker);
            Tick(Step);
            Tick(ExitDuration * 0.5f);

            Assert.AreEqual(beforeWait - 2f, afterWait, 0.001f);
            Assert.AreEqual(afterWait, car.PatienceLeft, 0.001f, "Patience is not spent while the fee is being taken.");
        }

        [Test]
        public void ServiceCar_IsNotDispatchedDuringStay_AndLeavesForItsBayAfterIt()
        {
            const float longStay = 20f;
            UseTraffic(parkOnlyChance: 0f, stayMin: longStay, stayMax: longStay);
            CreateTraffic();
            _wash.TryReserve(BlockingCar, Money.Zero);
            int carId = SpawnAtHead();
            Tick(Step);
            _agents.Arrive(carId);
            _traffic.TryGetCar(carId, out Car car);
            float patienceWhenParked = car.PatienceLeft;

            ServeAtWash(BlockingCar);
            Tick(Step);

            Assert.AreEqual(ServicePointState.Idle, _wash.State, "The wash is free, but the car is still staying.");
            Assert.AreEqual(ServicePointState.Idle, _exit.State);
            Assert.AreEqual(CarDestinationKind.ParkingSlot, _agents.Destinations[carId].Kind);
            Assert.AreEqual(patienceWhenParked, car.PatienceLeft, "No patience is spent during the stay.");

            Tick(longStay);

            Assert.AreEqual(Destination.ParkingExit(), Describe(carId));
            Assert.AreEqual(carId, _wash.CarId);
        }

        [Test]
        public void StayingParkedCar_DoesNotBlockQueueHead()
        {
            const float longStay = 20f;
            UseTraffic(parkOnlyChance: 0f, stayMin: longStay, stayMax: longStay);
            CreateTraffic(parkingCapacity: 1);
            _wash.TryReserve(BlockingCar, Money.Zero);
            int parked = SpawnAtHead();
            Tick(Step);

            // Spawned before the first car parks: the long spawn tick must not eat into its stay.
            int head = SpawnAtHead();
            _agents.Arrive(parked);
            Tick(Step);
            Assert.AreEqual(Destination.QueueSlot(0), Describe(head), "Wash busy and parking full: the head waits.");

            ServeAtWash(BlockingCar);
            Tick(Step);

            Assert.AreEqual(Destination.Point(WashId), Describe(head));
            Assert.AreEqual(CarDestinationKind.ParkingSlot, _agents.Destinations[parked].Kind);
        }

        [Test]
        public void ReadyParkedCars_AreDispatchedInReadinessOrder()
        {
            UseTraffic(parkOnlyChance: 0f, stayMin: 0f, stayMax: 10f);
            CreateTraffic(parkingCapacity: 2);
            _wash.TryReserve(BlockingCar, Money.Zero);

            // Value() calls in order: interval A, stay A (9 s), interval B, stay B (1 s).
            _random.Values.Enqueue(0.5f);
            _random.Values.Enqueue(0.9f);
            _random.Values.Enqueue(0.5f);
            _random.Values.Enqueue(0.1f);

            int first = SpawnAtHead();
            Tick(Step);

            // The second car is spawned while the first is still driving to its slot, so both stays start together.
            int second = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.ParkingSlot(1), Describe(second));
            _agents.Arrive(first);
            _agents.Arrive(second);

            Tick(2f);   // the second car's short stay is over, the first one still stays
            Tick(10f);  // now both are ready: second first, first after it

            ServeAtWash(BlockingCar);
            Tick(Step);

            Assert.AreEqual(Destination.ParkingExit(), Describe(second), "Readiness order, not parking order.");
            Assert.AreEqual(second, _wash.CarId);
            Assert.AreEqual(CarDestinationKind.ParkingSlot, _agents.Destinations[first].Kind);
        }

        [Test]
        public void LocationWithoutServicePoints_SpawnsParkingOnlyCars()
        {
            _points.Register(_parkingType.CreatePointDefinition("loc2_parking_exit", "loc2"));
            var agents = new FakeCarAgents();
            var traffic = new LocationTraffic(
                new LocationTrafficDefinition("loc2", "loc2_parking_exit", 4, 4), _points, agents, _config, _random, _bus);

            traffic.Tick(Step);

            Assert.AreEqual(1, traffic.CarsAlive);
            Assert.IsTrue(traffic.TryGetCar(agents.Spawned[0], out Car car));
            Assert.IsFalse(car.WantsService);
            traffic.Dispose();
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            CreateTraffic();

            _traffic.Dispose();

            Assert.AreEqual(0, _agents.SubscriberCount);
        }

        private void UseTraffic(float parkOnlyChance, float stayMin, float stayMax)
        {
            _config.Traffic = new TrafficSettings(SpawnInterval, 0f, 12, parkOnlyChance, stayMin, stayMax);
        }

        private void CreateTraffic(int queueCapacity = 4, int parkingCapacity = 4)
        {
            var definition = new LocationTrafficDefinition(LocationId, ExitId, queueCapacity, parkingCapacity);
            _traffic = new LocationTraffic(definition, _points, _agents, _config, _random, _bus);
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

        /// <summary>Occupies the parking exit with a car unknown to the traffic.</summary>
        private void BlockExit() => Assert.IsTrue(_exit.TryReserve(BlockingCar, Money.Zero));

        private void UnblockExit() => _exit.CancelReservation(BlockingCar);

        /// <summary>The car (already sent to the parking exit) arrives, a worker takes the fee and the car drives on.</summary>
        private void PayAtExit(int carId)
        {
            Assert.AreEqual(Destination.ParkingExit(), Describe(carId));
            _agents.Arrive(carId);
            _points.TryOccupy(ExitId, OccupantKind.Worker);
            Tick(Step);
            Tick(ExitDuration);
            _points.Vacate(ExitId, OccupantKind.Worker);
            Assert.AreEqual(ServicePointState.Clearing, _exit.State);
        }

        /// <summary>Lets <paramref name="carId"/> (already sent to the wash) arrive, be accepted and served; the wash then clears.</summary>
        private void ServeAtWash(int carId)
        {
            if (_traffic.TryGetCar(carId, out Car car))
            {
                if (!car.HasArrived)
                {
                    _agents.Arrive(carId);
                }
            }
            else
            {
                _wash.NotifyCarArrived(carId);
            }

            _points.TryOccupy(WashId, OccupantKind.Player);
            Tick(Step);
            Tick(WashDuration);
            _points.Vacate(WashId, OccupantKind.Player);
            Assert.AreEqual(ServicePointState.Clearing, _wash.State);
        }

        private string Describe(int carId) => _agents.Destinations[carId].ToString();

        private void OnCompleted(ServiceCompletedEvent gameEvent) => _completed.Add(gameEvent);

        private void OnLeft(CarLeftEvent gameEvent) => _left.Add(gameEvent);

        private void OnSpawned(CarSpawnedEvent gameEvent) => _spawned.Add(gameEvent);

        /// <summary>Expected destinations, compared by their debug text.</summary>
        private static class Destination
        {
            public static string QueueSlot(int index) => CarDestination.QueueSlot(index).ToString();

            public static string ParkingSlot(int index) => CarDestination.ParkingSlot(index).ToString();

            public static string ParkingExit() => CarDestination.ParkingExit().ToString();

            public static string Point(string pointId) => CarDestination.Point(pointId).ToString();

            public static string Exit() => CarDestination.Exit().ToString();
        }
    }
}
