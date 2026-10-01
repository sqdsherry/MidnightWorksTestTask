using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
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
    /// spawns cars into the entry queue, routes the queue head either straight to a free point or through the automatic
    /// entry gate into the parking lot, lets ready parked cars out through the paid parking exit and sends served cars away.
    /// </summary>
    /// <remarks>
    /// <para><b>Plan.</b> Every car gets a plan on spawn: a service (random among the location's service points), or, with
    /// <see cref="TrafficSettings.ParkOnlyChance"/>, parking only. A parking-only car always parks and leaves after its stay;
    /// parking is a service of its own (it pays the parking fee).</para>
    /// <para><b>Parking.</b> The entrance is an automatic gate with no logic: the head simply drives to a reserved slot.
    /// Every parked car first stays a random time from the configured range (not dispatched, no patience spent), then
    /// becomes "ready". Every car leaving the lot passes the parking exit barrier — a point with a worker — and pays
    /// <c>(BasePrice + PricePerSecond × seconds parked) × car multiplier</c>. After paying, a service car drives to the point
    /// reserved for it, a parking-only car drives past the bays to the exit.</para>
    /// <para>Per tick, in this order: patience → parking stays → ready-parked dispatch → queue head → spawn. Ready parked
    /// cars are dispatched before the head so that a car which has been waiting in the lot wins a freed point (GDD §3).</para>
    /// <para>Physical movement is delegated to <see cref="ICarAgents"/>; this class only reacts to its
    /// <see cref="ICarAgents.Arrived"/> events, so the whole flow is testable without Unity. How cars get somewhere
    /// (road graph, merge zones) is entirely Presentation's business.</para>
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
        private readonly ServicePoint _parkingExit;

        private readonly Dictionary<int, Car> _cars;
        private readonly List<Car> _activeCars;

        // Parked cars whose stay is still running.
        private readonly List<int> _stayingCars;

        // Why: cars are appended the moment their stay ends, so this list is already sorted by readiness time —
        // FIFO dispatch without sorting (and without allocations).
        private readonly List<int> _readyParkedCars;

        private readonly List<string> _serviceTypeIds = new List<string>();
        private readonly List<ServicePoint> _locationPoints = new List<ServicePoint>();

        private int _nextCarId;
        private float _spawnTimer;
        private float _time;
        private bool _disposed;

        /// <summary>Creates the orchestrator and subscribes to the location's points, the queue and the agents.</summary>
        /// <param name="definition">Layout facts of the location.</param>
        /// <param name="points">Point registry; the location's points (parking exit included) must already be registered.</param>
        /// <param name="agents">Physical cars of this location.</param>
        /// <param name="config">Car types and traffic settings.</param>
        /// <param name="random">Randomness for spawn timing, car type and requested service.</param>
        /// <param name="eventBus">Bus for spawn / served / left events.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the parking exit point is missing, not a barrier or belongs to another location.
        /// </exception>
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

            _parkingExit = ResolveParkingExit(definition, points);
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
            _stayingCars = new List<int>(definition.ParkingCapacity);
            _readyParkedCars = new List<int>(definition.ParkingCapacity);

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
            TickParkingStays(deltaTime);
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

        private static ServicePoint ResolveParkingExit(LocationTrafficDefinition definition, IServicePointService points)
        {
            string pointId = definition.ParkingExitPointId;
            if (!points.TryGet(pointId, out ServicePoint exit))
            {
                throw new ArgumentException("Parking exit point '" + pointId + "' is not registered.", nameof(definition));
            }

            if (exit.Definition.Kind != PointKind.Barrier)
            {
                throw new ArgumentException("Point '" + pointId + "' is not a barrier, it cannot be the parking exit.", nameof(definition));
            }

            if (!string.Equals(exit.Definition.LocationId, definition.LocationId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Parking exit '" + pointId + "' belongs to location '" + exit.Definition.LocationId
                    + "', not '" + definition.LocationId + "'.",
                    nameof(definition));
            }

            return exit;
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

                // Why: at the parking exit and at a point the wait ends when the order is accepted;
                // the service time itself costs no patience.
                if (car.State == CarState.AtParkingExit)
                {
                    if (_parkingExit.State == ServicePointState.AwaitingAccept)
                    {
                        car.TickPatience(deltaTime);
                    }

                    continue;
                }

                if (car.State == CarState.AtPoint)
                {
                    if (_points.TryGet(car.TargetPointId, out ServicePoint point) && point.State == ServicePointState.AwaitingAccept)
                    {
                        car.TickPatience(deltaTime);
                    }

                    continue;
                }

                // The car itself ignores states in which it is not waiting (driving, leaving, parking stay).
                car.TickPatience(deltaTime);
            }
        }

        private void TickParkingStays(float deltaTime)
        {
            for (int i = 0; i < _stayingCars.Count;)
            {
                Car car = _cars[_stayingCars[i]];
                car.TickParkingStay(deltaTime);
                if (!car.IsReadyToLeaveParking)
                {
                    i++;
                    continue;
                }

                // Why: parking-only and service cars alike leave through the paid exit, so both wait in one FIFO.
                _stayingCars.RemoveAt(i);
                _readyParkedCars.Add(car.Id);
            }
        }

        private void DispatchParkedCars()
        {
            for (int i = 0; i < _readyParkedCars.Count;)
            {
                // Why: every car leaves through the one exit barrier; once it is taken nobody else can leave this tick.
                if (!_parkingExit.IsAvailable)
                {
                    return;
                }

                Car car = _cars[_readyParkedCars[i]];
                if (TryLeaveParking(car))
                {
                    _readyParkedCars.RemoveAt(i);
                    continue;
                }

                // Why: FIFO, but not blocking — a service car waiting for its bay must not keep a parking-only car
                // (or a car wanting another bay) behind it locked in the lot.
                i++;
            }
        }

        /// <returns>True when the car was sent to the parking exit.</returns>
        private bool TryLeaveParking(Car car)
        {
            Money fee = ParkingFeeFor(car);
            if (!car.WantsService)
            {
                _parkingExit.TryReserve(car.Id, fee);
                SendToParkingExit(car, null);
                return true;
            }

            ServicePoint point = _points.FindAvailable(_definition.LocationId, car.RequestedServiceTypeId);
            if (point == null)
            {
                return false;
            }

            // Why: the exit and the bay are reserved together. Reserving only the exit would let the car pay and then
            // stand in the merge lane with no free bay; reserving only the bay would let the queue head see it taken
            // while the car may still be stuck behind a busy exit. Both are checked as available first, so the rollback
            // below is a safety net, not a normal path.
            if (!_parkingExit.TryReserve(car.Id, fee))
            {
                return false;
            }

            if (!point.TryReserve(car.Id, PriceFor(point, car)))
            {
                _parkingExit.CancelReservation(car.Id);
                return false;
            }

            SendToParkingExit(car, point.Definition.Id);
            return true;
        }

        private void SendToParkingExit(Car car, string nextPointId)
        {
            _parking.Release(car.ParkingSlot);
            car.SendToParkingExit(nextPointId);
            _agents.MoveTo(car.Id, CarDestination.ParkingExit());
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

            // Why: a parking-only car never takes a point; if the lot is full it simply waits at the head.
            if (head.WantsService)
            {
                string serviceTypeId = head.RequestedServiceTypeId;
                ServicePoint point = _points.FindAvailable(_definition.LocationId, serviceTypeId);

                // Why: a ready parked car has waited longer, so the head must not take "its" bay. Normally such a car was
                // dispatched earlier in this tick; it can still be waiting when the bay is free but the exit is busy.
                // Cars still in their parking stay do not block the head: they do not want the point yet.
                if (point != null && !IsReadyParkedCarWaitingFor(serviceTypeId) && point.TryReserve(head.Id, PriceFor(point, head)))
                {
                    _queue.RemoveHead();
                    head.SendToPoint(point.Definition.Id);
                    _agents.MoveTo(head.Id, CarDestination.Point(point.Definition.Id));
                    return;
                }
            }

            // Why: the entry gate is automatic and free; only a parking slot is needed, reserved before the car leaves
            // the queue so it can never end up with nowhere to park.
            if (!_parking.TryReserve(head.Id, out int parkingSlot))
            {
                return;
            }

            _queue.RemoveHead();
            head.SendToParking(parkingSlot, NextParkingStay());
            _agents.MoveTo(head.Id, CarDestination.ParkingSlot(parkingSlot));
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
            if (_queue.IsFull || _activeCars.Count >= _traffic.MaxCarsAlive || _totalSpawnWeight <= 0)
            {
                return;
            }

            CarType carType = PickCarType();
            string serviceTypeId = PickPlan();
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

        /// <returns>The requested service type id, or null for a parking-only car.</returns>
        private string PickPlan()
        {
            // Why: without service points the location still earns from parking, so cars keep coming as parking-only.
            if (_serviceTypeIds.Count == 0)
            {
                return null;
            }

            // Why: the roll is skipped for 0 and 1, so the outcome (and the random sequence) is exact at the extremes.
            float chance = _traffic.ParkOnlyChance;
            bool parkOnly = chance >= 1f || (chance > 0f && _random.Value() < chance);
            return parkOnly ? null : _serviceTypeIds[_random.Range(0, _serviceTypeIds.Count)];
        }

        private float NextParkingStay()
        {
            return _traffic.ParkingStayMin + (_traffic.ParkingStayMax - _traffic.ParkingStayMin) * _random.Value();
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

        private bool IsReadyParkedCarWaitingFor(string serviceTypeId)
        {
            for (int i = 0; i < _readyParkedCars.Count; i++)
            {
                if (string.Equals(_cars[_readyParkedCars[i]].RequestedServiceTypeId, serviceTypeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // Why: the fee scales with the car type too, so every payment follows the same rule.
        private static Money PriceFor(ServicePoint point, Car car) => point.Definition.BasePrice * car.Type.PriceMultiplier;

        // Why: charged for the whole time on the lot — the stay plus any wait for the exit or a bay.
        private Money ParkingFeeFor(Car car)
        {
            ServicePointDefinition exit = _parkingExit.Definition;
            double secondsParked = Math.Max(0.0, _time - car.ParkedAtTime);
            return PriceFormula.TimeBased(exit.BasePrice, exit.PricePerSecond, secondsParked, car.Type.PriceMultiplier);
        }

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
                case CarState.Parked:
                    _stayingCars.Add(carId);
                    break;
                case CarState.AtParkingExit:
                    _parkingExit.NotifyCarArrived(carId);
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
                // Paid at the exit: on to the bay reserved at dispatch, or away.
                car.ContinueFromParkingExit();
                _agents.MoveTo(carId, car.State == CarState.ToPoint ? CarDestination.Point(car.TargetPointId) : CarDestination.Exit());
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
