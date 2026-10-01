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
        public void ParkingRoute_BarrierToParkingToPoint()
        {
            _car.EnterQueue();
            _car.SendToBarrier(2);
            Assert.AreEqual(CarState.ToBarrier, _car.State);
            Assert.AreEqual(2, _car.ParkingSlot);

            _car.MarkArrived(1f);
            Assert.AreEqual(CarState.AtBarrier, _car.State);

            _car.SendToParking();
            _car.MarkArrived(5f);
            Assert.AreEqual(CarState.Parked, _car.State);
            Assert.AreEqual(5f, _car.ParkedAtTime);

            _car.SendToPoint("wash_1");
            Assert.AreEqual(CarState.ToPoint, _car.State);
            Assert.AreEqual(Car.NoParkingSlot, _car.ParkingSlot);
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
            Assert.Throws<InvalidOperationException>(() => _car.SendToBarrier(0));
            Assert.Throws<InvalidOperationException>(() => _car.SendToPoint("wash_1"));
            Assert.Throws<InvalidOperationException>(() => _car.Leave());
            Assert.Throws<InvalidOperationException>(() => _car.SendToParking());
            Assert.Throws<InvalidOperationException>(() => _car.MoveUpInQueue());

            _car.EnterQueue();
            Assert.Throws<InvalidOperationException>(() => _car.EnterQueue());
            Assert.Throws<InvalidOperationException>(() => _car.SendToParking());
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
    }
}
