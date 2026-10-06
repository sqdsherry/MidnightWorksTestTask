using System;
using AutoService.Domain.Common;
using AutoService.Domain.Points;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="ServicePoint"/>.</summary>
    public sealed class ServicePointTests
    {
        private const int CarId = 7;
        private const float ServiceDuration = 4f;
        private const float AcceptDelay = 0.5f;
        private const float ClearDelay = 1f;

        private ServicePoint _point;
        private int _acceptedCount;
        private Money _acceptedPrice;
        private int _completedCar;

        [SetUp]
        public void SetUp()
        {
            var definition = new ServicePointDefinition(
                "wash_1", "loc1", "wash", PointKind.Service, new Money(12), 0.0, ServiceDuration, AcceptDelay, ClearDelay);
            _point = new ServicePoint(definition);
            _acceptedCount = 0;
            _completedCar = ServicePoint.NoCar;
            _point.OrderAccepted += (point, price) =>
            {
                _acceptedCount++;
                _acceptedPrice = price;
            };
            _point.ServiceCompleted += (point, carId) => _completedCar = carId;
        }

        [Test]
        public void Definition_RejectsEmptyIdAndNegativeDuration()
        {
            Assert.Throws<ArgumentException>(() =>
                new ServicePointDefinition(" ", "loc1", "wash", PointKind.Service, Money.Zero, 0.0, 1f, 0f, 0f));
            Assert.Throws<ArgumentException>(() =>
                new ServicePointDefinition("p", "loc1", "wash", PointKind.Service, Money.Zero, 0.0, -1f, 0f, 0f));
            Assert.Throws<ArgumentException>(() =>
                new ServicePointDefinition("p", "loc1", "parking", PointKind.Barrier, Money.Zero, -0.5, 1f, 0f, 0f));
            Assert.Throws<ArgumentException>(() =>
                new ServicePointDefinition("p", "loc1", "parking", PointKind.Barrier, Money.Zero, double.NaN, 1f, 0f, 0f));
        }

        [Test]
        public void NewPoint_IsIdleAndAvailable()
        {
            Assert.AreEqual(ServicePointState.Idle, _point.State);
            Assert.IsTrue(_point.IsAvailable);
            Assert.AreEqual(ServicePoint.NoCar, _point.CarId);
        }

        [Test]
        public void WithoutOccupant_OrderIsNeverAccepted()
        {
            ArriveCar(new Money(30));

            _point.Tick(10f);

            Assert.AreEqual(ServicePointState.AwaitingAccept, _point.State);
            Assert.AreEqual(0, _acceptedCount);
        }

        [Test]
        public void Occupied_AcceptsAfterDelayWithPrice()
        {
            ArriveCar(new Money(30));
            _point.TryOccupy(OccupantKind.Player);

            _point.Tick(AcceptDelay * 0.5f);
            Assert.AreEqual(0, _acceptedCount);

            _point.Tick(AcceptDelay * 0.6f);

            Assert.AreEqual(ServicePointState.Servicing, _point.State);
            Assert.AreEqual(1, _acceptedCount);
            Assert.AreEqual(new Money(30), _acceptedPrice);
        }

        [Test]
        public void AcceptDelay_RestartsWhenOccupantLeaves()
        {
            ArriveCar(new Money(30));
            _point.TryOccupy(OccupantKind.Player);
            _point.Tick(AcceptDelay * 0.8f);

            _point.Vacate(OccupantKind.Player);
            _point.TryOccupy(OccupantKind.Player);
            _point.Tick(AcceptDelay * 0.8f);

            Assert.AreEqual(ServicePointState.AwaitingAccept, _point.State);
            Assert.AreEqual(0, _acceptedCount);

            _point.Tick(AcceptDelay * 0.3f);
            Assert.AreEqual(1, _acceptedCount);
        }

        [Test]
        public void Progress_AdvancesOnlyWhileOccupied()
        {
            StartServicing();

            _point.Tick(ServiceDuration * 0.25f);
            Assert.AreEqual(0.25f, _point.Progress, 0.0001f);

            _point.Vacate(OccupantKind.Player);
            _point.Tick(ServiceDuration);
            Assert.AreEqual(0.25f, _point.Progress, 0.0001f);
            Assert.AreEqual(ServicePointState.Servicing, _point.State);

            _point.TryOccupy(OccupantKind.Player);
            _point.Tick(ServiceDuration * 0.25f);
            Assert.AreEqual(0.5f, _point.Progress, 0.0001f);
        }

        [Test]
        public void Completion_RaisesEventAndFreesCar()
        {
            StartServicing();

            _point.Tick(ServiceDuration);

            Assert.AreEqual(ServicePointState.Clearing, _point.State);
            Assert.AreEqual(CarId, _completedCar);
            Assert.AreEqual(ServicePoint.NoCar, _point.CarId);
            Assert.IsFalse(_point.IsAvailable);
        }

        [Test]
        public void Clearing_BecomesIdleAfterClearDelay()
        {
            StartServicing();
            _point.Tick(ServiceDuration);

            _point.Tick(ClearDelay * 0.5f);
            Assert.AreEqual(ServicePointState.Clearing, _point.State);

            _point.Tick(ClearDelay * 0.6f);
            Assert.AreEqual(ServicePointState.Idle, _point.State);
            Assert.AreEqual(0f, _point.Progress);
            Assert.IsTrue(_point.IsAvailable);
        }

        [Test]
        public void ZeroDeltaTime_DoesNotAdvance()
        {
            var instant = new ServicePoint(new ServicePointDefinition(
                "p", "loc1", "wash", PointKind.Service, new Money(1), 0.0, 0f, 0f, 0f));
            instant.TryReserve(CarId, new Money(1));
            instant.NotifyCarArrived(CarId);
            instant.TryOccupy(OccupantKind.Player);

            instant.Tick(0f);

            Assert.AreEqual(ServicePointState.AwaitingAccept, instant.State);
        }

        [Test]
        public void TryOccupy_FailsWhenHeldByAnother()
        {
            Assert.IsTrue(_point.TryOccupy(OccupantKind.Worker));

            Assert.IsFalse(_point.TryOccupy(OccupantKind.Player));
            Assert.AreEqual(OccupantKind.Worker, _point.Occupant);
            Assert.IsTrue(_point.TryOccupy(OccupantKind.Worker));
        }

        [Test]
        public void TryOccupy_None_Throws()
        {
            Assert.Throws<ArgumentException>(() => _point.TryOccupy(OccupantKind.None));
        }

        [Test]
        public void Vacate_ByOtherOccupant_IsIgnored()
        {
            _point.TryOccupy(OccupantKind.Worker);

            _point.Vacate(OccupantKind.Player);

            Assert.AreEqual(OccupantKind.Worker, _point.Occupant);
        }

        [Test]
        public void OccupantChanged_RaisedOnRealChangesOnly()
        {
            int raised = 0;
            _point.OccupantChanged += point => raised++;

            _point.TryOccupy(OccupantKind.Player);
            _point.TryOccupy(OccupantKind.Player);
            _point.Vacate(OccupantKind.Worker);
            _point.Vacate(OccupantKind.Player);

            Assert.AreEqual(2, raised);
        }

        [Test]
        public void TryReserve_OnlyFromIdle()
        {
            Assert.IsTrue(_point.TryReserve(CarId, new Money(5)));

            Assert.IsFalse(_point.TryReserve(CarId + 1, new Money(5)));
            Assert.AreEqual(ServicePointState.Reserved, _point.State);
            Assert.AreEqual(CarId, _point.CarId);
        }

        [Test]
        public void NotifyCarArrived_ForeignCar_Throws()
        {
            _point.TryReserve(CarId, new Money(5));

            Assert.Throws<InvalidOperationException>(() => _point.NotifyCarArrived(CarId + 1));
        }

        [Test]
        public void NotifyCarArrived_WithoutReservation_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _point.NotifyCarArrived(CarId));
        }

        [Test]
        public void CancelReservation_ReturnsToIdle()
        {
            int stateChanges = 0;
            _point.TryReserve(CarId, new Money(5));
            _point.StateChanged += point => stateChanges++;

            _point.CancelReservation(CarId);

            Assert.AreEqual(ServicePointState.Idle, _point.State);
            Assert.AreEqual(ServicePoint.NoCar, _point.CarId);
            Assert.AreEqual(Money.Zero, _point.CurrentPrice);
            Assert.AreEqual(1, stateChanges);
            Assert.IsTrue(_point.TryReserve(CarId + 1, new Money(5)));
        }

        [Test]
        public void CancelReservation_ForeignCarOrAfterArrival_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _point.CancelReservation(CarId), "Nothing reserved.");

            _point.TryReserve(CarId, new Money(5));
            Assert.Throws<InvalidOperationException>(() => _point.CancelReservation(CarId + 1));

            _point.NotifyCarArrived(CarId);
            Assert.Throws<InvalidOperationException>(() => _point.CancelReservation(CarId), "The car is already there.");
        }

        // ── Supplies and upgrades (A2) ───────────────────────────────────────────────────────────────────────────

        [Test]
        public void Definition_RejectsSupplyWithoutCapacity()
        {
            Assert.Throws<ArgumentException>(() =>
                new ServicePointDefinition("p", "loc1", "oil", PointKind.Service, Money.Zero, 0.0, 1f, 0f, 0f, "oil", 0));
        }

        [Test]
        public void PointWithoutSupplyType_HasNoStock_AndWorksAsBefore()
        {
            Assert.IsNull(_point.Supply);

            StartServicing();
            Assert.IsFalse(_point.IsWaitingForSupply);
            Assert.AreEqual(1, _acceptedCount);
        }

        [Test]
        public void SuppliedPoint_StartsFull_AndAcceptedOrderUsesOneUnit()
        {
            UseSuppliedPoint(10);
            Assert.AreEqual(10, _point.Supply.Current);

            ServicePointState stateWhenConsumed = ServicePointState.Idle;
            _point.Supply.Changed += stock => stateWhenConsumed = _point.State;

            StartServicing();

            Assert.AreEqual(9, _point.Supply.Current);
            Assert.AreEqual(1, _acceptedCount);
            Assert.AreEqual(ServicePointState.Servicing, stateWhenConsumed, "Stock listeners see the order already being served.");
        }

        [Test]
        public void EmptySupply_BlocksAcceptance_AndTheAcceptTimerDoesNotRun()
        {
            UseSuppliedPoint(1);
            ServeOneOrder();
            Assert.IsTrue(_point.Supply.IsEmpty);

            ArriveCar(new Money(12));
            _point.TryOccupy(OccupantKind.Player);
            _point.Tick(10f);

            Assert.AreEqual(ServicePointState.AwaitingAccept, _point.State);
            Assert.IsTrue(_point.IsWaitingForSupply);
            Assert.AreEqual(1, _acceptedCount, "Only the first order was accepted.");

            _point.Supply.Add(1);
            _point.Tick(AcceptDelay * 0.5f);
            Assert.AreEqual(1, _acceptedCount, "The delay starts only once there is a unit to sell.");

            _point.Tick(AcceptDelay * 0.6f);
            Assert.AreEqual(2, _acceptedCount);
            Assert.IsTrue(_point.Supply.IsEmpty);
            Assert.IsFalse(_point.IsWaitingForSupply);
        }

        [Test]
        public void DurationMultiplier_Half_ServicesTwiceAsFast()
        {
            _point.ApplyModifiers(0.5f, 1.0);
            StartServicing();

            _point.Tick(ServiceDuration * 0.25f);
            Assert.AreEqual(0.5f, _point.Progress, 0.001f);

            _point.Tick(ServiceDuration * 0.25f + 0.001f);
            Assert.AreEqual(CarId, _completedCar);
        }

        [Test]
        public void ApplyModifiers_RejectsNonPositiveAndNaN()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _point.ApplyModifiers(0f, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _point.ApplyModifiers(float.NaN, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _point.ApplyModifiers(1f, -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _point.ApplyModifiers(1f, double.NaN));
            Assert.AreEqual(1f, _point.DurationMultiplier);
            Assert.AreEqual(1.0, _point.PriceMultiplier);
        }

        private void UseSuppliedPoint(int capacity)
        {
            var definition = new ServicePointDefinition(
                "oil_1", "loc1", "oil", PointKind.Service, new Money(12), 0.0, ServiceDuration, AcceptDelay, ClearDelay, "oil", capacity);
            _point = new ServicePoint(definition);
            _point.OrderAccepted += (point, price) => _acceptedCount++;
            _point.ServiceCompleted += (point, carId) => _completedCar = carId;
        }

        private void ServeOneOrder()
        {
            StartServicing();
            _point.Tick(ServiceDuration);
            _point.Tick(ClearDelay);
            _point.Vacate(OccupantKind.Player);
            Assert.AreEqual(ServicePointState.Idle, _point.State);
        }

        private void ArriveCar(Money price)
        {
            _point.TryReserve(CarId, price);
            _point.NotifyCarArrived(CarId);
        }

        private void StartServicing()
        {
            ArriveCar(new Money(12));
            _point.TryOccupy(OccupantKind.Player);
            _point.Tick(AcceptDelay);
            Assert.AreEqual(ServicePointState.Servicing, _point.State);
        }
    }
}
