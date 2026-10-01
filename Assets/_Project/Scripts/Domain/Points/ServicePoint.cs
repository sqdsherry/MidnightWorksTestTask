using System;
using AutoService.Domain.Common;
using AutoService.Domain.Supplies;

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
    /// <para>A point with a <see cref="Supply"/> needs one unit per order: while the stock is empty the car waits on the spot
    /// and the order is not accepted. Upgrades scale the service time and the price of new orders (<see cref="ApplyModifiers"/>).</para>
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
            Supply = definition.HasSupply ? new SupplyStock(definition.SupplyTypeId, definition.SupplyCapacity) : null;
            DurationMultiplier = 1f;
            PriceMultiplier = 1.0;
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

        /// <summary>Consumable stock (created full), or null when the point needs none (the barriers).</summary>
        public SupplyStock Supply { get; }

        /// <summary>True while a car waits for its order to be accepted, but the stock is empty (needs a box, not a worker).</summary>
        public bool IsWaitingForSupply => State == ServicePointState.AwaitingAccept && Supply != null && Supply.IsEmpty;

        /// <summary>Multiplier of <see cref="ServicePointDefinition.ServiceDuration"/> (speed upgrade); 1 by default.</summary>
        public float DurationMultiplier { get; private set; }

        /// <summary>Multiplier of the price of new orders (price upgrade); 1 by default. Applied by the traffic when it reserves.</summary>
        public double PriceMultiplier { get; private set; }

        /// <summary>Sets the upgrade multipliers. The running order keeps its price; its remaining time uses the new speed.</summary>
        /// <param name="durationMultiplier">Service time multiplier (finite, &gt; 0).</param>
        /// <param name="priceMultiplier">Price multiplier of new orders (finite, &gt; 0).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a non-positive, NaN or infinite multiplier.</exception>
        public void ApplyModifiers(float durationMultiplier, double priceMultiplier)
        {
            // Why: the negated comparisons also reject NaN.
            if (!(durationMultiplier > 0f) || float.IsInfinity(durationMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(durationMultiplier), durationMultiplier, "Duration multiplier must be finite and positive.");
            }

            if (!(priceMultiplier > 0.0) || double.IsInfinity(priceMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(priceMultiplier), priceMultiplier, "Price multiplier must be finite and positive.");
            }

            DurationMultiplier = durationMultiplier;
            PriceMultiplier = priceMultiplier;
        }

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

        /// <summary>
        /// Undoes <see cref="TryReserve"/> before the car has arrived: <see cref="ServicePointState.Reserved"/> → <see cref="ServicePointState.Idle"/>.
        /// Used to roll back a multi-point reservation when a later part of it fails.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the point is not reserved for <paramref name="carId"/>.</exception>
        public void CancelReservation(int carId)
        {
            if (State != ServicePointState.Reserved || CarId != carId)
            {
                throw new InvalidOperationException(
                    "Point '" + Definition.Id + "' is " + State + " for car " + CarId + "; car " + carId + " cannot cancel.");
            }

            CarId = NoCar;
            CurrentPrice = Money.Zero;
            SetState(ServicePointState.Idle);
        }

        /// <summary>The reserved car has arrived on the spot:<see cref="ServicePointState.Reserved"/> → <see cref="ServicePointState.AwaitingAccept"/>.</summary>
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
            // Why: without a unit there is nothing to sell — the car waits and the accept timer does not run,
            // so a delivered box does not instantly accept an order nobody was "taking".
            if (!IsOccupied || (Supply != null && Supply.IsEmpty))
            {
                return;
            }

            _acceptTimer += deltaTime;
            if (_acceptTimer < Definition.AcceptDelay)
            {
                return;
            }

            _acceptTimer = 0f;
            Supply?.TryConsume();
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
            float duration = Definition.ServiceDuration * DurationMultiplier;
            float step = duration > 0f ? deltaTime / duration : 1f;
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
