using System;
using AutoService.Domain.Traffic;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="Car"/> and <see cref="CarType"/>.</summary>
    public sealed class CarTests
    {
        private const float Patience = 10f;
        private const string Wash = "wash_1";
        private const string Entrance = "entrance_1";

        private static readonly CarType Sedan = new CarType("sedan", 1, 1.0, Patience);

        [Test]
        public void CarType_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentException>(() => new CarType("", 1, 1.0, 1f));
            Assert.Throws<ArgumentException>(() => new CarType("a", 0, 1.0, 1f));
            Assert.Throws<ArgumentException>(() => new CarType("a", 1, 0.0, 1f));
            Assert.Throws<ArgumentException>(() => new CarType("a", 1, double.NaN, 1f));
            Assert.Throws<ArgumentException>(() => new CarType("a", 1, 1.0, 0f));
        }

        [Test]
        public void Constructor_ServiceTypeMustMatchPlan()
        {
            Assert.Throws<ArgumentException>(() => new Car(1, Sedan, CarVisitPlan.ParkOnly, "wash"));
            Assert.Throws<ArgumentException>(() => new Car(1, Sedan, CarVisitPlan.WashOnly, null));
            Assert.Throws<ArgumentException>(() => new Car(1, Sedan, CarVisitPlan.WashThenPark, " "));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Car(1, Sedan, (CarVisitPlan)42, "wash"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Car(-1, Sedan, CarVisitPlan.ParkOnly, null));
            Assert.Throws<ArgumentNullException>(() => new Car(1, null, CarVisitPlan.ParkOnly, null));
        }

        [Test]
        public void NewCar_IsArrivingWithFullPatience()
        {
            Car car = WashOnly();

            Assert.AreEqual(CarState.Arriving, car.State);
            Assert.AreEqual(CarVisitPlan.WashOnly, car.Plan);
            Assert.IsTrue(car.WantsService);
            Assert.AreEqual(Patience, car.PatienceLeft);
            Assert.AreEqual(1f, car.Patience01);
            Assert.AreEqual(Car.NoParkingSlot, car.ParkingSlot);
            Assert.IsNull(car.TargetPointId);
        }

        [Test]
        public void WashOnly_QueueToPointToExit()
        {
            Car car = WashOnly();
            car.EnterQueue();
            car.MarkArrived();
            Assert.IsTrue(car.HasArrived);

            car.SendToPoint(Wash);
            Assert.AreEqual(CarState.ToPoint, car.State);
            Assert.IsFalse(car.HasArrived);
            Assert.AreEqual(Wash, car.TargetPointId);

            car.MarkArrived();
            Assert.AreEqual(CarState.AtPoint, car.State);

            car.Leave();
            Assert.AreEqual(CarState.Leaving, car.State);
            Assert.IsNull(car.TargetPointId);
        }

        [Test]
        public void Buffer_QueueToBufferToPoint()
        {
            Car car = WashOnly();
            car.EnterQueue();

            car.SendToBuffer(Wash);
            Assert.AreEqual(CarState.ToBuffer, car.State);
            Assert.AreEqual(Wash, car.TargetPointId);

            car.MoveUpInBuffer();
            Assert.AreEqual(CarState.ToBuffer, car.State, "Moving up while still driving in keeps the state.");

            car.MarkArrived();
            Assert.AreEqual(CarState.InBuffer, car.State);

            car.MoveUpInBuffer();
            Assert.AreEqual(CarState.InBuffer, car.State);
            Assert.IsFalse(car.HasArrived);

            car.MarkArrived();
            car.SendToPoint(Wash);
            Assert.AreEqual(CarState.ToPoint, car.State);
        }

        [Test]
        public void ParkOnly_QueueToEntranceToParkingToExit()
        {
            var car = new Car(2, Sedan, CarVisitPlan.ParkOnly, null);
            car.EnterQueue();

            car.SendToEntrance(Entrance, 3, 5f);
            Assert.AreEqual(CarState.ToEntrance, car.State);
            Assert.AreEqual(Entrance, car.TargetPointId);
            Assert.AreEqual(3, car.ParkingSlot);
            Assert.AreEqual(5f, car.PlannedStay);

            car.MarkArrived();
            Assert.AreEqual(CarState.AtEntrance, car.State);

            car.SendToParking();
            Assert.AreEqual(CarState.ToParking, car.State);
            Assert.IsNull(car.TargetPointId);
            Assert.AreEqual(5f, car.ParkingStayLeft);

            car.MarkArrived();
            Assert.AreEqual(CarState.Parked, car.State);
            Assert.IsFalse(car.IsStayOver);

            car.TickParkingStay(5f);
            Assert.IsTrue(car.IsStayOver);

            car.LeaveParking();
            Assert.AreEqual(CarState.Leaving, car.State);
            Assert.AreEqual(Car.NoParkingSlot, car.ParkingSlot);
        }

        [Test]
        public void WashThenPark_PointToEntrance()
        {
            var car = new Car(3, Sedan, CarVisitPlan.WashThenPark, "wash");
            car.EnterQueue();
            car.SendToPoint(Wash);
            car.MarkArrived();

            car.SendToEntrance(Entrance, 0, 4f);

            Assert.AreEqual(CarState.ToEntrance, car.State);
            Assert.AreEqual(Entrance, car.TargetPointId);
        }

        [Test]
        public void SendToEntrance_MustMatchThePlan()
        {
            Car washOnly = WashOnly();
            washOnly.EnterQueue();
            Assert.Throws<InvalidOperationException>(() => washOnly.SendToEntrance(Entrance, 0, 1f), "A service car does not park from the queue.");
            washOnly.SendToPoint(Wash);
            washOnly.MarkArrived();
            Assert.Throws<InvalidOperationException>(() => washOnly.SendToEntrance(Entrance, 0, 1f), "Wash-only never parks.");

            var parkOnly = new Car(2, Sedan, CarVisitPlan.ParkOnly, null);
            parkOnly.EnterQueue();
            Assert.Throws<InvalidOperationException>(() => parkOnly.SendToPoint(Wash));
            Assert.Throws<InvalidOperationException>(() => parkOnly.SendToBuffer(Wash));
            Assert.Throws<ArgumentOutOfRangeException>(() => parkOnly.SendToEntrance(Entrance, -1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => parkOnly.SendToEntrance(Entrance, 0, float.NaN));
            Assert.Throws<ArgumentException>(() => parkOnly.SendToEntrance(" ", 0, 1f));
            Assert.AreEqual(CarState.InQueue, parkOnly.State);
        }

        [Test]
        public void ParkingStay_DoesNotRunWhileDrivingToTheSlot()
        {
            var car = new Car(2, Sedan, CarVisitPlan.ParkOnly, null);
            car.EnterQueue();
            car.SendToEntrance(Entrance, 0, 5f);
            car.MarkArrived();
            car.SendToParking();

            car.TickParkingStay(10f);

            Assert.AreEqual(5f, car.ParkingStayLeft);
        }

        [Test]
        public void MoveUpInQueue_ResetsArrival()
        {
            Car car = WashOnly();
            car.EnterQueue();
            car.MarkArrived();

            car.MoveUpInQueue();

            Assert.AreEqual(CarState.InQueue, car.State);
            Assert.IsFalse(car.HasArrived);
        }

        [Test]
        public void InvalidTransitions_Throw()
        {
            Car car = WashOnly();
            Assert.Throws<InvalidOperationException>(() => car.SendToPoint(Wash));
            Assert.Throws<InvalidOperationException>(() => car.SendToBuffer(Wash));
            Assert.Throws<InvalidOperationException>(() => car.Leave());
            Assert.Throws<InvalidOperationException>(() => car.SendToParking());
            Assert.Throws<InvalidOperationException>(() => car.LeaveParking());
            Assert.Throws<InvalidOperationException>(() => car.MoveUpInQueue());
            Assert.Throws<InvalidOperationException>(() => car.MoveUpInBuffer());

            car.EnterQueue();
            Assert.Throws<InvalidOperationException>(() => car.EnterQueue());
            Assert.Throws<ArgumentException>(() => car.SendToPoint(" "));

            car.SendToBuffer(Wash);
            Assert.Throws<InvalidOperationException>(() => car.SendToPoint(Wash), "Still driving into the buffer.");
        }

        [Test]
        public void Patience_DrainsOnlyWhileWaiting()
        {
            Car car = WashOnly();
            car.TickPatience(1f);
            Assert.AreEqual(Patience, car.PatienceLeft, "Arriving does not drain.");

            car.EnterQueue();
            car.TickPatience(1f);
            Assert.AreEqual(Patience - 1f, car.PatienceLeft, 0.0001f);

            car.SendToBuffer(Wash);
            car.TickPatience(1f);
            Assert.AreEqual(Patience - 1f, car.PatienceLeft, 0.0001f, "Driving does not drain.");

            car.MarkArrived();
            car.TickPatience(1f);
            Assert.AreEqual(Patience - 2f, car.PatienceLeft, 0.0001f, "Waiting in the buffer drains.");
        }

        [Test]
        public void Patience_DrainsAtTheEntranceButNotWhileParked()
        {
            var car = new Car(2, Sedan, CarVisitPlan.ParkOnly, null);
            car.EnterQueue();
            car.SendToEntrance(Entrance, 0, 5f);
            car.MarkArrived();

            car.TickPatience(1f);
            Assert.AreEqual(Patience - 1f, car.PatienceLeft, 0.0001f);

            car.SendToParking();
            car.MarkArrived();
            car.TickPatience(3f);
            Assert.AreEqual(Patience - 1f, car.PatienceLeft, 0.0001f, "The paid stay costs no patience.");
        }

        [Test]
        public void Patience_NeverBelowZeroAndDepletedRaisedOnce()
        {
            Car car = WashOnly();
            int depleted = 0;
            car.PatienceDepleted += c => depleted++;
            car.EnterQueue();

            car.TickPatience(Patience * 2f);
            car.TickPatience(1f);

            Assert.AreEqual(0f, car.PatienceLeft);
            Assert.AreEqual(0f, car.Patience01);
            Assert.AreEqual(1, depleted);
        }

        private static Car WashOnly() => new Car(1, Sedan, CarVisitPlan.WashOnly, "wash");
    }
}
