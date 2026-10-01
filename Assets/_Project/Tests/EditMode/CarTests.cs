using System;
using AutoService.Domain.Traffic;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="Car"/> and <see cref="CarType"/>.</summary>
    public sealed class CarTests
    {
        private const float Patience = 10f;

        private Car _car;

        [SetUp]
        public void SetUp()
        {
            _car = new Car(1, new CarType("sedan", 1, 1.0, Patience), "wash");
        }

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
        public void NewCar_IsArrivingWithFullPatience()
        {
            Assert.AreEqual(CarState.Arriving, _car.State);
            Assert.AreEqual(Patience, _car.PatienceLeft);
            Assert.AreEqual(1f, _car.Patience01);
            Assert.AreEqual(Car.NoParkingSlot, _car.ParkingSlot);
            Assert.IsNull(_car.TargetPointId);
            Assert.IsNull(_car.NextPointId);
        }

        [Test]
        public void DirectRoute_QueueToPointToExit()
        {
            _car.EnterQueue();
            _car.MarkArrived(0f);
            Assert.IsTrue(_car.HasArrived);

            _car.SendToPoint("wash_1");
            Assert.AreEqual(CarState.ToPoint, _car.State);
            Assert.IsFalse(_car.HasArrived);
            Assert.AreEqual("wash_1", _car.TargetPointId);

            _car.MarkArrived(1f);
            Assert.AreEqual(CarState.AtPoint, _car.State);

            _car.Leave();
            Assert.AreEqual(CarState.Leaving, _car.State);
            Assert.IsNull(_car.TargetPointId);
        }

        [Test]
        public void SendToParking_GoesFromQueueStraightToTheSlot()
        {
            _car.EnterQueue();

            _car.SendToParking(2, 5f);

            Assert.AreEqual(CarState.ToParking, _car.State);
            Assert.AreEqual(2, _car.ParkingSlot);
            Assert.AreEqual(5f, _car.ParkingStayLeft);
            Assert.IsFalse(_car.HasArrived);

            _car.MarkArrived(7f);
            Assert.AreEqual(CarState.Parked, _car.State);
            Assert.AreEqual(7f, _car.ParkedAtTime);
        }

        [Test]
        public void ParkingRoute_ExitThenPoint()
        {
            ParkWithStay(0f);

            _car.SendToParkingExit("wash_1");
            Assert.AreEqual(CarState.ToParkingExit, _car.State);
            Assert.AreEqual(Car.NoParkingSlot, _car.ParkingSlot);
            Assert.AreEqual("wash_1", _car.NextPointId);

            _car.MarkArrived(2f);
            Assert.AreEqual(CarState.AtParkingExit, _car.State);

            _car.ContinueFromParkingExit();
            Assert.AreEqual(CarState.ToPoint, _car.State);
            Assert.AreEqual("wash_1", _car.TargetPointId);
            Assert.IsNull(_car.NextPointId);
            Assert.IsFalse(_car.HasArrived);
        }

        [Test]
        public void ParkingOnlyRoute_ExitThenLeave()
        {
            var parkOnly = new Car(2, new CarType("sedan", 1, 1.0, Patience), null);
            parkOnly.EnterQueue();
            parkOnly.SendToParking(0, 0f);
            parkOnly.MarkArrived(1f);

            parkOnly.SendToParkingExit(null);
            parkOnly.MarkArrived(2f);
            parkOnly.ContinueFromParkingExit();

            Assert.AreEqual(CarState.Leaving, parkOnly.State);
            Assert.IsNull(parkOnly.TargetPointId);
        }

        [Test]
        public void ParkingOnlyCar_HasNoServiceAndCannotGoToPoint()
        {
            var parkOnly = new Car(2, new CarType("sedan", 1, 1.0, Patience), null);
            parkOnly.EnterQueue();

            Assert.IsFalse(parkOnly.WantsService);
            Assert.IsTrue(_car.WantsService);
            Assert.Throws<InvalidOperationException>(() => parkOnly.SendToPoint("wash_1"));
            Assert.Throws<ArgumentException>(() => new Car(3, new CarType("sedan", 1, 1.0, Patience), " "));

            parkOnly.SendToParking(0, 0f);
            parkOnly.MarkArrived(0f);
            Assert.Throws<InvalidOperationException>(() => parkOnly.SendToParkingExit("wash_1"));
        }

        [Test]
        public void SendToParkingExit_OnlyWhenTheStayIsOver()
        {
            ParkWithStay(5f);
            Assert.IsFalse(_car.IsReadyToLeaveParking);
            Assert.Throws<InvalidOperationException>(() => _car.SendToParkingExit("wash_1"));
            Assert.Throws<InvalidOperationException>(() => _car.SendToParkingExit(null));

            _car.TickParkingStay(5f);

            Assert.IsTrue(_car.IsReadyToLeaveParking);
            Assert.Throws<ArgumentException>(() => _car.SendToParkingExit(" "));
            _car.SendToParkingExit("wash_1");
            Assert.AreEqual(CarState.ToParkingExit, _car.State);
        }

        [Test]
        public void ParkingStay_BlocksPatienceUntilOver()
        {
            ParkWithStay(5f);

            _car.TickPatience(3f);
            _car.TickParkingStay(3f);
            Assert.AreEqual(Patience, _car.PatienceLeft, "No patience is spent during the stay.");
            Assert.AreEqual(2f, _car.ParkingStayLeft, 0.0001f);

            _car.TickParkingStay(3f);
            Assert.AreEqual(0f, _car.ParkingStayLeft);
            Assert.IsTrue(_car.IsReadyToLeaveParking);

            _car.TickPatience(1f);
            Assert.AreEqual(Patience - 1f, _car.PatienceLeft, 0.0001f, "A ready car waits and loses patience.");
        }

        [Test]
        public void ParkingStay_DoesNotRunWhileDrivingToTheSlot()
        {
            _car.EnterQueue();
            _car.SendToParking(0, 5f);

            _car.TickParkingStay(10f);

            Assert.AreEqual(5f, _car.ParkingStayLeft);
        }

        [Test]
        public void SendToParking_InvalidArguments_Throw()
        {
            _car.EnterQueue();

            Assert.Throws<ArgumentOutOfRangeException>(() => _car.SendToParking(-1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _car.SendToParking(0, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _car.SendToParking(0, float.NaN));
            Assert.AreEqual(CarState.InQueue, _car.State);
        }

        [Test]
        public void MoveUpInQueue_ResetsArrival()
        {
            _car.EnterQueue();
            _car.MarkArrived(0f);

            _car.MoveUpInQueue();

            Assert.AreEqual(CarState.InQueue, _car.State);
            Assert.IsFalse(_car.HasArrived);
        }

        [Test]
        public void InvalidTransitions_Throw()
        {
            Assert.Throws<InvalidOperationException>(() => _car.SendToParking(0, 0f));
            Assert.Throws<InvalidOperationException>(() => _car.SendToPoint("wash_1"));
            Assert.Throws<InvalidOperationException>(() => _car.Leave());
            Assert.Throws<InvalidOperationException>(() => _car.SendToParkingExit(null));
            Assert.Throws<InvalidOperationException>(() => _car.ContinueFromParkingExit());
            Assert.Throws<InvalidOperationException>(() => _car.MoveUpInQueue());

            _car.EnterQueue();
            Assert.Throws<InvalidOperationException>(() => _car.EnterQueue());
            Assert.Throws<InvalidOperationException>(() => _car.ContinueFromParkingExit());

            _car.SendToParking(0, 0f);
            _car.MarkArrived(0f);
            Assert.Throws<InvalidOperationException>(() => _car.SendToPoint("wash_1"), "Parked cars go through the exit.");
            _car.SendToParkingExit(null);
            Assert.Throws<InvalidOperationException>(() => _car.ContinueFromParkingExit(), "Not at the exit yet.");
        }

        [Test]
        public void Patience_DrainsOnlyWhileWaiting()
        {
            _car.TickPatience(1f);
            Assert.AreEqual(Patience, _car.PatienceLeft, "Arriving does not drain.");

            _car.EnterQueue();
            _car.TickPatience(1f);
            Assert.AreEqual(Patience - 1f, _car.PatienceLeft, 0.0001f);

            _car.SendToPoint("wash_1");
            _car.TickPatience(1f);
            Assert.AreEqual(Patience - 1f, _car.PatienceLeft, 0.0001f, "Driving does not drain.");
        }

        [Test]
        public void Patience_DrainsAtParkingExitButNotOnTheWayThere()
        {
            ParkWithStay(0f);
            _car.SendToParkingExit(null);

            _car.TickPatience(1f);
            Assert.AreEqual(Patience, _car.PatienceLeft, 0.0001f, "Driving to the exit does not drain.");

            _car.MarkArrived(1f);
            _car.TickPatience(1f);
            Assert.AreEqual(Patience - 1f, _car.PatienceLeft, 0.0001f);
        }

        [Test]
        public void Patience_NeverBelowZeroAndDepletedRaisedOnce()
        {
            int depleted = 0;
            _car.PatienceDepleted += car => depleted++;
            _car.EnterQueue();

            _car.TickPatience(Patience * 2f);
            _car.TickPatience(1f);

            Assert.AreEqual(0f, _car.PatienceLeft);
            Assert.AreEqual(0f, _car.Patience01);
            Assert.AreEqual(1, depleted);
        }

        private void ParkWithStay(float stay)
        {
            _car.EnterQueue();
            _car.SendToParking(1, stay);
            _car.MarkArrived(1f);
            Assert.AreEqual(CarState.Parked, _car.State);
        }
    }
}
