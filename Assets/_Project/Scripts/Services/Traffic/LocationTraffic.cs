using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Points;
using AutoService.Domain.Traffic;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Events;
using AutoService.Services.Points;

namespace AutoService.Services.Traffic
{
    /// <summary>
    /// Orchestrates the car flow of ONE location (a second location is a second instance):
    /// spawns cars into the entry queue, routes the queue head either straight to a free point or through the parking
    /// barrier into the parking lot, dispatches parked cars to freed points and sends served cars to the exit.
    /// </summary>
    /// <remarks>
    /// <para>Per tick, in this order: patience → parked dispatch → queue head → spawn. Parked cars are dispatched before
    /// the head so that a car which has been waiting in the lot always wins a freed point (GDD §3).</para>
    /// <para>Physical movement is delegated to <see cref="ICarAgents"/>; this class only reacts to its
    /// <see cref="ICarAgents.Arrived"/> events, so the whole flow is testable without Unity.</para>
    /// <para>Single publisher of <see cref="ServiceCompletedEvent"/> (it is the one who knows the car type) and of
    /// <see cref="CarSpawnedEvent"/> / <see cref="CarLeftEvent"/>.</para>
    /// <para>Steady state allocates nothing: collections are preallocated and iterated by index. The only allocation is the
    /// <see cref="Car"/> object created on spawn (once every few seconds, not per frame).</para>
    /// </remarks>
    public sealed class LocationTraffic : ITickable, IDisposable
    {
        // Why: jitter may exceed the interval in the config; spawning every frame would flood the queue check for nothing.
        private const float MinSpawnInterval = 0.1f;

        private readonly LocationTrafficDefinition _definition;
        private readonly IServicePointService _points;
        private readonly ICarAgents _agents;
        private readonly IRandom _random;
        private readonly IEventBus _eventBus;
        private readonly TrafficSettings _traffic;
        private readonly IReadOnlyList<CarType> _carTypes;
        private readonly int _totalSpawnWeight;

        private readonly EntryQueue _queue;
        private readonly ParkingLot _parking;
        private readonly ServicePoint _barrier;

        private readonly Dictionary<int, Car> _cars;
        private readonly List<Car> _activeCars;

        // Why: cars are appended when they park, and game time only grows, so this list is already sorted by
        // ParkedAtTime — FIFO dispatch without sorting (and without allocations).
        private readonly List<int> _parkedOrder;

        private readonly List<string> _serviceTypeIds = new List<string>();
        private readonly List<ServicePoint> _locationPoints = new List<ServicePoint>();

        private int _nextCarId;
        private float _spawnTimer;
        private float _time;
        private bool _disposed;

        /// <summary>Creates the orchestrator and subscribes to the location's points, the queue and the agents.</summary>
        /// <param name="definition">Layout facts of the location.</param>
        /// <param name="points">Point registry; the location's points (barrier included) must already be registered.</param>
        /// <param name="agents">Physical cars of this location.</param>
        /// <param name="config">Car types and traffic settings.</param>
        /// <param name="random">Randomness for spawn timing, car type and requested service.</param>
        /// <param name="eventBus">Bus for spawn / served / left events.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the barrier point is missing, not a barrier or belongs to another location.</exception>
        public LocationTraffic(
            LocationTrafficDefinition definition,
            IServicePointService points,
            ICarAgents agents,
            IConfigProvider config,
            IRandom random,
            IEventBus eventBus)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _agents = agents ?? throw new ArgumentNullException(nameof(agents));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _barrier = ResolveBarrier(definition, points);
            _traffic = config.Traffic;
            _carTypes = config.CarTypes;
            for (int i = 0; i < _carTypes.Count; i++)
            {
                _totalSpawnWeight += _carTypes[i].SpawnWeight;
            }

            _queue = new EntryQueue(definition.QueueCapacity);
            _parking = new ParkingLot(definition.ParkingCapacity);

            int maxCars = _traffic.MaxCarsAlive;
            _cars = new Dictionary<int, Car>(maxCars);
            _activeCars = new List<Car>(maxCars);
            _parkedOrder = new List<int>(definition.ParkingCapacity);

            CollectLocationPoints();

            _queue.Shifted += OnQueueShifted;
            _agents.Arrived += OnCarArrived;
        }

        /// <summary>Id of the location.</summary>
        public string LocationId => _definition.LocationId;

        /// <summary>Number of cars in the entry queue.</summary>
        public int QueueCount => _queue.Count;

        /// <summary>Number of free (unreserved) parking slots.</summary>
        public int ParkingFree => _parking.FreeCount;

        /// <summary>Number of cars currently present in the location.</summary>
        public int CarsAlive => _activeCars.Count;

        /// <summary>Looks up a living car by id (for UI / debugging).</summary>
        public bool TryGetCar(int carId, out Car car) => _cars.TryGetValue(carId, out car);

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // Why: dt is 0 while paused; the flow freezes completely instead of spawning or dispatching into a stopped world.
            if (_disposed || deltaTime <= 0f)
            {
                return;
            }

