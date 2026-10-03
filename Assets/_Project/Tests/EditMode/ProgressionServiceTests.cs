using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Points;
using AutoService.Domain.Progression;
using AutoService.Services.Config;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Progression;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    public sealed class ProgressionServiceTests
    {
        private FakeConfigProvider _config;
        private EventBus _eventBus;
        private PlayerProgress _progress;
        private ProgressionService _service;

        [SetUp]
        public void SetUp()
        {
            _config = new FakeConfigProvider();
            _config.ServiceTypeList.Add(new ServiceTypeSettings("wash", "Wash", PointKind.Service, new Money(10), 0, 5, 0, 0, "", 0, null, 2));
            _config.ServiceTypeList.Add(new ServiceTypeSettings("parking", "Parking", PointKind.Barrier, new Money(5), 0, 5, 0, 0, "", 0, null, 0));
            
            _eventBus = new EventBus(new FakeGameLogger());
            _progress = new PlayerProgress(new LevelTable(new[] { 0, 15, 35 }, 50));
            _service = new ProgressionService(_progress, _config, _eventBus);
        }

        [Test]
        public void ServiceCompletedEvent_WithXpReward_AddsXp()
        {
            _eventBus.Publish(new ServiceCompletedEvent("point1", "wash"));
            Assert.AreEqual(2, _service.Xp);
        }

        [Test]
        public void ServiceCompletedEvent_UnknownType_AddsNoXp()
        {
            _eventBus.Publish(new ServiceCompletedEvent("point1", "unknown"));
            Assert.AreEqual(0, _service.Xp);
        }

        [Test]
        public void LevelUp_PublishesLevelUpEvent()
        {
            var events = new List<LevelUpEvent>();
            _eventBus.Subscribe<LevelUpEvent>(e => events.Add(e));

            _progress.AddXp(15);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(2, events[0].NewLevel);
        }

        [Test]
        public void Dispose_UnsubscribesFromEvents()
        {
            _service.Dispose();
            
            _eventBus.Publish(new ServiceCompletedEvent("point1", "wash"));
            Assert.AreEqual(0, _service.Xp);

            var events = new List<LevelUpEvent>();
            _eventBus.Subscribe<LevelUpEvent>(e => events.Add(e));
            _progress.AddXp(15);
            Assert.AreEqual(0, events.Count);
        }
    }
}
