using System;
using System.Collections.Generic;
using AutoService.Domain.Building;
using AutoService.Domain.Common;
using AutoService.Domain.Progression;
using AutoService.Presentation.Popups;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Progression;
using NUnit.Framework;
using UnityEngine;

namespace AutoService.Tests.EditMode
{
    public sealed class LevelUpPresenterTests
    {
        private GameObject _go;
        private LevelUpPopupView _view;
        private FakeProgressionService _progression;
        private LevelUpPresenter _presenter;

        private sealed class FakeProgressionService : IProgressionService
        {
            public int Xp { get; set; }
            public int Level { get; set; } = 1;
            public float LevelProgress01 => 0.5f;
            public int XpToNextLevel => 10;

            public event Action Changed;
            public event Action<int> LeveledUp;

            public void Restore(int xp)
            {
                Xp = xp;
                Changed?.Invoke();
            }

            public void AddExperience(int amount)
            {
                Xp += amount;
                Changed?.Invoke();
            }

            public void TriggerLevelUp(int newLevel)
            {
                Level = newLevel;
                LeveledUp?.Invoke(newLevel);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LevelUpPopupTest");
            _view = _go.AddComponent<LevelUpPopupView>();
            _progression = new FakeProgressionService();
            _presenter = new LevelUpPresenter(_progression, _view, Buildables);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        private static readonly BuildableSettings[] Buildables =
        {
            new BuildableSettings("wash_2", "Wash Bay 2", "", BuildableKind.ServicePoint, "wash_2", new Money(100), 2, 0d),
            new BuildableSettings("parking_3", "Parking Spot 3", "", BuildableKind.ParkingSlot, "2", new Money(50), 2, 0d),
            new BuildableSettings("oil_1", "Oil Change 1", "", BuildableKind.ServicePoint, "oil_1", new Money(200), 3, 0d),
        };

        [Test]
        public void CollectUnlocks_ReturnsBuildablesOfExactlyThatLevel()
        {
            var names = new List<string>();

            LevelUpPresenter.CollectUnlocks(Buildables, 2, names);
            CollectionAssert.AreEqual(new[] { "Wash Bay 2", "Parking Spot 3" }, names);

            LevelUpPresenter.CollectUnlocks(Buildables, 3, names);
            CollectionAssert.AreEqual(new[] { "Oil Change 1" }, names);

            LevelUpPresenter.CollectUnlocks(Buildables, 5, names);
            Assert.IsEmpty(names);
        }

        [Test]
        public void OnLeveledUp_OpensPopup()
        {
            _progression.TriggerLevelUp(2);
            Assert.IsTrue(_view.IsOpen);
        }

        [Test]
        public void Dispose_UnsubscribesFromEvents()
        {
            _presenter.Dispose();
            _view.Hide();
            _progression.TriggerLevelUp(2);
            Assert.IsFalse(_view.IsOpen);
        }

        [Test]
        public void ProgressionService_AddExperience_TriggersLevelUp()
        {
            var config = new FakeConfigProvider();
            var eventBus = new EventBus(new FakeGameLogger());
            var table = new LevelTable(new[] { 0, 10, 25 }, 50);
            var progress = new PlayerProgress(table);
            var service = new ProgressionService(progress, config, eventBus);

            int leveledUpTo = 0;
            service.LeveledUp += lvl => leveledUpTo = lvl;

            service.AddExperience(10);

            Assert.AreEqual(2, service.Level);
            Assert.AreEqual(2, leveledUpTo);
        }

        [Test]
        public void WalletServiceExtensions_AddMoney_AddsCurrency()
        {
            var wallet = new Domain.Economy.Wallet(Money.Zero);
            var eventBus = new EventBus(new FakeGameLogger());
            var service = new Services.Economy.WalletService(wallet, eventBus);

            service.AddMoney(1000);
            Assert.AreEqual(1000L, service.Balance.Amount);

            service.AddMoney(10000);
            Assert.AreEqual(11000L, service.Balance.Amount);
        }
    }
}
