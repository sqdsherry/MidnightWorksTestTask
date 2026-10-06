using AutoService.Domain.Common;
using AutoService.Domain.Upgrades;
using AutoService.Services.Points;
using AutoService.Services.Save;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="PointsSaveable"/>.</summary>
    public sealed class PointsSaveableTests
    {
        private SaveTestWorld _world;
        private PointsSaveable _saveable;

        [SetUp]
        public void SetUp()
        {
            _world = new SaveTestWorld();
            _saveable = new PointsSaveable(_world.Points, _world.Upgrades, _world.Staff, _world.Logger);
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void Capture_WritesLevelsSupplyAndWorker()
        {
            _world.Upgrades.TryUpgrade(SaveTestWorld.Wash1, UpgradeKind.Speed);
            _world.Upgrades.TryUpgrade(SaveTestWorld.Wash1, UpgradeKind.Speed);
            _world.Upgrades.TryUpgrade(SaveTestWorld.Wash1, UpgradeKind.Price);
            _world.Point(SaveTestWorld.Wash1).Supply.Restore(4);
            _world.Staff.TryHireWorker(SaveTestWorld.Wash1);
            SaveData data = SaveData.CreateEmpty();

            _saveable.Capture(data);

            Assert.AreEqual(1, data.points.Length);
            PointSaveData entry = data.points[0];
            Assert.AreEqual(SaveTestWorld.Wash1, entry.pointId);
            Assert.AreEqual(2, entry.speedLevel);
            Assert.AreEqual(1, entry.priceLevel);
            Assert.AreEqual(4, entry.supply);
            Assert.IsTrue(entry.hasWorker);
        }

        [Test]
        public void Restore_SetsLevelsAndSupply()
        {
            SaveData data = DataFor(new PointSaveData { pointId = SaveTestWorld.Wash1, speedLevel = 3, priceLevel = 2, supply = 7 });

            _saveable.Restore(data);

            Assert.AreEqual(3, _world.Upgrades.GetLevel(SaveTestWorld.Wash1, UpgradeKind.Speed));
            Assert.AreEqual(2, _world.Upgrades.GetLevel(SaveTestWorld.Wash1, UpgradeKind.Price));
            Assert.AreEqual(7, _world.Point(SaveTestWorld.Wash1).Supply.Current);
        }

        [Test]
        public void UnsavedSupply_LeavesTheStockAlone()
        {
            int before = _world.Point(SaveTestWorld.Wash1).Supply.Current;
            SaveData data = DataFor(new PointSaveData { pointId = SaveTestWorld.Wash1, supply = PointSaveData.UnsavedSupply });

            _saveable.Restore(data);

            Assert.AreEqual(before, _world.Point(SaveTestWorld.Wash1).Supply.Current);
        }

        [Test]
        public void HasWorker_RestoresTheWorkerForFree()
        {
            Money balance = _world.Wallet.Balance;
            SaveData data = DataFor(new PointSaveData { pointId = SaveTestWorld.Wash1, supply = 5, hasWorker = true });

            _saveable.Restore(data);

            Assert.IsTrue(_world.Staff.HasWorker(SaveTestWorld.Wash1));
            Assert.AreEqual(1, _world.Agents.Spawned.Count);
            Assert.AreEqual(balance, _world.Wallet.Balance);
        }

        [Test]
        public void UnknownPoint_IsSkipped_WithWarning()
        {
            SaveData data = DataFor(
                new PointSaveData { pointId = "loc1_gone", speedLevel = 2, hasWorker = true },
                new PointSaveData { pointId = SaveTestWorld.Wash1, speedLevel = 1, supply = 3 });

            _saveable.Restore(data);

            Assert.AreEqual(1, _world.Upgrades.GetLevel(SaveTestWorld.Wash1, UpgradeKind.Speed), "Known points still restore.");
            Assert.AreEqual(0, _world.Agents.Spawned.Count);
            Assert.AreEqual(1, _world.Logger.Warnings.Count);
        }

        private static SaveData DataFor(params PointSaveData[] points)
        {
            SaveData data = SaveData.CreateEmpty();
            data.points = points;
            return data;
        }
    }
}
