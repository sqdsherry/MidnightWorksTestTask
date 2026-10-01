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
    /// Accept and clear delays are zero to keep the tick arithmetic readable.
    /// </remarks>
    public sealed class LocationTrafficTests
    {
        private const string LocationId = "loc1";
        private const string BarrierId = "loc1_barrier";
        private const string WashId = "loc1_wash_1";
        private const string WashType = "wash";
        private const string ParkingType = "parking";
        private const float SpawnInterval = 1000f;
        private const float WashDuration = 6f;
        private const float BarrierDuration = 1f;
        private const float Step = 0.1f;

        private EventBus _bus;
        private WalletService _wallet;
        private ServicePointService _points;
        private FakeCarAgents _agents;
        private FakeRandom _random;
        private FakeConfigProvider _config;
        private LocationTraffic _traffic;
        private ServicePoint _wash;
        private ServicePoint _barrier;

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
            _config = new FakeConfigProvider { Traffic = new TrafficSettings(SpawnInterval, 0f, 12) };

            var parking = new ServiceTypeSettings(ParkingType, "Parking", PointKind.Barrier, new Money(2), BarrierDuration, 0f, 0f);
            var wash = new ServiceTypeSettings(WashType, "Wash", PointKind.Service, new Money(12), WashDuration, 0f, 0f);
            _config.ServiceTypeList.Add(parking);
            _config.ServiceTypeList.Add(wash);
            _config.CarTypeList.Add(new CarType("sedan", 1, 1.0, 60f));
            _config.CarTypeList.Add(new CarType("suv", 1, 1.5, 60f));

            _barrier = _points.Register(parking.CreatePointDefinition(BarrierId, LocationId));
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
        public void Head_DrivesStraightToFreePoint()
        {
            CreateTraffic();
            SpawnAtHead();

            Tick(Step);

            Assert.AreEqual(Destination.Point(WashId), Describe(0));
            Assert.AreEqual(ServicePointState.Reserved, _wash.State);
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
        public void Head_GoesToBarrierWhenPointIsBusy()
        {
            CreateTraffic(parkingCapacity: 2);
            SendFirstCarToWash();
            int second = SpawnAtHead();

            Tick(Step);

            Assert.AreEqual(Destination.Barrier(), Describe(second));
            Assert.AreEqual(ServicePointState.Reserved, _barrier.State);
            Assert.AreEqual(1, _traffic.ParkingFree);
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
            Assert.AreEqual(ServicePointState.Idle, _barrier.State);
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
        public void ParkedCar_HasPriorityOverQueueHead()
        {
            // One parking slot: the head cannot leave the queue through the barrier while the parked car waits.
            CreateTraffic(parkingCapacity: 1);
            int first = SendFirstCarToWash();
            int parked = SpawnAtHead();
            Tick(Step);
            ParkThroughBarrier(parked);
            int head = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.QueueSlot(0), Describe(head), "Wash busy and parking full: the head waits.");

            ServeAtWash(first);
            Tick(Step);

            Assert.AreEqual(Destination.Point(WashId), Describe(parked));
            Assert.AreNotEqual(Destination.Point(WashId), Describe(head));
            Assert.AreEqual(parked, _wash.CarId);
        }

        [Test]
        public void FullCycle_ThroughParking_PaysTwiceAndPublishesCompletion()
        {
            CreateTraffic(parkingCapacity: 1);

            // Keep the wash busy with an unknown car so the first real car has to use the parking.
            _wash.TryReserve(999, Money.Zero);
            int car = SpawnAtHead();
            Tick(Step);
            Assert.AreEqual(Destination.Barrier(), Describe(car));

            ParkThroughBarrier(car);
            Assert.AreEqual(new Money(2), _wallet.Balance, "Parking fee.");

            ServeAtWash(999);
            Tick(Step);
            Assert.AreEqual(Destination.Point(WashId), Describe(car));
            Assert.AreEqual(1, _traffic.ParkingFree);

            ServeAtWash(car);
            Assert.AreEqual(new Money(14), _wallet.Balance, "Parking fee + wash.");
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
            Assert.AreEqual(2, _completed.Count, "One completion for the barrier, one for the wash.");
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
        public void Dispose_Unsubscribes()
        {
            CreateTraffic();

            _traffic.Dispose();

            Assert.AreEqual(0, _agents.SubscriberCount);
        }

        private void CreateTraffic(int queueCapacity = 4, int parkingCapacity = 4)
        {
            var definition = new LocationTrafficDefinition(LocationId, BarrierId, queueCapacity, parkingCapacity);
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

        /// <summary>The car (already sent to the barrier) arrives, is accepted by a worker and parks.</summary>
        private void ParkThroughBarrier(int carId)
        {
            _agents.Arrive(carId);
            _points.TryOccupy(BarrierId, OccupantKind.Worker);
            Tick(Step);
            Tick(BarrierDuration);
            _points.Vacate(BarrierId, OccupantKind.Worker);
            Assert.AreEqual(CarDestinationKind.ParkingSlot, _agents.Destinations[carId].Kind);
            _agents.Arrive(carId);
            Tick(Step);
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

            public static string Barrier() => CarDestination.Barrier().ToString();

            public static string Point(string pointId) => CarDestination.Point(pointId).ToString();

            public static string Exit() => CarDestination.Exit().ToString();
        }
    }
}
