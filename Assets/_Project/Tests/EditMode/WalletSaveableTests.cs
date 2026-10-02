using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Save;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="WalletSaveable"/>.</summary>
    public sealed class WalletSaveableTests
    {
        private EventBus _bus;
        private FakeGameLogger _logger;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(new FakeGameLogger());
            _logger = new FakeGameLogger();
        }

        [Test]
        public void CaptureRestore_RoundTripsTheBalance_AndRaisesBalanceChanged()
        {
            var saved = new WalletService(new Wallet(new Money(4321)), _bus);
            SaveData data = SaveData.CreateEmpty();
            new WalletSaveable(saved, _logger).Capture(data);
            saved.Dispose();

            var restored = new WalletService(new Wallet(new Money(100)), _bus);
            Money? raised = null;
            restored.BalanceChanged += balance => raised = balance;
            new WalletSaveable(restored, _logger).Restore(data);

            Assert.AreEqual(4321L, data.money);
            Assert.AreEqual(new Money(4321), restored.Balance);
            Assert.AreEqual(new Money(4321), raised);
            restored.Dispose();
        }

        [Test]
        public void NegativeMoney_RestoresZero_WithWarning()
        {
            var wallet = new WalletService(new Wallet(new Money(100)), _bus);
            SaveData data = SaveData.CreateEmpty();
            data.money = -5;

            new WalletSaveable(wallet, _logger).Restore(data);

            Assert.AreEqual(Money.Zero, wallet.Balance);
            Assert.AreEqual(1, _logger.Warnings.Count);
            wallet.Dispose();
        }
    }
}
