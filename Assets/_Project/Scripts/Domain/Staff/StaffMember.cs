using System;
using AutoService.Domain.Supplies;

namespace AutoService.Domain.Staff
{
    /// <summary>
    /// A hired NPC: its role, where it belongs and its job FSM (<see cref="StaffState"/>). Knows nothing about walking —
    /// the Services layer moves the body and calls the transition methods when it arrives.
    /// </summary>
    /// <remarks>Every transition checks the current state and throws on an invalid one, so a broken flow fails loudly.</remarks>
    public sealed class StaffMember
    {
        /// <summary>Creates a worker of a point; it starts walking to the point's work spot.</summary>
        /// <exception cref="ArgumentException">Thrown for an empty location or point id.</exception>
        public static StaffMember CreateWorker(int id, string locationId, string pointId)
        {
            RequireId(pointId, nameof(pointId));
            return new StaffMember(id, StaffRole.PointWorker, locationId, pointId, StaffState.WalkingToSpot);
        }

        /// <summary>Creates a storekeeper of a location; it starts walking from the staff room to its waiting spot.</summary>
        /// <param name="id">Runtime id.</param>
        /// <param name="locationId">Location it works in.</param>
        /// <param name="homeIndex">Index of its waiting spot at the warehouse (= hiring order in the location, &gt;= 0).</param>
        /// <exception cref="ArgumentException">Thrown for an empty location id.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative spot index.</exception>
        public static StaffMember CreateStorekeeper(int id, string locationId, int homeIndex)
        {
            if (homeIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(homeIndex), homeIndex, "Home spot index must be non-negative.");
            }

            return new StaffMember(id, StaffRole.Storekeeper, locationId, null, StaffState.ReturningHome) { HomeIndex = homeIndex };
        }

        private StaffMember(int id, StaffRole role, string locationId, string assignedPointId, StaffState state)
        {
            RequireId(locationId, nameof(locationId));
            Id = id;
            Role = role;
            LocationId = locationId;
            AssignedPointId = assignedPointId;
            State = state;
            CarriedBox = SupplyBox.None;
        }

        /// <summary>Runtime id (also the id of its body in the staff agents).</summary>
        public int Id { get; }

        /// <summary>Job.</summary>
        public StaffRole Role { get; }

        /// <summary>Location the NPC works in.</summary>
        public string LocationId { get; }

        /// <summary>Point of a worker; null for the storekeeper.</summary>
        public string AssignedPointId { get; }

        /// <summary>Current job state.</summary>
        public StaffState State { get; private set; }

        /// <summary>Box the storekeeper carries, or <see cref="SupplyBox.None"/>.</summary>
        public SupplyBox CarriedBox { get; private set; }

        /// <summary>Point the storekeeper restocks (from the decision to go to the warehouse until the delivery); null otherwise.</summary>
        public string TargetPointId { get; private set; }

        /// <summary>Index of the storekeeper's waiting spot at the warehouse; 0 for workers.</summary>
        public int HomeIndex { get; private set; }

        /// <summary>Worker reached the work spot: <see cref="StaffState.WalkingToSpot"/> → Working, or WaitingForSpot if it is taken.</summary>
        /// <param name="tookSpot">True when the work spot could be occupied.</param>
        /// <exception cref="InvalidOperationException">Thrown when not walking to the spot.</exception>
        public void ArriveAtSpot(bool tookSpot)
        {
            Require(StaffState.WalkingToSpot);
            State = tookSpot ? StaffState.Working : StaffState.WaitingForSpot;
        }

        /// <summary>The player has left the spot: <see cref="StaffState.WaitingForSpot"/> → <see cref="StaffState.Working"/>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when not waiting for the spot.</exception>
        public void TakeSpot()
        {
            Require(StaffState.WaitingForSpot);
            State = StaffState.Working;
        }

        /// <summary>
        /// A point needs restocking: <see cref="StaffState.Idle"/> or <see cref="StaffState.ReturningHome"/> →
        /// <see cref="StaffState.ToWarehouse"/>, with the point it will buy the box for.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for an empty target.</exception>
        /// <exception cref="InvalidOperationException">Thrown in any other state.</exception>
        public void GoToWarehouse(string targetPointId)
        {
            if (State != StaffState.Idle && State != StaffState.ReturningHome)
            {
                throw InvalidTransition(nameof(GoToWarehouse));
            }

            RequireId(targetPointId, nameof(targetPointId));
            TargetPointId = targetPointId;
            State = StaffState.ToWarehouse;
        }

