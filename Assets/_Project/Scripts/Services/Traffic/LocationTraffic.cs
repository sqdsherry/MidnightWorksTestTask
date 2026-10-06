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
    /// Orchestrates the car flow of ONE location (a second location is a second instance), layout v3 (GDD §3):
    /// spawns cars into the entry queue, sends the queue head to its service point (or into the point's buffer) or through
    /// the main parking entrance, lets served cars park through the service entrance and parked cars leave by themselves.
    /// </summary>
    /// <remarks>
    /// <para><b>Plans.</b> Every car gets a <see cref="CarVisitPlan"/> on spawn, weighted by <see cref="TrafficSettings"/>
    /// (a location without service points only gets parking-only cars). "Wash" means the car's requested service type,
    /// picked at random among the location's service points.</para>
    /// <para><b>Parking.</b> Both entrances are barrier points with a worker. The parking slot and the barrier are reserved
    /// together, and the stay is rolled at that moment: the barrier's order price is the time-based fee of the planned stay
    /// (<see cref="PriceFormula.TimeBased"/>), paid when the order is accepted. The car then parks, stays (no patience spent)
    /// and drives out automatically — there is no exit barrier.</para>
    /// <para><b>Buffers.</b> Every service point has its own buffer (an <see cref="EntryQueue"/>) of
    /// <see cref="LocationTrafficDefinition.ServiceBufferCapacity"/> slots (or the capacity given to
    /// <see cref="AddServicePoint"/>) on the lane in front of it, so a head waiting
    /// for a busy bay leaves the entry queue and does not block parking-only cars behind it. The buffer head takes the point
    /// as soon as it is idle; the queue head only drives straight to a point whose buffer is empty, so nobody jumps the line.</para>
    /// <para><b>Growth.</b> Built later (module A1): <see cref="AddServicePoint"/> adds a bay with its buffer and service type,
    /// <see cref="SetParkingCapacity"/> opens more slots, <see cref="SetFlowMultiplier"/> makes cars come more often.</para>
    /// <para>Per tick, in this order: patience → parking stays (auto-exit) → buffer heads → queue head → spawn.</para>
    /// <para>Physical movement is delegated to <see cref="ICarAgents"/>; this class only reacts to its
    /// <see cref="ICarAgents.Arrived"/> events, so the whole flow is testable without Unity. How cars get somewhere
    /// (road graph, merge zones) is entirely Presentation's business.</para>
    /// <para>Single publisher of <see cref="ServiceCompletedEvent"/> (it is the one who knows the car type) and of
    /// <see cref="CarSpawnedEvent"/> / <see cref="CarLeftEvent"/> / <see cref="ParkingRefusedEvent"/>.</para>
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
        private readonly int _totalPlanWeight;

        private readonly EntryQueue _queue;
        private readonly ParkingLot _parking;
        private readonly ServicePoint _mainEntrance;
        private readonly ServicePoint _serviceEntrance;

        private readonly Dictionary<int, Car> _cars;
        private readonly List<Car> _activeCars;

        // Parked cars whose stay is still running.
        private readonly List<int> _parkedCars;

        private readonly List<string> _serviceTypeIds = new List<string>();
        private readonly List<ServicePoint> _locationPoints = new List<ServicePoint>();

        // Why: one buffer per service point, parallel lists iterated by index — no lookups by id in the tick.
        // A point without a buffer has a null entry, so the indices always match.
        private readonly List<ServicePoint> _servicePoints = new List<ServicePoint>();
        private readonly List<EntryQueue> _buffers = new List<EntryQueue>();

        private int _nextCarId;
        private float _spawnTimer;
        private double _flowMultiplier = 1.0;
        private bool _disposed;

        /// <summary>Creates the orchestrator and subscribes to the location's points, the queues and the agents.</summary>
        /// <param name="definition">Layout facts of the location.</param>
        /// <param name="points">Point registry; the location's points (both entrances included) must already be registered.</param>
        /// <param name="agents">Physical cars of this location.</param>
        /// <param name="config">Car types and traffic settings.</param>
        /// <param name="random">Randomness for spawn timing, car type, plan, requested service and parking stay.</param>
        /// <param name="eventBus">Bus for spawn / served / refused / left events.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when an entrance point is missing, not a barrier or belongs to another location.
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

            _mainEntrance = ResolveEntrance(definition, definition.MainEntranceId, points);
            _serviceEntrance = ResolveEntrance(definition, definition.ServiceEntranceId, points);
            _traffic = config.Traffic;
            _totalPlanWeight = _traffic.ParkOnlyWeight + _traffic.ServiceOnlyWeight + _traffic.ServiceThenParkWeight;
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
            _parkedCars = new List<int>(definition.ParkingCapacity);

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

        /// <summary>Number of usable parking slots (layout slots 0..ParkingCapacity-1).</summary>
        public int ParkingCapacity => _parking.Capacity;

        /// <summary>Current car flow multiplier (1 = the configured spawn interval).</summary>
        public double FlowMultiplier => _flowMultiplier;

        /// <summary>Number of cars currently present in the location.</summary>
        public int CarsAlive => _activeCars.Count;

        /// <summary>Looks up a living car by id (for UI / debugging).</summary>
        public bool TryGetCar(int carId, out Car car) => _cars.TryGetValue(carId, out car);

        /// <summary>Number of cars in the buffer of <paramref name="pointId"/> (0 for an unknown point or no buffer).</summary>
        public int BufferCount(string pointId)
        {
            int index = IndexOfServicePoint(pointId);
            return index >= 0 && _buffers[index] != null ? _buffers[index].Count : 0;
        }

        /// <summary>
        /// Adds a service point built after the start: cars start requesting its service type, it gets its own buffer
        /// and its completions are handled like those of the starting points.
        /// </summary>
        /// <param name="point">The point, already registered in the point service.</param>
        /// <param name="bufferCapacity">Buffer slots in front of the point (&gt;= 0; 0 = no buffer).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="point"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the point is not a service, belongs to another location or the capacity is negative.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown when the point is already part of the flow.</exception>
        public void AddServicePoint(ServicePoint point, int bufferCapacity)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            ServicePointDefinition pointDefinition = point.Definition;
            if (pointDefinition.Kind != PointKind.Service)
            {
                throw new ArgumentException("Point '" + pointDefinition.Id + "' is not a service point.", nameof(point));
            }

            if (!string.Equals(pointDefinition.LocationId, _definition.LocationId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Point '" + pointDefinition.Id + "' belongs to location '" + pointDefinition.LocationId
                    + "', not '" + _definition.LocationId + "'.",
                    nameof(point));
            }

            if (bufferCapacity < 0)
            {
                throw new ArgumentException("Buffer capacity must be non-negative.", nameof(bufferCapacity));
            }

            if (_locationPoints.Contains(point))
            {
                throw new InvalidOperationException("Point '" + pointDefinition.Id + "' is already part of the flow.");
            }

            AddLocationPoint(point, bufferCapacity);
        }

        /// <summary>Opens more parking slots; existing reservations keep theirs.</summary>
        /// <param name="capacity">New number of usable slots; the layout's slots 0..capacity-1 are used.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the capacity would shrink.</exception>
        public void SetParkingCapacity(int capacity)
        {
            _parking.SetCapacity(capacity);
        }

        /// <summary>Scales the car flow: the spawn interval becomes the configured one divided by <paramref name="multiplier"/>.</summary>
        /// <remarks>Applies from the next spawn on; the jitter stays as configured.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a non-positive, infinite or NaN multiplier.</exception>
        public void SetFlowMultiplier(double multiplier)
        {
            // Why: the negated comparison also rejects NaN.
            if (!(multiplier > 0.0) || double.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "Flow multiplier must be a finite positive number.");
            }

            _flowMultiplier = multiplier;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // Why: dt is 0 while paused; the flow freezes completely instead of spawning or dispatching into a stopped world.
            if (_disposed || deltaTime <= 0f)
            {
                return;
            }

            TickPatience(deltaTime);
            TickParkingStays(deltaTime);
            DispatchBufferHeads();
            DispatchQueueHead();
            TickSpawn(deltaTime);
        }

        /// <summary>Unsubscribes from the points, the queues and the agents. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _queue.Shifted -= OnQueueShifted;
            for (int i = 0; i < _buffers.Count; i++)
            {
                if (_buffers[i] != null)
                {
                    _buffers[i].Shifted -= OnBufferShifted;
                }
            }

            _agents.Arrived -= OnCarArrived;
            for (int i = 0; i < _locationPoints.Count; i++)
            {
                _locationPoints[i].ServiceCompleted -= OnServiceCompleted;
            }
        }

        private static ServicePoint ResolveEntrance(LocationTrafficDefinition definition, string pointId, IServicePointService points)
        {
            if (pointId == null)
            {
                return null;
            }

            if (!points.TryGet(pointId, out ServicePoint entrance))
            {
                throw new ArgumentException("Parking entrance '" + pointId + "' is not registered.", nameof(definition));
            }

            if (entrance.Definition.Kind != PointKind.Barrier)
            {
                throw new ArgumentException("Point '" + pointId + "' is not a barrier, it cannot be a parking entrance.", nameof(definition));
            }

            if (!string.Equals(entrance.Definition.LocationId, definition.LocationId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Parking entrance '" + pointId + "' belongs to location '" + entrance.Definition.LocationId
                    + "', not '" + definition.LocationId + "'.",
                    nameof(definition));
            }

            return entrance;
        }

        // Points built later join through AddServicePoint.
        private void CollectLocationPoints()
        {
            IReadOnlyList<ServicePoint> all = _points.All;
            for (int i = 0; i < all.Count; i++)
            {
                ServicePoint point = all[i];
                if (string.Equals(point.Definition.LocationId, _definition.LocationId, StringComparison.Ordinal))
                {
                    AddLocationPoint(point, _definition.ServiceBufferCapacity);
                }
            }
        }

        private void AddLocationPoint(ServicePoint point, int bufferCapacity)
        {
            ServicePointDefinition pointDefinition = point.Definition;
            _locationPoints.Add(point);
            point.ServiceCompleted += OnServiceCompleted;

            // Why: a car may only request a service that exists here; barriers are a means, not a requested service.
            if (pointDefinition.Kind != PointKind.Service)
            {
                return;
            }

            EntryQueue buffer = bufferCapacity > 0 ? new EntryQueue(bufferCapacity) : null;
            if (buffer != null)
            {
                buffer.Shifted += OnBufferShifted;
            }

            _servicePoints.Add(point);
            _buffers.Add(buffer);

            if (!_serviceTypeIds.Contains(pointDefinition.ServiceTypeId))
            {
                _serviceTypeIds.Add(pointDefinition.ServiceTypeId);
            }
        }

        private void TickPatience(float deltaTime)
        {
            for (int i = 0; i < _activeCars.Count; i++)
            {
                Car car = _activeCars[i];

                // Why: at a point or an entrance the wait ends when the order is accepted;
                // the service time itself costs no patience.
                if (car.State == CarState.AtPoint || car.State == CarState.AtEntrance)
                {
                    if (_points.TryGet(car.TargetPointId, out ServicePoint point) && point.State == ServicePointState.AwaitingAccept)
                    {
                        car.TickPatience(deltaTime);
                    }

                    continue;
                }

                // The car itself ignores states in which it is not waiting (driving, parked, leaving).
                car.TickPatience(deltaTime);
            }
        }

        private void TickParkingStays(float deltaTime)
        {
            for (int i = 0; i < _parkedCars.Count;)
            {
                Car car = _cars[_parkedCars[i]];
                car.TickParkingStay(deltaTime);
                if (!car.IsStayOver)
                {
                    i++;
                    continue;
                }

                // Why: the exit is automatic — the stay was paid for at the entrance, so the car simply drives off.
                _parkedCars.RemoveAt(i);
                _parking.Release(car.ParkingSlot);
                car.LeaveParking();
                _agents.MoveTo(car.Id, CarDestination.Exit());
            }
        }

        private void DispatchBufferHeads()
        {
            for (int i = 0; i < _buffers.Count; i++)
            {
                EntryQueue buffer = _buffers[i];
                if (buffer == null)
                {
                    continue;
                }

                int headId = buffer.Head;
                if (headId == EntryQueue.None)
                {
                    continue;
                }

                Car head = _cars[headId];
                ServicePoint point = _servicePoints[i];

                // Why: the head first has to stand at the front of the buffer, otherwise it would cut past the cars ahead.
                if (head.State != CarState.InBuffer || !head.HasArrived || !point.TryReserve(head.Id, PriceFor(point, head)))
                {
                    continue;
                }

                buffer.RemoveHead();
                SendToPoint(head, point);
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

            if (!head.WantsService)
            {
                if (TryReserveEntrance(head, _mainEntrance, out int slot, out float stay))
                {
                    _queue.RemoveHead();
                    SendToEntrance(head, _mainEntrance, slot, stay);
                }

                return;
            }

            if (TryDispatchHeadToService(head))
            {
                return;
            }

            // Otherwise every matching point is busy and its buffer full: the head waits at the fork.
        }

        private bool TryDispatchHeadToService(Car head)
        {
            string serviceTypeId = head.RequestedServiceTypeId;
            int bufferChoice = -1;
            for (int i = 0; i < _servicePoints.Count; i++)
            {
                ServicePoint point = _servicePoints[i];
                if (!string.Equals(point.Definition.ServiceTypeId, serviceTypeId, StringComparison.Ordinal))
                {
                    continue;
                }

                EntryQueue buffer = _buffers[i];
                bool bufferEmpty = buffer == null || buffer.Count == 0;

                // Why: straight to the point only if nobody waits in its buffer — the buffer was there first.
                if (bufferEmpty && point.TryReserve(head.Id, PriceFor(point, head)))
                {
                    _queue.RemoveHead();
                    SendToPoint(head, point);
                    return true;
                }

                if (buffer != null && !buffer.IsFull && (bufferChoice < 0 || buffer.Count < _buffers[bufferChoice].Count))
                {
                    bufferChoice = i;
                }
            }

            if (bufferChoice < 0)
            {
                return false;
            }

            string pointId = _servicePoints[bufferChoice].Definition.Id;
            _queue.RemoveHead();
            _buffers[bufferChoice].TryEnqueue(head.Id, out int bufferSlot);
            head.SendToBuffer(pointId);
            _agents.MoveTo(head.Id, CarDestination.BufferSlot(pointId, bufferSlot));
            return true;
        }

        /// <summary>
        /// Reserves <paramref name="entrance"/> and a parking slot together and rolls the stay the car will pay for.
        /// </summary>
        /// <returns>False (nothing reserved) when the barrier is busy or the lot is full.</returns>
        private bool TryReserveEntrance(Car car, ServicePoint entrance, out int slot, out float stay)
        {
            stay = 0f;

            if (entrance == null)
            {
                slot = ParkingLot.None;
                return false;
            }

            // Why: both are checked before anything is reserved, so a failure never leaves half a reservation behind.
            if (!entrance.IsAvailable || !_parking.TryReserve(car.Id, out slot))
            {
                slot = ParkingLot.None;
                return false;
            }

            // Why: the stay is rolled now and paid up front, so the fee shown at the barrier is final.
            stay = NextParkingStay();
            ServicePointDefinition barrier = entrance.Definition;
            // Why: the price upgrade multiplies the fee now, at the reservation — the shown fee is final, and an upgrade
            // bought later only affects new orders (nothing has to be recalculated).
            Money fee = PriceFormula.TimeBased(
                barrier.BasePrice, barrier.PricePerSecond, stay, car.Type.PriceMultiplier * entrance.PriceMultiplier);
            entrance.TryReserve(car.Id, fee);
            return true;
        }

        private void SendToEntrance(Car car, ServicePoint entrance, int slot, float stay)
        {
            string entranceId = entrance.Definition.Id;
            car.SendToEntrance(entranceId, slot, stay);
            _agents.MoveTo(car.Id, CarDestination.Entrance(entranceId));
        }

        private void SendToPoint(Car car, ServicePoint point)
        {
            string pointId = point.Definition.Id;
            car.SendToPoint(pointId);
            _agents.MoveTo(car.Id, CarDestination.Point(pointId));
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

            if (_serviceTypeIds.Count == 0 && (_mainEntrance == null || _definition.ParkingCapacity == 0))
            {
                return;
            }

            CarType carType = PickCarType();
            CarVisitPlan plan = PickPlan();
            string serviceTypeId = plan == CarVisitPlan.ParkOnly ? null : _serviceTypeIds[_random.Range(0, _serviceTypeIds.Count)];
            int carId = _nextCarId++;

            // Why: the only allocation of the flow — once per spawned car, every few seconds; never per frame.
            var car = new Car(carId, carType, plan, serviceTypeId);
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
            float interval = (float)(_traffic.SpawnInterval / _flowMultiplier);
            return Math.Max(MinSpawnInterval, interval + offset);
        }

        private CarVisitPlan PickPlan()
        {
            if (_mainEntrance == null || _definition.ParkingCapacity == 0)
            {
                return CarVisitPlan.WashOnly;
            }

            // Why: without service points the location still earns from parking, so cars keep coming as parking-only.
            if (_serviceTypeIds.Count == 0)
            {
                return CarVisitPlan.ParkOnly;
            }

            int roll = _random.Range(0, _totalPlanWeight);
            if (roll < _traffic.ParkOnlyWeight)
            {
                return CarVisitPlan.ParkOnly;
            }

            return roll < _traffic.ParkOnlyWeight + _traffic.ServiceOnlyWeight ? CarVisitPlan.WashOnly : CarVisitPlan.WashThenPark;
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

        // Why: the service price scales with the car type and the point's price upgrade, like the parking fee; it is fixed
        // at the reservation, so an upgrade only affects new orders.
        private static Money PriceFor(ServicePoint point, Car car) =>
            point.Definition.BasePrice * (car.Type.PriceMultiplier * point.PriceMultiplier);

        private int IndexOfServicePoint(string pointId)
        {
            for (int i = 0; i < _servicePoints.Count; i++)
            {
                if (string.Equals(_servicePoints[i].Definition.Id, pointId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
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

        private void OnBufferShifted(EntryQueue buffer)
        {
            int index = _buffers.IndexOf(buffer);
            string pointId = _servicePoints[index].Definition.Id;
            for (int slot = 0; slot < buffer.Count; slot++)
            {
                int carId = buffer.GetAt(slot);
                _cars[carId].MoveUpInBuffer();
                _agents.MoveTo(carId, CarDestination.BufferSlot(pointId, slot));
            }
        }

        private void OnCarArrived(int carId)
        {
            // Why: duplicate or stale reports (e.g. a fallback arrival after a newer one) must not advance the flow twice.
            if (!_cars.TryGetValue(carId, out Car car) || car.HasArrived)
            {
                return;
            }

            car.MarkArrived();
            switch (car.State)
            {
                case CarState.AtPoint:
                case CarState.AtEntrance:
                    if (_points.TryGet(car.TargetPointId, out ServicePoint point))
                    {
                        point.NotifyCarArrived(carId);
                    }

                    break;
                case CarState.Parked:
                    _parkedCars.Add(carId);
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
                // Paid at the entrance: on to the slot reserved together with the barrier.
                car.SendToParking();
                _agents.MoveTo(carId, CarDestination.ParkingSlot(car.ParkingSlot));
            }
            else
            {
                LeavePoint(car);
            }

            _eventBus.Publish(new ServiceCompletedEvent(
                pointDefinition.Id, pointDefinition.ServiceTypeId, pointDefinition.Kind, carId, car.Type.Id));
        }

        private void LeavePoint(Car car)
        {
            if (car.Plan == CarVisitPlan.WashThenPark)
            {
                if (TryReserveEntrance(car, _serviceEntrance, out int slot, out float stay))
                {
                    SendToEntrance(car, _serviceEntrance, slot, stay);
                    return;
                }

                // Why: the car cannot wait on the bay's exit lane — it would block the road for everybody behind it,
                // so with the service entrance busy or the lot full it gives up on parking and drives away.
                _eventBus.Publish(new ParkingRefusedEvent(car.Id, _definition.LocationId));
            }

            car.Leave();
            _agents.MoveTo(car.Id, CarDestination.Exit());
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