            _time += deltaTime;
            TickPatience(deltaTime);
            DispatchParkedCars();
            DispatchQueueHead();
            TickSpawn(deltaTime);
        }

        /// <summary>Unsubscribes from the points, the queue and the agents. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _queue.Shifted -= OnQueueShifted;
            _agents.Arrived -= OnCarArrived;
            for (int i = 0; i < _locationPoints.Count; i++)
            {
                _locationPoints[i].ServiceCompleted -= OnServiceCompleted;
            }
        }

        private static ServicePoint ResolveBarrier(LocationTrafficDefinition definition, IServicePointService points)
        {
            if (!points.TryGet(definition.BarrierPointId, out ServicePoint barrier))
            {
                throw new ArgumentException("Barrier point '" + definition.BarrierPointId + "' is not registered.", nameof(definition));
            }

            if (barrier.Definition.Kind != PointKind.Barrier)
            {
                throw new ArgumentException("Point '" + definition.BarrierPointId + "' is not a barrier.", nameof(definition));
            }

            if (!string.Equals(barrier.Definition.LocationId, definition.LocationId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Barrier '" + definition.BarrierPointId + "' belongs to location '" + barrier.Definition.LocationId
                    + "', not '" + definition.LocationId + "'.",
                    nameof(definition));
            }

            return barrier;
        }

        // TODO(05-build): points built later must be added here (subscription + requested service types).
        private void CollectLocationPoints()
        {
            IReadOnlyList<ServicePoint> all = _points.All;
            for (int i = 0; i < all.Count; i++)
            {
                ServicePoint point = all[i];
                ServicePointDefinition pointDefinition = point.Definition;
                if (!string.Equals(pointDefinition.LocationId, _definition.LocationId, StringComparison.Ordinal))
                {
                    continue;
                }

                _locationPoints.Add(point);
                point.ServiceCompleted += OnServiceCompleted;

                // Why: a car may only request a service that exists here; barriers are a means, not a requested service.
                if (pointDefinition.Kind == PointKind.Service && !_serviceTypeIds.Contains(pointDefinition.ServiceTypeId))
                {
                    _serviceTypeIds.Add(pointDefinition.ServiceTypeId);
                }
            }
        }

        private void TickPatience(float deltaTime)
        {
            for (int i = 0; i < _activeCars.Count; i++)
            {
                Car car = _activeCars[i];
                if (car.State == CarState.AtPoint)
                {
                    // Why: at a point the wait ends when the order is accepted; the service time itself costs no patience.
                    if (_points.TryGet(car.TargetPointId, out ServicePoint point) && point.State == ServicePointState.AwaitingAccept)
                    {
                        car.TickPatience(deltaTime);
                    }

                    continue;
                }

                // The car itself ignores states in which it is not waiting (driving, leaving).
                car.TickPatience(deltaTime);
            }
        }

        private void DispatchParkedCars()
        {
            for (int i = 0; i < _parkedOrder.Count;)
            {
                Car car = _cars[_parkedOrder[i]];
                ServicePoint point = _points.FindAvailable(_definition.LocationId, car.RequestedServiceTypeId);
                if (point == null || !point.TryReserve(car.Id, PriceFor(point, car)))
                {
                    i++;
                    continue;
                }

                _parking.Release(car.ParkingSlot);
                _parkedOrder.RemoveAt(i);
                SendToPoint(car, point);
            }
        }

        private void DispatchQueueHead()
        {
            int headId = _queue.Head;
            if (headId == EntryQueue.None)
            {
                return;
            }

            Car head = _cars[headId];
            if (!head.HasArrived)
            {
                // Why: the head decides at the fork; deciding while it is still driving up would let it cut corners.
                return;
            }

            string serviceTypeId = head.RequestedServiceTypeId;
            ServicePoint point = _points.FindAvailable(_definition.LocationId, serviceTypeId);

            // Why: parked cars are dispatched earlier in the same tick, so normally none of them can still be waiting for an
            // available point; the explicit check keeps the priority rule true regardless of the call order.
            if (point != null && !IsParkedCarWaitingFor(serviceTypeId) && point.TryReserve(head.Id, PriceFor(point, head)))
            {
                _queue.RemoveHead();
                SendToPoint(head, point);
                return;
            }

            // Why: both the barrier and a parking slot are reserved up front, so a car sent to the barrier can never end up
            // with nowhere to park.
            if (!_barrier.IsAvailable || !_parking.TryReserve(head.Id, out int parkingSlot))
            {
                return;
            }

            _barrier.TryReserve(head.Id, PriceFor(_barrier, head));
            _queue.RemoveHead();
            head.SendToBarrier(parkingSlot);
            _agents.MoveTo(head.Id, CarDestination.Barrier());
        }

        private void TickSpawn(float deltaTime)
        {
            _spawnTimer -= deltaTime;
            if (_spawnTimer > 0f)
            {
                return;
            }

            // Why: the timer restarts even if the spawn is refused (full queue = the car drives past, GDD §5.1),
            // so the flow keeps its rhythm instead of bursting the moment a slot frees up.
            _spawnTimer = NextSpawnInterval();
            TrySpawn();
        }

        private void TrySpawn()
        {
            if (_queue.IsFull || _activeCars.Count >= _traffic.MaxCarsAlive || _serviceTypeIds.Count == 0 || _totalSpawnWeight <= 0)
            {
                return;
            }

            CarType carType = PickCarType();
            string serviceTypeId = _serviceTypeIds[_random.Range(0, _serviceTypeIds.Count)];
            int carId = _nextCarId++;

            // Why: the only allocation of the flow — once per spawned car, every few seconds; never per frame.
            var car = new Car(carId, carType, serviceTypeId);
            _queue.TryEnqueue(carId, out int slot);
            _cars.Add(carId, car);
            _activeCars.Add(car);

            car.EnterQueue();
            _agents.Spawn(carId, carType.Id);
            _agents.MoveTo(carId, CarDestination.QueueSlot(slot));
            _eventBus.Publish(new CarSpawnedEvent(carId, carType.Id, serviceTypeId, _definition.LocationId));
        }

        private float NextSpawnInterval()
        {
            float offset = _traffic.SpawnIntervalJitter * (_random.Value() * 2f - 1f);
            return Math.Max(MinSpawnInterval, _traffic.SpawnInterval + offset);
        }

        private CarType PickCarType()
        {
            int roll = _random.Range(0, _totalSpawnWeight);
            for (int i = 0; i < _carTypes.Count; i++)
            {
                roll -= _carTypes[i].SpawnWeight;
                if (roll < 0)
                {
                    return _carTypes[i];
                }
            }

            // Unreachable for a roll inside [0, total); kept as a safe fallback for a misbehaving IRandom.
            return _carTypes[_carTypes.Count - 1];
        }

        private bool IsParkedCarWaitingFor(string serviceTypeId)
        {
            for (int i = 0; i < _parkedOrder.Count; i++)
            {
                if (string.Equals(_cars[_parkedOrder[i]].RequestedServiceTypeId, serviceTypeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void SendToPoint(Car car, ServicePoint point)
        {
            string pointId = point.Definition.Id;
            car.SendToPoint(pointId);
            _agents.MoveTo(car.Id, CarDestination.Point(pointId));
        }

        // Why: the barrier fee scales with the car type too, so every payment follows the same rule.
        private static Money PriceFor(ServicePoint point, Car car) => point.Definition.BasePrice * car.Type.PriceMultiplier;

        private void OnQueueShifted(EntryQueue queue)
        {
            for (int slot = 0; slot < queue.Count; slot++)
            {
                int carId = queue.GetAt(slot);
                _cars[carId].MoveUpInQueue();
                _agents.MoveTo(carId, CarDestination.QueueSlot(slot));
            }
        }

        private void OnCarArrived(int carId)
        {
            // Why: duplicate or stale reports (e.g. a fallback arrival after a newer one) must not advance the flow twice.
            if (!_cars.TryGetValue(carId, out Car car) || car.HasArrived)
            {
                return;
            }

            car.MarkArrived(_time);
            switch (car.State)
            {
                case CarState.AtBarrier:
                    _barrier.NotifyCarArrived(carId);
                    break;
                case CarState.Parked:
                    _parkedOrder.Add(carId);
                    break;
                case CarState.AtPoint:
                    if (_points.TryGet(car.TargetPointId, out ServicePoint point))
                    {
                        point.NotifyCarArrived(carId);
                    }

                    break;
                case CarState.Leaving:
                    RemoveCar(car, CarLeaveReason.Served);
                    break;
            }
        }

        private void OnServiceCompleted(ServicePoint point, int carId)
        {
            if (!_cars.TryGetValue(carId, out Car car))
            {
                return;
            }

            ServicePointDefinition pointDefinition = point.Definition;
            if (pointDefinition.Kind == PointKind.Barrier)
            {
                car.SendToParking(0f);
                _agents.MoveTo(carId, CarDestination.ParkingSlot(car.ParkingSlot));
            }
            else
            {
                car.Leave();
                _agents.MoveTo(carId, CarDestination.Exit());
            }

            _eventBus.Publish(new ServiceCompletedEvent(
                pointDefinition.Id, pointDefinition.ServiceTypeId, pointDefinition.Kind, carId, car.Type.Id));
        }

        private void RemoveCar(Car car, CarLeaveReason reason)
        {
            _agents.Despawn(car.Id);
            _cars.Remove(car.Id);
            _activeCars.Remove(car);
            _eventBus.Publish(new CarLeftEvent(car.Id, reason));
        }
    }
}