        /// <summary>
        /// At the warehouse the target changed (the old one was restocked meanwhile): stays in
        /// <see cref="StaffState.ToWarehouse"/> / <see cref="StaffState.WaitingForMoney"/> with the new target.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for an empty target.</exception>
        /// <exception cref="InvalidOperationException">Thrown in any other state.</exception>
        public void Retarget(string targetPointId)
        {
            if (State != StaffState.ToWarehouse && State != StaffState.WaitingForMoney)
            {
                throw InvalidTransition(nameof(Retarget));
            }

            RequireId(targetPointId, nameof(targetPointId));
            TargetPointId = targetPointId;
        }

        /// <summary>Reached the waiting spot: <see cref="StaffState.ReturningHome"/> → <see cref="StaffState.Idle"/>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when not returning home.</exception>
        public void ArriveHome()
        {
            Require(StaffState.ReturningHome);
            State = StaffState.Idle;
        }

        /// <summary>At the warehouse without money: <see cref="StaffState.ToWarehouse"/> → <see cref="StaffState.WaitingForMoney"/>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when not walking to the warehouse.</exception>
        public void WaitForMoney()
        {
            Require(StaffState.ToWarehouse);
            State = StaffState.WaitingForMoney;
        }

        /// <summary>
        /// Nothing to restock any more (the player did it): <see cref="StaffState.ToWarehouse"/> or
        /// <see cref="StaffState.WaitingForMoney"/> → <see cref="StaffState.ReturningHome"/>, without a target.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown in any other state.</exception>
        public void CancelRestock()
        {
            if (State != StaffState.ToWarehouse && State != StaffState.WaitingForMoney)
            {
                throw InvalidTransition(nameof(CancelRestock));
            }

            TargetPointId = null;
            State = StaffState.ReturningHome;
        }

        /// <summary>Bought a box: <see cref="StaffState.ToWarehouse"/> or <see cref="StaffState.WaitingForMoney"/> → <see cref="StaffState.ToPoint"/>.</summary>
        /// <param name="box">The box (not <see cref="SupplyBox.None"/>).</param>
        /// <param name="targetPointId">Point the box goes to.</param>
        /// <exception cref="ArgumentException">Thrown for no box or an empty target.</exception>
        /// <exception cref="InvalidOperationException">Thrown in any other state.</exception>
        public void PickUp(SupplyBox box, string targetPointId)
        {
            if (State != StaffState.ToWarehouse && State != StaffState.WaitingForMoney)
            {
                throw InvalidTransition(nameof(PickUp));
            }

            if (box.IsNone)
            {
                throw new ArgumentException("The storekeeper must pick up a real box.", nameof(box));
            }

            RequireId(targetPointId, nameof(targetPointId));
            CarriedBox = box;
            TargetPointId = targetPointId;
            State = StaffState.ToPoint;
        }

        /// <summary>The target filled up meanwhile; the box goes to <paramref name="targetPointId"/> instead (stays <see cref="StaffState.ToPoint"/>).</summary>
        /// <exception cref="ArgumentException">Thrown for an empty target.</exception>
        /// <exception cref="InvalidOperationException">Thrown when not carrying a box.</exception>
        public void Redirect(string targetPointId)
        {
            Require(StaffState.ToPoint);
            RequireId(targetPointId, nameof(targetPointId));
            TargetPointId = targetPointId;
        }

        /// <summary>The box is delivered (or dropped): <see cref="StaffState.ToPoint"/> → <see cref="StaffState.ReturningHome"/>, hands empty.</summary>
        /// <returns>The box that was carried.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not carrying a box.</exception>
        public SupplyBox ReleaseBox()
        {
            Require(StaffState.ToPoint);
            SupplyBox box = CarriedBox;
            CarriedBox = SupplyBox.None;
            TargetPointId = null;
            State = StaffState.ReturningHome;
            return box;
        }

        private void Require(StaffState expected)
        {
            if (State != expected)
            {
                throw new InvalidOperationException(
                    Role + " " + Id + " is " + State + ", expected " + expected + ".");
            }
        }

        private InvalidOperationException InvalidTransition(string transition)
        {
            return new InvalidOperationException(Role + " " + Id + " cannot " + transition + " while " + State + ".");
        }

        private static void RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Id must not be empty.", parameterName);
            }
        }
    }
}
