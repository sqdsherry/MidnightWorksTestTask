using System;

namespace AutoService.Domain.Traffic
{
    /// <summary>
    /// A customer car: its request, patience and position in the flow (<see cref="CarState"/>).
    /// </summary>
    /// <remarks>
    /// The car does not know about NavMesh or points: the orchestrator tells it where it is sent and reports arrivals.
    /// Every transition validates the source state and throws <see cref="InvalidOperationException"/> otherwise,
    /// so an orchestration bug fails loudly instead of corrupting the flow.
    /// </remarks>
    public sealed class Car
    {
        /// <summary>Value of <see cref="ParkingSlot"/> when the car holds no parking slot.</summary>
        public const int NoParkingSlot = -1;

        private bool _patienceDepletedRaised;

        /// <summary>Creates a car in <see cref="CarState.Arriving"/> with full patience.</summary>
        /// <param name="id">Runtime id (non-negative).</param>
        /// <param name="type">Car type.</param>
        /// <param name="requestedServiceTypeId">Id of the service the car wants.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative id.</exception>
        /// <exception cref="ArgumentNullException">Thrown for a null type.</exception>
        /// <exception cref="ArgumentException">Thrown for an empty service type id.</exception>
        public Car(int id, CarType type, string requestedServiceTypeId)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Car id must be non-negative.");
            }

            if (string.IsNullOrWhiteSpace(requestedServiceTypeId))
            {
                throw new ArgumentException("Requested service type id must not be empty.", nameof(requestedServiceTypeId));
            }

            Id = id;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            RequestedServiceTypeId = requestedServiceTypeId;
            State = CarState.Arriving;
            PatienceLeft = type.Patience;
            ParkingSlot = NoParkingSlot;
        }

        /// <summary>Raised once, when <see cref="PatienceLeft"/> reaches zero. Reaction (leaving angry) is module 14.</summary>
        public event Action<Car> PatienceDepleted;

        /// <summary>Runtime id.</summary>
        public int Id { get; }

        /// <summary>Car type.</summary>
        public CarType Type { get; }

        /// <summary>Id of the service the car wants.</summary>
        public string RequestedServiceTypeId { get; }

        /// <summary>Current flow state.</summary>
        public CarState State { get; private set; }

        /// <summary>True once the car has reached its current destination; reset by every new destination.</summary>
        public bool HasArrived { get; private set; }

        /// <summary>Seconds of patience left (never below 0).</summary>
        public float PatienceLeft { get; private set; }

        /// <summary>Patience left as a fraction of the type's patience, 0..1.</summary>
        public float Patience01 => PatienceLeft / Type.Patience;

        /// <summary>Reserved parking slot index, or <see cref="NoParkingSlot"/>.</summary>
        public int ParkingSlot { get; private set; }

        /// <summary>Id of the point the car is sent to or stands at, or null.</summary>
        public string TargetPointId { get; private set; }

        /// <summary>Game time when the car parked; parked cars are dispatched first-in first-out by it.</summary>
        public float ParkedAtTime { get; private set; }

        /// <summary><see cref="CarState.Arriving"/> → <see cref="CarState.InQueue"/>; the car drives to its queue slot.</summary>
        public void EnterQueue()
        {
            Require(CarState.Arriving, nameof(EnterQueue));
            GoTo(CarState.InQueue);
        }

        /// <summary>
        /// The queue moved and the car drives one slot forward: stays <see cref="CarState.InQueue"/>, <see cref="HasArrived"/> is reset.
        /// </summary>
        public void MoveUpInQueue()
        {
            Require(CarState.InQueue, nameof(MoveUpInQueue));
            HasArrived = false;
        }

        /// <summary><see cref="CarState.InQueue"/> → <see cref="CarState.ToBarrier"/> with a reserved parking slot.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative slot.</exception>
        public void SendToBarrier(int parkingSlot)
        {
            if (parkingSlot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(parkingSlot), parkingSlot, "Parking slot must be non-negative.");
            }

            Require(CarState.InQueue, nameof(SendToBarrier));
            ParkingSlot = parkingSlot;
            GoTo(CarState.ToBarrier);
        }

        /// <summary><see cref="CarState.AtBarrier"/> → <see cref="CarState.ToParking"/>; the car drives to <see cref="ParkingSlot"/>.</summary>
        public void SendToParking()
        {
            Require(CarState.AtBarrier, nameof(SendToParking));
            GoTo(CarState.ToParking);
        }

        /// <summary>
        /// <see cref="CarState.InQueue"/> or <see cref="CarState.Parked"/> → <see cref="CarState.ToPoint"/>.
        /// Clears <see cref="ParkingSlot"/>: the orchestrator releases the slot itself.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for an empty point id.</exception>
        public void SendToPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
            {
                throw new ArgumentException("Point id must not be empty.", nameof(pointId));
            }

            if (State != CarState.InQueue && State != CarState.Parked)
            {
                throw InvalidTransition(nameof(SendToPoint));
            }

            TargetPointId = pointId;
            ParkingSlot = NoParkingSlot;
            GoTo(CarState.ToPoint);
        }

        /// <summary><see cref="CarState.AtPoint"/> → <see cref="CarState.Leaving"/>; the car drives to the exit.</summary>
        public void Leave()
        {
            Require(CarState.AtPoint, nameof(Leave));
            TargetPointId = null;
            GoTo(CarState.Leaving);
        }

        /// <summary>
        /// The car reached its current destination: sets <see cref="HasArrived"/> and completes a drive
        /// (<c>ToBarrier → AtBarrier</c>, <c>ToParking → Parked</c>, <c>ToPoint → AtPoint</c>). Other states only set the flag.
        /// </summary>
        /// <param name="time">Current game time, stored as <see cref="ParkedAtTime"/> when the car parks.</param>
        public void MarkArrived(float time)
        {
            HasArrived = true;
            switch (State)
            {
                case CarState.ToBarrier:
                    State = CarState.AtBarrier;
                    break;
                case CarState.ToParking:
                    State = CarState.Parked;
                    ParkedAtTime = time;
                    break;
                case CarState.ToPoint:
                    State = CarState.AtPoint;
                    break;
            }
        }

        /// <summary>
        /// Drains patience while the car waits (<see cref="CarState.InQueue"/>, <see cref="CarState.AtBarrier"/>,
        /// <see cref="CarState.Parked"/>, <see cref="CarState.AtPoint"/>); ignored in other states. Never goes below zero.
        /// </summary>
        /// <remarks>
        /// At a point patience must only drain until the service starts; the car cannot see the point,
        /// so the orchestrator calls this for <see cref="CarState.AtPoint"/> only while the point awaits acceptance.
        /// </remarks>
        public void TickPatience(float deltaTime)
        {
            if (deltaTime <= 0f || !IsWaiting())
            {
                return;
            }

            PatienceLeft = Math.Max(0f, PatienceLeft - deltaTime);
            if (PatienceLeft > 0f || _patienceDepletedRaised)
            {
                return;
            }

            _patienceDepletedRaised = true;
            PatienceDepleted?.Invoke(this);
        }

        private bool IsWaiting()
        {
            return State == CarState.InQueue
                || State == CarState.AtBarrier
                || State == CarState.Parked
                || State == CarState.AtPoint;
        }

        private void GoTo(CarState state)
        {
            State = state;
            HasArrived = false;
        }

        private void Require(CarState expected, string transition)
        {
            if (State != expected)
            {
                throw InvalidTransition(transition);
            }
        }

        private InvalidOperationException InvalidTransition(string transition)
        {
            return new InvalidOperationException("Car " + Id + " cannot " + transition + " from state " + State + ".");
        }
    }
}
