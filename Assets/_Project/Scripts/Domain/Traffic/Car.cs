using System;

namespace AutoService.Domain.Traffic
{
    /// <summary>
    /// A customer car: its plan (a service, or parking only), patience, parking stay and position in the flow
    /// (<see cref="CarState"/>).
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
        /// <param name="requestedServiceTypeId">Id of the service the car wants, or null for a parking-only car.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative id.</exception>
        /// <exception cref="ArgumentNullException">Thrown for a null type.</exception>
        /// <exception cref="ArgumentException">Thrown for an empty (but non-null) service type id.</exception>
        public Car(int id, CarType type, string requestedServiceTypeId)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Car id must be non-negative.");
            }

            // Why: null is the explicit "parking only" plan; an empty string is a config bug, not a plan.
            if (requestedServiceTypeId != null && requestedServiceTypeId.Trim().Length == 0)
            {
                throw new ArgumentException(
                    "Requested service type id must be null (parking only) or non-empty.", nameof(requestedServiceTypeId));
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

        /// <summary>Id of the service the car wants, or null for a parking-only car.</summary>
        public string RequestedServiceTypeId { get; }

        /// <summary>False for a parking-only car: it parks for a while and leaves, never visiting a service point.</summary>
        public bool WantsService => RequestedServiceTypeId != null;

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

        /// <summary>Game time when the car parked.</summary>
        public float ParkedAtTime { get; private set; }

        /// <summary>Seconds the car still wants to stay parked (set by <see cref="SendToParking"/>, drained while parked).</summary>
        public float ParkingStayLeft { get; private set; }

        /// <summary>True when the car is parked and its stay is over: it may now go to a point or leave.</summary>
        public bool IsReadyToLeaveParking => State == CarState.Parked && ParkingStayLeft <= 0f;

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
        /// <param name="parkingStay">Seconds the car will stay parked once there (&gt;= 0).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative or NaN stay.</exception>
        public void SendToParking(float parkingStay)
        {
            if (!(parkingStay >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(parkingStay), parkingStay, "Parking stay must be non-negative.");
            }

            Require(CarState.AtBarrier, nameof(SendToParking));
            ParkingStayLeft = parkingStay;
            GoTo(CarState.ToParking);
        }

        /// <summary>Drains the parking stay while <see cref="CarState.Parked"/>; ignored in other states. Never goes below zero.</summary>
        public void TickParkingStay(float deltaTime)
        {
            if (deltaTime <= 0f || State != CarState.Parked || ParkingStayLeft <= 0f)
            {
                return;
            }

            ParkingStayLeft = Math.Max(0f, ParkingStayLeft - deltaTime);
        }

        /// <summary>
        /// <see cref="CarState.Parked"/> → <see cref="CarState.Leaving"/>: the car drives from its slot to the exit.
        /// Clears <see cref="ParkingSlot"/>: the orchestrator releases the slot itself.
        /// </summary>
        /// <remarks>Does not require the stay to be over, so an impatient car can leave too (module 14).</remarks>
        public void LeaveParking()
        {
            Require(CarState.Parked, nameof(LeaveParking));
            ParkingSlot = NoParkingSlot;
            ParkingStayLeft = 0f;
            GoTo(CarState.Leaving);
        }

        /// <summary>
        /// <see cref="CarState.InQueue"/> or <see cref="CarState.Parked"/> (stay over) → <see cref="CarState.ToPoint"/>.
        /// Clears <see cref="ParkingSlot"/>: the orchestrator releases the slot itself.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for an empty point id.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown from other states, for a parking-only car, or for a parked car whose stay is not over.
        /// </exception>
        public void SendToPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
            {
                throw new ArgumentException("Point id must not be empty.", nameof(pointId));
            }

            bool fromQueue = State == CarState.InQueue;
            if (!WantsService || (!fromQueue && !IsReadyToLeaveParking))
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
        /// <see cref="CarState.Parked"/> after its stay, <see cref="CarState.AtPoint"/>); ignored otherwise. Never below zero.
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

        // Why: the parking stay is time the customer wanted to spend parked, not waiting — it costs no patience.
        private bool IsWaiting()
        {
            return State == CarState.InQueue
                || State == CarState.AtBarrier
                || (State == CarState.Parked && ParkingStayLeft <= 0f)
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
