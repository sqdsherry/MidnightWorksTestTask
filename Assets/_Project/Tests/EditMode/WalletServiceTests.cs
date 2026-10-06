using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="WalletService"/>.</summary>
    public sealed class WalletServiceTests
    {
        private EventBus _bus;
        private WalletService _service;
        private int _busEvents;
        private BalanceChangedEvent _lastBusEvent;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(new FakeGameLogger());
            _service = new WalletService(new Wallet(new Money(100)), _bus);
            _busEvents = 0;
            _bus.Subscribe<BalanceChangedEvent>(OnBusEvent);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Unsubscribe<BalanceChangedEvent>(OnBusEvent);
            _service.Dispose();
        }

        [Test]
        public void Spend_PublishesNegativeDelta()
        {
            _service.TrySpend(new Money(30));

            Assert.AreEqual(1, _busEvents);
            Assert.AreEqual(new Money(70), _lastBusEvent.Balance);
            Assert.AreEqual(-30L, _lastBusEvent.Delta);
        }

        [Test]
        public void Add_PublishesPositiveDeltaAndRaisesOwnEvent()
        {
            Money raised = Money.Zero;
            _service.BalanceChanged += balance => raised = balance;

            _service.Add(new Money(5));

            Assert.AreEqual(new Money(105), raised);
            Assert.AreEqual(5L, _lastBusEvent.Delta);
        }

        [Test]
        public void Dispose_StopsForwarding()
        {
            var wallet = new Wallet(new Money(10));
            var service = new WalletService(wallet, _bus);
            service.Dispose();

            wallet.Add(new Money(1));

            Assert.AreEqual(0, _busEvents);
        }

        private void OnBusEvent(BalanceChangedEvent gameEvent)
        {
            _busEvents++;
            _lastBusEvent = gameEvent;
        }
    }
}
