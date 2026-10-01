using System;
using AutoService.Domain.Common;

namespace AutoService.Domain.Points
{
    /// <summary>
    /// A place where a car is served (a wash bay, the parking barrier...). Owns the order lifecycle
    /// (<see cref="ServicePointState"/>) and the work spot occupancy.
    /// </summary>
    /// <remarks>
    /// Core rule (GDD §4.2): both accepting the order and the service progress happen ONLY while the work spot is occupied.
    /// Leaving pauses the progress (it is not reset); leaving before acceptance restarts the accept delay.
    /// <para>The point never moves money itself: it raises <see cref="OrderAccepted"/> and the Services layer credits the wallet.</para>
    /// </remarks>
    public sealed class ServicePoint
    {
        /// <summary>Value of <see cref="CarId"/> when no car is assigned.</summary>
        public const int NoCar = -1;

        private float _acceptTimer;
        private float _clearTimer;

        /// <summary>Creates an idle, unoccupied point.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
        public ServicePoint(ServicePointDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CarId = NoCar;
        }

        /// <summary>Raised after every <see cref="State"/> change.</summary>
        public event Action<ServicePoint> StateChanged;

        /// <summary>Raised after every <see cref="Occupant"/> change.</summary>
        public event Action<ServicePoint> OccupantChanged;

        /// <summary>Raised when the order is accepted, with its price. The listener is responsible for crediting the money.</summary>
        public event Action<ServicePoint, Money> OrderAccepted;

        /// <summary>Raised when the service is finished, with the served car id. The point is already in <see cref="ServicePointState.Clearing"/>.</summary>
        public event Action<ServicePoint, int> ServiceCompleted;

        /// <summary>Immutable data of the point.</summary>
        public ServicePointDefinition Definition { get; }

        /// <summary>Current order state.</summary>
        public ServicePointState State { get; private set; }

        /// <summary>Who stands on the work spot.</summary>
        public OccupantKind Occupant { get; private set; }

        /// <summary>True while someone stands on the work spot.</summary>
        public bool IsOccupied => Occupant != OccupantKind.None;

        /// <summary>Id of the assigned car, or <see cref="NoCar"/>.</summary>
        public int CarId { get; private set; }

        /// <summary>Service progress of the current order, 0..1.</summary>
        public float Progress { get; private set; }

        /// <summary>Price of the current order (zero when idle).</summary>
        public Money CurrentPrice { get; private set; }

        /// <summary>True when a new car can be assigned (<see cref="ServicePointState.Idle"/>).</summary>
        public bool IsAvailable => State == ServicePointState.Idle;

        /// <summary>Puts <paramref name="occupant"/> on the work spot.</summary>
        /// <returns>True if the spot is now held by <paramref name="occupant"/> (also when it already was); false if someone else holds it.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="occupant"/> is <see cref="OccupantKind.None"/>.</exception>
        public bool TryOccupy(OccupantKind occupant)
        {
            if (occupant == OccupantKind.None)
            {
                throw new ArgumentException("Use Vacate to free the work spot.", nameof(occupant));
            }

            if (Occupant == occupant)
            {
                return true;
            }

            if (Occupant != OccupantKind.None)
            {
                return false;
            }

            Occupant = occupant;
            OccupantChanged?.Invoke(this);
            return true;
        }

        /// <summary>Frees the work spot if it is held by <paramref name="occupant"/>; otherwise does nothing.</summary>
        public void Vacate(OccupantKind occupant)
        {
            if (occupant == OccupantKind.None || Occupant != occupant)
            {
                return;
            }

            Occupant = OccupantKind.None;

            // Why: acceptance needs AcceptDelay seconds of uninterrupted presence; service progress, unlike it, is kept.
            _acceptTimer = 0f;
            OccupantChanged?.Invoke(this);
        }

        /// <summary>Assigns a car that is about to drive here. Only possible from <see cref="ServicePointState.Idle"/>.</summary>
        /// <param name="carId">Runtime id of the car (non-negative).</param>
        /// <param name="price">Price the car will pay when its order is accepted.</param>
        /// <returns>True if reserved; false if the point is not idle.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="carId"/> is negative.</exception>
        public bool TryReserve(int carId, Money price)
        {
            if (carId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(carId), carId, "Car id must be non-negative.");
            }

            if (State != ServicePointState.Idle)
            {
                return false;
            }

            CarId = carId;
            CurrentPrice = price;
            Progress = 0f;
            _acceptTimer = 0f;
            SetState(ServicePointState.Reserved);
            return true;
        }

        /// <summary>The reserved car has arrived on the spot: <see cref="ServicePointState.Reserved"/> → <see cref="ServicePointState.AwaitingAccept"/>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the point is not reserved for <paramref name="carId"/>.</exception>
        public void NotifyCarArrived(int carId)
        {
            if (State != ServicePointState.Reserved || CarId != carId)
            {
                throw new InvalidOperationException(
                    "Point '" + Definition.Id + "' is " + State + " for car " + CarId + "; car " + carId + " cannot arrive.");
            }

            _acceptTimer = 0f;
            SetState(ServicePointState.AwaitingAccept);
        }

        /// <summary>Advances acceptance, service progress and clearing. At most one state transition per call.</summary>
        /// <param name="deltaTime">Scaled frame time in seconds.</param>
        public void Tick(float deltaTime)
        {
            // Why: dt is 0 while paused; without this guard zero-length delays would still complete during the pause.
            if (deltaTime <= 0f)
            {
                return;
            }

            switch (State)
            {
                case ServicePointState.AwaitingAccept:
                    TickAccept(deltaTime);
                    break;
                case ServicePointState.Servicing:
                    TickService(deltaTime);
                    break;
                case ServicePointState.Clearing:
                    TickClearing(deltaTime);
                    break;
            }
        }

        private void TickAccept(float deltaTime)
        {
            if (!IsOccupied)
            {
                return;
            }

            _acceptTimer += deltaTime;
            if (_acceptTimer < Definition.AcceptDelay)
            {
                return;
            }

            _acceptTimer = 0f;
            SetState(ServicePointState.Servicing);
            OrderAccepted?.Invoke(this, CurrentPrice);
        }

        private void TickService(float deltaTime)
        {
            if (!IsOccupied)
            {
                return;
            }

            // Why: a zero duration is a valid "instant" service; dividing by it would produce NaN/Infinity.
            float step = Definition.ServiceDuration > 0f ? deltaTime / Definition.ServiceDuration : 1f;
            Progress = Math.Min(1f, Progress + step);
            if (Progress < 1f)
            {
                return;
            }

            int servedCar = CarId;
            CarId = NoCar;
            _clearTimer = 0f;
            SetState(ServicePointState.Clearing);
            ServiceCompleted?.Invoke(this, servedCar);
        }

        private void TickClearing(float deltaTime)
        {
            // Why: clearing does not need the occupant — it is just the time the served car needs to drive off the spot.
            _clearTimer += deltaTime;
            if (_clearTimer < Definition.ClearDelay)
            {
                return;
            }

            Progress = 0f;
            CurrentPrice = Money.Zero;
            SetState(ServicePointState.Idle);
        }

        private void SetState(ServicePointState state)
        {
            State = state;
            StateChanged?.Invoke(this);
        }
    }
}
