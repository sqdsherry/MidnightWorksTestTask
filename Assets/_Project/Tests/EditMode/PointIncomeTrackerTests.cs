using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Domain.Points;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="PointIncomeTracker"/>: orders are published on a real bus, time is ticked by hand.</summary>
    public sealed class PointIncomeTrackerTests
    {
        private const string Wash = "loc1_wash_1";
        private const string Oil = "loc1_oil_1";

        private EventBus _bus;
        private WalletService _wallet;
        private ServicePointService _points;
        private PointIncomeTracker _tracker;
        private ServiceTypeSettings _type;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(new FakeGameLogger());
            _wallet = new WalletService(new Wallet(Money.Zero), _bus);
            _points = new ServicePointService(_wallet, _bus);
            _type = new ServiceTypeSettings("wash", "Wash", PointKind.Service, new Money(12), 0.0, 1f, 0f, 0f, "", 0, null, 0);
            _points.Register(_type.CreatePointDefinition(Wash, "loc1"));
            _tracker = new PointIncomeTracker(_points, _bus);
        }

        [TearDown]
        public void TearDown()
        {
            _tracker.Dispose();
            _points.Dispose();
            _wallet.Dispose();
        }

        [Test]
        public void Income_IsTheSumOverTheWindow()
        {
            Order(Wash, 12);
            _tracker.Tick(15f);
            Order(Wash, 30);
            _tracker.Tick(20f);
            Order(Wash, 8);

            Assert.AreEqual(new Money(50), _tracker.GetIncomePerMinute(Wash));
        }

        [Test]
        public void OldOrders_DropOutAfterAMinute()
        {
            Order(Wash, 12);
            _tracker.Tick(30f);
            Order(Wash, 30);

            _tracker.Tick(35f);
            Assert.AreEqual(new Money(30), _tracker.GetIncomePerMinute(Wash), "The first order is 65 s old.");

            _tracker.Tick(30f);
            Assert.AreEqual(Money.Zero, _tracker.GetIncomePerMinute(Wash));
        }

        [Test]
        public void HugeDelta_ClearsTheWholeWindow()
        {
            Order(Wash, 12);

            _tracker.Tick(1000f);

            Assert.AreEqual(Money.Zero, _tracker.GetIncomePerMinute(Wash));
        }

        [Test]
        public void Pause_StopsTheWindow()
        {
            Order(Wash, 12);

            for (int i = 0; i < 100; i++)
            {
                _tracker.Tick(0f);
            }

            Assert.AreEqual(new Money(12), _tracker.GetIncomePerMinute(Wash));
        }

        [Test]
        public void PointsRegisteredLater_AreTracked_AndKeptApart()
        {
            _points.Register(_type.CreatePointDefinition(Oil, "loc1"));

            Order(Oil, 25);
            Order(Wash, 12);

            Assert.AreEqual(new Money(25), _tracker.GetIncomePerMinute(Oil));
            Assert.AreEqual(new Money(12), _tracker.GetIncomePerMinute(Wash));
            Assert.AreEqual(Money.Zero, _tracker.GetIncomePerMinute("unknown"));
        }

        [Test]
        public void Dispose_StopsListening()
        {
            _tracker.Dispose();

            Order(Wash, 12);

            Assert.AreEqual(Money.Zero, _tracker.GetIncomePerMinute(Wash));
        }

        private void Order(string pointId, long price)
        {
            _bus.Publish(new OrderAcceptedEvent(pointId, "wash", PointKind.Service, new Money(price)));
        }
    }
}
