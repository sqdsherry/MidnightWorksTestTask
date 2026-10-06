using System;
using AutoService.Services.Events;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="EventBus"/>.</summary>
    public sealed class EventBusTests
    {
        private FakeGameLogger _logger;
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _logger = new FakeGameLogger();
            _bus = new EventBus(_logger);
        }

        [Test]
        public void Publish_DeliversEventToSubscriber()
        {
            int received = 0;
            _bus.Subscribe<TestEvent>(e => received = e.Value);

            _bus.Publish(new TestEvent(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new TestEvent(1)));
        }

        [Test]
        public void Publish_OnlyReachesSubscribersOfThatType()
        {
            int calls = 0;
            _bus.Subscribe<OtherEvent>(_ => calls++);

            _bus.Publish(new TestEvent(1));

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            int calls = 0;
            Action<TestEvent> handler = _ => calls++;
            _bus.Subscribe(handler);

            _bus.Unsubscribe(handler);
            _bus.Publish(new TestEvent(1));

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Unsubscribe_UnknownHandler_IsIgnored()
        {
            Assert.DoesNotThrow(() => _bus.Unsubscribe<TestEvent>(_ => { }));
        }

        [Test]
        public void UnsubscribeInsideHandler_DoesNotBreakCurrentDelivery()
        {
            int firstCalls = 0;
            int secondCalls = 0;
            Action<TestEvent> second = _ => secondCalls++;
            Action<TestEvent> first = null;
            first = _ =>
            {
                firstCalls++;
                _bus.Unsubscribe(first);
                _bus.Unsubscribe(second);
            };
            _bus.Subscribe(first);
            _bus.Subscribe(second);

            _bus.Publish(new TestEvent(1));
            _bus.Publish(new TestEvent(2));

            // The ongoing delivery used the old snapshot, so "second" still got event 1; nobody gets event 2.
            Assert.AreEqual(1, firstCalls);
            Assert.AreEqual(1, secondCalls);
        }

        [Test]
        public void SubscribeInsideHandler_TakesEffectFromNextPublish()
        {
            int lateCalls = 0;
            Action<TestEvent> late = _ => lateCalls++;
            _bus.Subscribe<TestEvent>(_ => _bus.Subscribe(late));

            _bus.Publish(new TestEvent(1));
            Assert.AreEqual(0, lateCalls);

            _bus.Publish(new TestEvent(2));
            Assert.AreEqual(1, lateCalls);
        }

        [Test]
        public void ThrowingHandler_IsLoggedAndOthersStillReceive()
        {
            int calls = 0;
            _bus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
            _bus.Subscribe<TestEvent>(_ => calls++);

            _bus.Publish(new TestEvent(1));

            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, _logger.Errors.Count);
            StringAssert.Contains("boom", _logger.Errors[0]);
        }

        [Test]
        public void DuplicateSubscription_IsDeliveredOnce()
        {
            int calls = 0;
            Action<TestEvent> handler = _ => calls++;
            _bus.Subscribe(handler);
            _bus.Subscribe(handler);

            _bus.Publish(new TestEvent(1));

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Subscribe_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _bus.Subscribe<TestEvent>(null));
        }

        private readonly struct TestEvent
        {
            public TestEvent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private readonly struct OtherEvent
        {
        }
    }
}
