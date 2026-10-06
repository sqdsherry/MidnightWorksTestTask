using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Domain.Points;
using AutoService.Domain.Upgrades;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Upgrades;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="UpgradeService"/> and <see cref="UpgradeTrack"/> with a real wallet, bus and points.</summary>
    public sealed class UpgradeServiceTests
    {
        private const string Wash = "loc1_wash_1";
        private const string Built = "loc1_wash_2";
        private const int MaxLevel = 3;
        private const int RequiredLevel = 2;

        private EventBus _bus;
        private WalletService _wallet;
        private ServicePointService _points;
        private FakeConfigProvider _config;
        private FakeUnlockGate _gate;
        private UpgradeService _service;
        private ServiceTypeSettings _washType;
        private ServicePoint _wash;

        private readonly List<UpgradePurchasedEvent> _purchased = new List<UpgradePurchasedEvent>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(new FakeGameLogger());
            _wallet = new WalletService(new Wallet(new Money(1000)), _bus);
            _points = new ServicePointService(_wallet, _bus);
            _gate = new FakeUnlockGate();
            _config = new FakeConfigProvider();
            _config.UpgradeList.Add(new UpgradeSettings(
                UpgradeKind.Speed, "Speed", "-10% service time", new Money(100), 1.35, MaxLevel, 0.10, RequiredLevel));
            _config.UpgradeList.Add(new UpgradeSettings(
                UpgradeKind.Price, "Price", "+15% price", new Money(100), 1.35, MaxLevel, 0.15, RequiredLevel));
            _washType = new ServiceTypeSettings("wash", "Wash", PointKind.Service, new Money(12), 0.0, 6f, 0f, 0f, "", 0, null, 0);

            _wash = _points.Register(_washType.CreatePointDefinition(Wash, "loc1"));
            _service = new UpgradeService(_points, _wallet, _gate, _config, _bus);

            _purchased.Clear();
            _bus.Subscribe<UpgradePurchasedEvent>(OnPurchased);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Unsubscribe<UpgradePurchasedEvent>(OnPurchased);
            _service.Dispose();
            _points.Dispose();
            _wallet.Dispose();
        }

        [Test]
        public void Cost_FollowsTheFormula()
        {
            Assert.AreEqual(new Money(100), _service.GetNextCost(Wash, UpgradeKind.Speed));

            Assert.IsTrue(_service.TryUpgrade(Wash, UpgradeKind.Speed));
            Assert.AreEqual(new Money(135), _service.GetNextCost(Wash, UpgradeKind.Speed), "100 × 1.35^1.");

            Assert.IsTrue(_service.TryUpgrade(Wash, UpgradeKind.Speed));
            Assert.AreEqual(new Money(182), _service.GetNextCost(Wash, UpgradeKind.Speed), "100 × 1.35^2 = 182.25.");
            Assert.AreEqual(new Money(765), _wallet.Balance, "1000 − 100 − 135.");
        }

        [Test]
        public void Upgrade_AppliesModifiersToThePoint()
        {
            _service.TryUpgrade(Wash, UpgradeKind.Speed);
            _service.TryUpgrade(Wash, UpgradeKind.Speed);
            _service.TryUpgrade(Wash, UpgradeKind.Price);

            Assert.AreEqual(0.81f, _wash.DurationMultiplier, 0.0001f, "0.9^2.");
            Assert.AreEqual(1.15, _wash.PriceMultiplier, 0.0001);
            Assert.AreEqual(2, _service.GetLevel(Wash, UpgradeKind.Speed));
            Assert.AreEqual(1, _service.GetLevel(Wash, UpgradeKind.Price));
        }

        [Test]
        public void Upgrade_PublishesThePurchase()
        {
            var raised = new List<UpgradeKind>();
            _service.Upgraded += (pointId, kind) => raised.Add(kind);

            _service.TryUpgrade(Wash, UpgradeKind.Price);

            Assert.AreEqual(1, _purchased.Count);
            Assert.AreEqual(Wash, _purchased[0].PointId);
            Assert.AreEqual(UpgradeKind.Price, _purchased[0].Kind);
            Assert.AreEqual(1, _purchased[0].NewLevel);
            Assert.AreEqual(new Money(100), _purchased[0].Cost);
            CollectionAssert.AreEqual(new[] { UpgradeKind.Price }, raised);
        }

        [Test]
        public void MaxLevel_IsMaxed()
        {
            for (int i = 0; i < MaxLevel; i++)
            {
                Assert.IsTrue(_service.TryUpgrade(Wash, UpgradeKind.Speed));
            }

            Assert.AreEqual(UpgradeAvailability.Maxed, _service.GetAvailability(Wash, UpgradeKind.Speed));
            Assert.IsFalse(_service.TryUpgrade(Wash, UpgradeKind.Speed));
            Assert.AreEqual(Money.Zero, _service.GetNextCost(Wash, UpgradeKind.Speed));
            Assert.AreEqual(MaxLevel, _service.GetLevel(Wash, UpgradeKind.Speed));
        }

        [Test]
        public void LockedGate_BlocksTheUpgrade()
        {
            _gate.UnlockUpTo(RequiredLevel - 1);

            Assert.AreEqual(UpgradeAvailability.Locked, _service.GetAvailability(Wash, UpgradeKind.Speed));
            Assert.IsFalse(_service.TryUpgrade(Wash, UpgradeKind.Speed));
            Assert.AreEqual(new Money(1000), _wallet.Balance);
        }

        [Test]
        public void NotEnoughMoney_IsReported_AndNothingIsSpent()
        {
            _wallet.TrySpend(new Money(950));

            Assert.AreEqual(UpgradeAvailability.NotEnoughMoney, _service.GetAvailability(Wash, UpgradeKind.Price));
            Assert.IsFalse(_service.TryUpgrade(Wash, UpgradeKind.Price));
            Assert.AreEqual(new Money(50), _wallet.Balance);
            Assert.AreEqual(1.0, _wash.PriceMultiplier);
        }

        [Test]
        public void Restore_SetsTheLevelWithoutPayment_AndClamps()
        {
            _service.Restore(Wash, UpgradeKind.Speed, 99);
            _service.Restore("unknown", UpgradeKind.Speed, 1);

            Assert.AreEqual(MaxLevel, _service.GetLevel(Wash, UpgradeKind.Speed));
            Assert.AreEqual((float)Math.Pow(0.9, MaxLevel), _wash.DurationMultiplier, 0.0001f);
            Assert.AreEqual(new Money(1000), _wallet.Balance);
            Assert.AreEqual(0, _purchased.Count, "A restore is not a purchase.");
        }

        [Test]
        public void PointsRegisteredLater_GetTracks()
        {
            ServicePoint built = _points.Register(_washType.CreatePointDefinition(Built, "loc1"));

            Assert.IsTrue(_service.TryUpgrade(Built, UpgradeKind.Price));
            Assert.AreEqual(1.15, built.PriceMultiplier, 0.0001);
        }

        [Test]
        public void UnknownPoint_Throws_AndKindWithoutConfig_IsNotSupported()
        {
            Assert.Throws<ArgumentException>(() => _service.GetAvailability("unknown", UpgradeKind.Speed));
            Assert.IsFalse(_service.TryUpgrade("unknown", UpgradeKind.Speed));

            _config.UpgradeList.Clear();
            using (var bare = new UpgradeService(_points, _wallet, _gate, _config, _bus))
            {
                Assert.AreEqual(UpgradeAvailability.NotSupported, bare.GetAvailability(Wash, UpgradeKind.Speed));
                Assert.AreEqual(0, bare.GetLevel(Wash, UpgradeKind.Speed));
            }
        }

        [Test]
        public void Track_LevelUpAtMax_Throws_AndRestoreClamps()
        {
            var track = new UpgradeTrack(1);
            track.LevelUp();

            Assert.IsTrue(track.IsMaxed);
            Assert.Throws<InvalidOperationException>(() => track.LevelUp());
            track.Restore(-5);
            Assert.AreEqual(0, track.Level);
        }

        [Test]
        public void SpeedFormula_NeverReachesZero()
        {
            var speed = new UpgradeSettings(UpgradeKind.Speed, "Speed", "", new Money(100), 1.35, 10, 0.10, 1);

            Assert.AreEqual(0.3487, speed.DurationMultiplierAt(10), 0.0001, "0.9^10.");
            Assert.AreEqual(1.0, speed.PriceMultiplierAt(10));
            Assert.Throws<ArgumentException>(() => new UpgradeSettings(UpgradeKind.Speed, "", "", Money.Zero, 1.0, 1, 1.0, 1));
        }

        private void OnPurchased(UpgradePurchasedEvent gameEvent) => _purchased.Add(gameEvent);
    }
}
