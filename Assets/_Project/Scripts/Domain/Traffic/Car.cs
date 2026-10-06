using System;

namespace AutoService.Domain.Traffic
{
    /// <summary>
    /// A customer car: its visit plan, patience, parking stay and position in the flow (<see cref="CarState"/>).
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
        /// <param name="plan">What the car came for.</param>
        /// <param name="requestedServiceTypeId">
        /// Id of the service the car wants: null for <see cref="CarVisitPlan.ParkOnly"/>, non-empty otherwise.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative id or an undefined plan.</exception>
        /// <exception cref="ArgumentNullException">Thrown for a null type.</exception>
        /// <exception cref="ArgumentException">Thrown when the service type id does not match the plan.</exception>
        public Car(int id, CarType type, CarVisitPlan plan, string requestedServiceTypeId)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Car id must be non-negative.");
            }

            if (plan != CarVisitPlan.ParkOnly && plan != CarVisitPlan.WashOnly && plan != CarVisitPlan.WashThenPark)
            {
                throw new ArgumentOutOfRangeException(nameof(plan), plan, "Unknown visit plan.");
            }

            bool hasService = requestedServiceTypeId != null && requestedServiceTypeId.Trim().Length > 0;
            if ((plan == CarVisitPlan.ParkOnly) != (requestedServiceTypeId == null) || (requestedServiceTypeId != null && !hasService))
            {
                throw new ArgumentException(
                    "A parking-only car has no service type id; any other plan needs a non-empty one.", nameof(requestedServiceTypeId));
            }

            Id = id;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Plan = plan;
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

        /// <summary>What the car came for.</summary>
        public CarVisitPlan Plan { get; }

        /// <summary>Id of the service the car wants, or null for a parking-only car.</summary>
        public string RequestedServiceTypeId { get; }

        /// <summary>True unless the car only wants to park.</summary>
        public bool WantsService => Plan != CarVisitPlan.ParkOnly;

        /// <summary>Current flow state.</summary>
        public CarState State { get; private set; }

        /// <summary>True once the car has reached its current destination; reset by every new destination.</summary>
        public bool HasArrived { get; private set; }

        /// <summary>Seconds of patience left (never below 0).</summary>
        public float PatienceLeft { get; private set; }

        /// <summary>Patience left as a fraction of the type's patience, 0..1.</summary>
        public float Patience01 => PatienceLeft / Type.Patience;

        /// <summary>
        /// Id of the point the car is heading for or stands at: its service point (also while in that point's buffer)
        /// or the entrance barrier; null otherwise.
        /// </summary>
        public string TargetPointId { get; private set; }

        /// <summary>Reserved parking slot index, or <see cref="NoParkingSlot"/>.</summary>
        public int ParkingSlot { get; private set; }

        /// <summary>Stay the car pays for at the entrance, in seconds (set by <see cref="SendToEntrance"/>).</summary>
        public float PlannedStay { get; private set; }

        /// <summary>Seconds of the stay still left while parked.</summary>
        public float ParkingStayLeft { get; private set; }

        /// <summary>True when the car is parked and its stay is over: it leaves.</summary>
        public bool IsStayOver => State == CarState.Parked && ParkingStayLeft <= 0f;

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

        /// <summary><see cref="CarState.InQueue"/> → <see cref="CarState.ToBuffer"/>: the head waits for its busy point in the point's buffer.</summary>
        /// <exception cref="ArgumentException">Thrown for an empty point id.</exception>
        /// <exception cref="InvalidOperationException">Thrown from other states or for a parking-only car.</exception>
        public void SendToBuffer(string pointId)
        {
            RequirePointId(pointId);
            RequireService(nameof(SendToBuffer));
            Require(CarState.InQueue, nameof(SendToBuffer));
            TargetPointId = pointId;
            GoTo(CarState.ToBuffer);
        }

        /// <summary>
        /// The buffer moved and the car drives one slot forward: keeps <see cref="CarState.ToBuffer"/>/<see cref="CarState.InBuffer"/>,
        /// <see cref="HasArrived"/> is reset.
        /// </summary>
        public void MoveUpInBuffer()
        {
            if (State != CarState.ToBuffer && State != CarState.InBuffer)
            {
                throw InvalidTransition(nameof(MoveUpInBuffer));
            }

            HasArrived = false;
        }

        /// <summary>
        /// <see cref="CarState.InQueue"/> or <see cref="CarState.InBuffer"/> → <see cref="CarState.ToPoint"/>: drives to the reserved point.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for an empty point id.</exception>
        /// <exception cref="InvalidOperationException">Thrown from other states or for a parking-only car.</exception>
        public void SendToPoint(string pointId)
        {
            RequirePointId(pointId);
            RequireService(nameof(SendToPoint));
            if (State != CarState.InQueue && State != CarState.InBuffer)
            {
                throw InvalidTransition(nameof(SendToPoint));
            }

            TargetPointId = pointId;
            GoTo(CarState.ToPoint);
        }

        /// <summary>
        /// Drives to a parking entrance with a reserved slot and the stay it will pay for:
        /// from <see cref="CarState.InQueue"/> (<see cref="CarVisitPlan.ParkOnly"/>, main entrance) or
        /// from <see cref="CarState.AtPoint"/> (<see cref="CarVisitPlan.WashThenPark"/>, service entrance) → <see cref="CarState.ToEntrance"/>.
        /// </summary>
        /// <param name="entrancePointId">Id of the reserved entrance barrier.</param>
        /// <param name="parkingSlot">Reserved slot index (&gt;= 0).</param>
        /// <param name="plannedStay">Seconds the car will stay parked (&gt;= 0).</param>
        /// <exception cref="ArgumentException">Thrown for an empty entrance id.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative slot or a negative/NaN stay.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the state does not match the plan.</exception>
        public void SendToEntrance(string entrancePointId, int parkingSlot, float plannedStay)
        {
            RequirePointId(entrancePointId);
            if (parkingSlot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(parkingSlot), parkingSlot, "Parking slot must be non-negative.");
            }

            // Why: the negated comparison also rejects NaN.
            if (!(plannedStay >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(plannedStay), plannedStay, "Planned stay must be non-negative.");
            }

            bool fromQueue = State == CarState.InQueue && Plan == CarVisitPlan.ParkOnly;
            bool fromPoint = State == CarState.AtPoint && Plan == CarVisitPlan.WashThenPark;
            if (!fromQueue && !fromPoint)
            {
                throw InvalidTransition(nameof(SendToEntrance));
            }

            TargetPointId = entrancePointId;
            ParkingSlot = parkingSlot;
            PlannedStay = plannedStay;
            GoTo(CarState.ToEntrance);
        }

        /// <summary>The fee is paid: <see cref="CarState.AtEntrance"/> → <see cref="CarState.ToParking"/> (drives to <see cref="ParkingSlot"/>).</summary>
        public void SendToParking()
        {
            Require(CarState.AtEntrance, nameof(SendToParking));
            TargetPointId = null;
            ParkingStayLeft = PlannedStay;
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
        /// <see cref="CarState.Parked"/> → <see cref="CarState.Leaving"/>: the car drives out of the lot to the exit.
        /// Clears <see cref="ParkingSlot"/>: the orchestrator releases the slot itself.
        /// </summary>
        /// <remarks>Does not require the stay to be over, so an impatient car could leave too (module 14).</remarks>
        public void LeaveParking()
        {
            Require(CarState.Parked, nameof(LeaveParking));
            ParkingSlot = NoParkingSlot;
            ParkingStayLeft = 0f;
            GoTo(CarState.Leaving);
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
        /// (<c>ToBuffer → InBuffer</c>, <c>ToPoint → AtPoint</c>, <c>ToEntrance → AtEntrance</c>, <c>ToParking → Parked</c>).
        /// Other states only set the flag.
        /// </summary>
        public void MarkArrived()
        {
            HasArrived = true;
            switch (State)
            {
                case CarState.ToBuffer:
                    State = CarState.InBuffer;
                    break;
                case CarState.ToPoint:
                    State = CarState.AtPoint;
                    break;
                case CarState.ToEntrance:
                    State = CarState.AtEntrance;
                    break;
                case CarState.ToParking:
                    State = CarState.Parked;
                    break;
            }
        }

        /// <summary>
        /// Drains patience while the car waits (<see cref="CarState.InQueue"/>, <see cref="CarState.InBuffer"/>,
        /// <see cref="CarState.AtPoint"/>, <see cref="CarState.AtEntrance"/>); ignored otherwise. Never below zero.
        /// </summary>
        /// <remarks>
        /// At a point or an entrance patience must only drain until the order is accepted; the car cannot see the point,
        /// so the orchestrator calls this for those two states only while the point awaits acceptance.
        /// The paid parking stay never costs patience.
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
                || State == CarState.InBuffer
                || State == CarState.AtPoint
                || State == CarState.AtEntrance;
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

        private void RequireService(string transition)
        {
            if (!WantsService)
            {
                throw InvalidTransition(transition);
            }
        }

        private static void RequirePointId(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId))
            {
                throw new ArgumentException("Point id must not be empty.", nameof(pointId));
            }
        }

        private InvalidOperationException InvalidTransition(string transition)
        {
            return new InvalidOperationException(
                "Car " + Id + " (" + Plan + ") cannot " + transition + " from state " + State + ".");
        }
    }
}
