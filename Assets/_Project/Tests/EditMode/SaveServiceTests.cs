using System;
using AutoService.Infrastructure.Save;
using AutoService.Services.Save;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="SaveService"/> with an in-memory storage and the real <see cref="JsonUtilitySaveSerializer"/>
    /// (or <see cref="FakeSaveSerializer"/> where JSON cannot express the case).
    /// </summary>
    public sealed class SaveServiceTests
    {
        private FakeSaveStorage _storage;
        private FakeTimeProvider _time;
        private FakeGameLogger _logger;
        private SaveService _service;

        [SetUp]
        public void SetUp()
        {
            _storage = new FakeSaveStorage();
            _time = new FakeTimeProvider();
            _logger = new FakeGameLogger();
            _service = new SaveService(_storage, new JsonUtilitySaveSerializer(), _time, _logger);
        }

        [Test]
        public void SaveThenLoad_RoundTripsEveryField()
        {
            SaveData data = SaveData.CreateEmpty();
            data.money = 5000000000L;
            data.xp = 42;
            data.level = 3;
            data.builtPlotIds = new[] { "loc1_plot_wash_2", "loc1_plot_parking_2" };
            data.points = new[]
            {
                new PointSaveData { pointId = "loc1_wash_1", speedLevel = 2, priceLevel = 1, supply = 7, hasWorker = true },
                new PointSaveData { pointId = "loc1_entrance_main" },
            };
            data.storekeeperLocationIds = new[] { "loc1" };
            data.unlockedLocationIds = new[] { "loc1", "loc2" };
            data.vipLoyalty = 0.25f;
            data.tutorialStep = 5;

            _service.Save(data);
            bool loaded = _service.TryLoad(out SaveData result);

            Assert.IsTrue(loaded);
            Assert.AreNotSame(data, result);
            Assert.AreEqual(SaveData.CurrentVersion, result.version);
            Assert.AreEqual(_time.UtcNow.Ticks, result.savedAtUtcTicks);
            Assert.AreEqual(5000000000L, result.money);
            Assert.AreEqual(42, result.xp);
            Assert.AreEqual(3, result.level);
            CollectionAssert.AreEqual(data.builtPlotIds, result.builtPlotIds);
            CollectionAssert.AreEqual(data.storekeeperLocationIds, result.storekeeperLocationIds);
            CollectionAssert.AreEqual(data.unlockedLocationIds, result.unlockedLocationIds);
            Assert.AreEqual(0.25f, result.vipLoyalty);
            Assert.AreEqual(5, result.tutorialStep);

            Assert.AreEqual(2, result.points.Length);
            Assert.AreEqual("loc1_wash_1", result.points[0].pointId);
            Assert.AreEqual(2, result.points[0].speedLevel);
            Assert.AreEqual(1, result.points[0].priceLevel);
            Assert.AreEqual(7, result.points[0].supply);
            Assert.IsTrue(result.points[0].hasWorker);
            Assert.AreEqual("loc1_entrance_main", result.points[1].pointId);
            Assert.AreEqual(PointSaveData.UnsavedSupply, result.points[1].supply);
            Assert.IsFalse(result.points[1].hasWorker);
        }

        [Test]
        public void TryLoad_NoSave_ReturnsFalseWithoutWarning()
        {
            Assert.IsFalse(_service.HasSave);
            Assert.IsFalse(_service.TryLoad(out SaveData result));
            Assert.IsNull(result);
            Assert.AreEqual(0, _logger.Warnings.Count);
        }

        [Test]
        public void TryLoad_CorruptedJson_ReturnsFalseAndKeepsFile()
        {
            _storage.Json = "{ this is not json";

            Assert.IsFalse(_service.TryLoad(out SaveData result));
            Assert.IsNull(result);
            Assert.AreEqual(1, _logger.Warnings.Count);
            Assert.AreEqual("{ this is not json", _storage.Json);
        }

        [Test]
        public void TryLoad_VersionFromFuture_ReturnsFalse()
        {
            _storage.Json = "{\"version\":" + (SaveData.CurrentVersion + 1) + "}";

            Assert.IsFalse(_service.TryLoad(out SaveData result));
            Assert.IsNull(result);
            Assert.AreEqual(1, _logger.Warnings.Count);
        }

        [Test]
        public void TryLoad_VersionBelowOne_ReturnsFalse()
        {
            _storage.Json = "{\"version\":0}";

            Assert.IsFalse(_service.TryLoad(out SaveData _));
            Assert.AreEqual(1, _logger.Warnings.Count);
        }

        [Test]
        public void TryLoad_NullArrays_AreNormalizedToEmpty()
        {
            var serializer = new FakeSaveSerializer { Result = new SaveData { version = SaveData.CurrentVersion } };
            var service = new SaveService(_storage, serializer, _time, _logger);
            _storage.Json = "{}";

            Assert.IsTrue(service.TryLoad(out SaveData result));
            Assert.IsNotNull(result.builtPlotIds);
            Assert.IsNotNull(result.points);
            Assert.IsNotNull(result.storekeeperLocationIds);
            Assert.IsNotNull(result.unlockedLocationIds);
            Assert.AreEqual(0, result.builtPlotIds.Length);
            Assert.AreEqual(0, result.points.Length);
            Assert.AreEqual(0, result.storekeeperLocationIds.Length);
            Assert.AreEqual(0, result.unlockedLocationIds.Length);
        }

        [Test]
        public void Save_StampsVersionAndTime()
        {
            _time.UtcNow = new DateTime(2026, 10, 1, 12, 30, 0, DateTimeKind.Utc);
            var data = new SaveData { version = 0 };

            _service.Save(data);

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(_time.UtcNow.Ticks, data.savedAtUtcTicks);
            Assert.AreEqual(1, _storage.WriteCount);
            Assert.IsTrue(_service.HasSave);
        }

        [Test]
        public void Delete_RemovesSave()
        {
            _service.Save(SaveData.CreateEmpty());

            _service.Delete();

            Assert.IsFalse(_service.HasSave);
            Assert.IsFalse(_service.TryLoad(out SaveData _));
        }
    }
}
