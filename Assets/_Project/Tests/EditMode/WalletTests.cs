using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="Wallet"/>.</summary>
    public sealed class WalletTests
    {
        private Wallet _wallet;
        private int _eventCount;
        private Money _lastEventBalance;

        [SetUp]
        public void SetUp()
        {
            _wallet = new Wallet(new Money(100));
            _eventCount = 0;
            _lastEventBalance = Money.Zero;
            _wallet.BalanceChanged += OnBalanceChanged;
        }

        [TearDown]
        public void TearDown()
        {
            _wallet.BalanceChanged -= OnBalanceChanged;
        }

        [Test]
        public void Constructor_SetsInitialBalance()
        {
            Assert.AreEqual(new Money(100), _wallet.Balance);
        }

        [Test]
        public void TrySpend_NotEnough_ReturnsFalseWithoutChangeOrEvent()
        {
            bool spent = _wallet.TrySpend(new Money(101));

            Assert.IsFalse(spent);
            Assert.AreEqual(new Money(100), _wallet.Balance);
            Assert.AreEqual(0, _eventCount);
        }

        [Test]
        public void TrySpend_Enough_DeductsAndRaisesEventWithNewBalance()
        {
            bool spent = _wallet.TrySpend(new Money(40));

            Assert.IsTrue(spent);
            Assert.AreEqual(new Money(60), _wallet.Balance);
            Assert.AreEqual(1, _eventCount);
            Assert.AreEqual(new Money(60), _lastEventBalance);
        }

        [Test]
        public void TrySpend_WholeBalance_Succeeds()
        {
            Assert.IsTrue(_wallet.TrySpend(new Money(100)));
            Assert.AreEqual(Money.Zero, _wallet.Balance);
        }

        [Test]
        public void TrySpend_Zero_ReturnsTrueWithoutEvent()
        {
            Assert.IsTrue(_wallet.TrySpend(Money.Zero));
            Assert.AreEqual(0, _eventCount);
        }

        [Test]
        public void Add_IncreasesBalanceAndRaisesEvent()
        {
            _wallet.Add(new Money(25));

            Assert.AreEqual(new Money(125), _wallet.Balance);
            Assert.AreEqual(1, _eventCount);
            Assert.AreEqual(new Money(125), _lastEventBalance);
        }

        [Test]
        public void Add_Zero_DoesNotRaiseEvent()
        {
            _wallet.Add(Money.Zero);

            Assert.AreEqual(new Money(100), _wallet.Balance);
            Assert.AreEqual(0, _eventCount);
        }

        [Test]
        public void CanAfford_ComparesWithBalance()
        {
            Assert.IsTrue(_wallet.CanAfford(new Money(100)));
            Assert.IsFalse(_wallet.CanAfford(new Money(101)));
        }

        private void OnBalanceChanged(Money balance)
        {
            _eventCount++;
            _lastEventBalance = balance;
        }
    }
}
