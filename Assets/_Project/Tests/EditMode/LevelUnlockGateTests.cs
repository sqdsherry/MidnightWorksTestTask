using AutoService.Domain.Progression;
using AutoService.Services.Config;
using AutoService.Services.Events;
using AutoService.Services.Progression;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    public sealed class LevelUnlockGateTests
    {
        private PlayerProgress _progress;
        private ProgressionService _service;
        private LevelUnlockGate _gate;

        [SetUp]
        public void SetUp()
        {
            var config = new FakeConfigProvider();
            var eventBus = new EventBus(new FakeGameLogger());
            _progress = new PlayerProgress(new LevelTable(new[] { 0, 15 }, 50));
            _service = new ProgressionService(_progress, config, eventBus);
            _gate = new LevelUnlockGate(_service);
        }

        [Test]
        public void IsUnlocked_Level1_Required2_ReturnsFalse()
        {
            Assert.IsFalse(_gate.IsUnlocked(2));
        }

        [Test]
        public void IsUnlocked_AfterLevelUp_ReturnsTrueAndInvokesChanged()
        {
            bool changedCalled = false;
            _gate.Changed += () => changedCalled = true;

            _progress.AddXp(15); // Level 2

            Assert.IsTrue(_gate.IsUnlocked(2));
            Assert.IsTrue(changedCalled);
        }
    }
}
