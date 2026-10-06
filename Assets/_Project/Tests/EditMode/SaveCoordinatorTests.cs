using System.Collections.Generic;
using AutoService.Services.Save;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="SaveCoordinator"/> with a fake save service and logging saveables.</summary>
    public sealed class SaveCoordinatorTests
    {
        private const float Interval = 30f;
        private const float Frame = 0.016f;

        private readonly List<string> _log = new List<string>();
        private FakeSaveService _saveService;
        private FakeGameLogger _logger;
        private SaveCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _saveService = new FakeSaveService();
            _logger = new FakeGameLogger();
            _coordinator = new SaveCoordinator(_saveService, _logger, Interval);
        }

        [TearDown]
        public void TearDown()
        {
            _coordinator.Dispose();
        }

        [Test]
        public void SaveNow_CapturesInRegistrationOrderAndSavesOnce()
        {
            _coordinator.Add(new FakeSaveable("a", _log));
            _coordinator.Add(new FakeSaveable("b", _log));
            _coordinator.Add(new FakeSaveable("c", _log));

            _coordinator.SaveNow();

            CollectionAssert.AreEqual(new[] { "capture:a", "capture:b", "capture:c" }, _log);
            Assert.AreEqual(1, _saveService.Saved.Count);
        }

        [Test]
        public void Add_SameInstanceTwice_IsCapturedOnce()
        {
            var saveable = new FakeSaveable("a", _log);
            _coordinator.Add(saveable);
            _coordinator.Add(saveable);

            _coordinator.SaveNow();

            CollectionAssert.AreEqual(new[] { "capture:a" }, _log);
        }

        [Test]
        public void Remove_StopsCapturing()
        {
            var a = new FakeSaveable("a", _log);
            var b = new FakeSaveable("b", _log);
            _coordinator.Add(a);
            _coordinator.Add(b);

            _coordinator.Remove(a);
            _coordinator.SaveNow();

            CollectionAssert.AreEqual(new[] { "capture:b" }, _log);
        }

        [Test]
        public void TryRestore_WithoutSave_ReturnsFalseAndRestoresNothing()
        {
            _coordinator.Add(new FakeSaveable("a", _log));

            Assert.IsFalse(_coordinator.TryRestore());
            Assert.AreEqual(0, _log.Count);
        }

        [Test]
        public void TryRestore_WithSave_RestoresInRegistrationOrder()
        {
            var a = new FakeSaveable("a", _log);
            var b = new FakeSaveable("b", _log);
            _coordinator.Add(a);
            _coordinator.Add(b);
            _saveService.Stored = SaveData.CreateEmpty();

            Assert.IsTrue(_coordinator.TryRestore());
            CollectionAssert.AreEqual(new[] { "restore:a", "restore:b" }, _log);
            Assert.AreSame(_saveService.Stored, a.Restored);
            Assert.AreSame(_saveService.Stored, b.Restored);
        }

        [Test]
        public void Tick_AutosavesExactlyAtInterval()
        {
            _coordinator.Tick(Interval - 1f);
            Assert.AreEqual(0, _saveService.Saved.Count);

            _coordinator.Tick(1f);
            Assert.AreEqual(1, _saveService.Saved.Count);

            // The timer restarts after a save.
            _coordinator.Tick(Interval - 1f);
            Assert.AreEqual(1, _saveService.Saved.Count);
        }

        [Test]
        public void Tick_ZeroDelta_DoesNotAutosave()
        {
            _coordinator.Tick(Interval - 1f);

            for (int i = 0; i < 1000; i++)
            {
                _coordinator.Tick(0f);
            }

            Assert.AreEqual(0, _saveService.Saved.Count);
        }

        [Test]
        public void RequestSave_ManyTimesInOneFrame_SavesOnceOnNextTick()
        {
            _coordinator.RequestSave();
            _coordinator.RequestSave();
            _coordinator.RequestSave();
            Assert.AreEqual(0, _saveService.Saved.Count);

            _coordinator.Tick(Frame);
            Assert.AreEqual(1, _saveService.Saved.Count);

            _coordinator.Tick(Frame);
            Assert.AreEqual(1, _saveService.Saved.Count);
        }

        [Test]
        public void SaveNow_SaveableThrows_OthersAreCapturedAndSaveIsWritten()
        {
            _coordinator.Add(new FakeSaveable("broken", _log) { Throws = true });
            _coordinator.Add(new FakeSaveable("ok", _log) { TutorialStepToWrite = 4 });

            _coordinator.SaveNow();

            CollectionAssert.AreEqual(new[] { "capture:broken", "capture:ok" }, _log);
            Assert.AreEqual(1, _saveService.Saved.Count);
            Assert.AreEqual(4, _saveService.Saved[0].tutorialStep);
            Assert.AreEqual(1, _logger.Errors.Count);
            StringAssert.Contains(nameof(FakeSaveable), _logger.Errors[0]);
        }

        [Test]
        public void ResetProgress_DeletesAndStopsAutosaveAndRequests()
        {
            _coordinator.ResetProgress();
            _coordinator.RequestSave();
            _coordinator.Tick(Interval);
            _coordinator.Tick(Interval);

            Assert.AreEqual(1, _saveService.DeleteCount);
            Assert.AreEqual(0, _saveService.Saved.Count);
        }

        [Test]
        public void ResetProgress_ThenSaveNow_WritesNothing()
        {
            _coordinator.Add(new FakeSaveable("a", _log));
            _coordinator.ResetProgress();

            _coordinator.SaveNow();
            _coordinator.Tick(Interval);

            Assert.AreEqual(0, _saveService.Saved.Count);
            Assert.AreEqual(0, _log.Count);
        }

        [Test]
        public void ResetProgress_ThenTryRestore_ResumesAutosave()
        {
            _coordinator.ResetProgress();

            _coordinator.TryRestore();
            _coordinator.Tick(Interval);

            Assert.AreEqual(1, _saveService.Saved.Count);
        }
    }
}
