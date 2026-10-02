using System;
using AutoService.Presentation.Controls;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="EscapeRouter"/>.</summary>
    public sealed class EscapeRouterTests
    {
        private EscapeRouter _router;
        private int _unhandled;

        [SetUp]
        public void SetUp()
        {
            _router = new EscapeRouter();
            _unhandled = 0;
            _router.Unhandled += () => _unhandled++;
        }

        [Test]
        public void NoHandlers_RaisesUnhandled()
        {
            _router.HandleEscape();

            Assert.AreEqual(1, _unhandled);
        }

        [Test]
        public void Handler_TakesEscape_UnhandledNotRaised()
        {
            var panel = new FakeHandler(handles: true);
            _router.Push(panel);

            _router.HandleEscape();

            Assert.AreEqual(1, panel.Calls);
            Assert.AreEqual(0, _unhandled);
        }

        [Test]
        public void RemovedHandler_IsNotCalled()
        {
            var panel = new FakeHandler(handles: true);
            _router.Push(panel);
            _router.Remove(panel);

            _router.HandleEscape();

            Assert.AreEqual(0, panel.Calls);
            Assert.AreEqual(1, _unhandled);
        }

        [Test]
        public void OnlyHandlerDeclines_EscapeReachesPause()
        {
            // An open panel whose object is behind the camera declines Esc.
            var hiddenPanel = new FakeHandler(handles: false);
            _router.Push(hiddenPanel);

            _router.HandleEscape();

            Assert.AreEqual(1, hiddenPanel.Calls);
            Assert.AreEqual(1, _unhandled);
        }

        [Test]
        public void TopHandler_GoesFirst()
        {
            var bottom = new FakeHandler(handles: true);
            var top = new FakeHandler(handles: true);
            _router.Push(bottom);
            _router.Push(top);

            _router.HandleEscape();

            Assert.AreEqual(1, top.Calls);
            Assert.AreEqual(0, bottom.Calls);
        }

        [Test]
        public void HandlerThatDeclines_PassesEscapeDown()
        {
            var bottom = new FakeHandler(handles: true);
            var top = new FakeHandler(handles: false);
            _router.Push(bottom);
            _router.Push(top);

            _router.HandleEscape();

            Assert.AreEqual(1, top.Calls);
            Assert.AreEqual(1, bottom.Calls);
            Assert.AreEqual(0, _unhandled);
        }

        [Test]
        public void PushAgain_MovesHandlerToTopOnce()
        {
            var first = new FakeHandler(handles: true);
            var second = new FakeHandler(handles: true);
            _router.Push(first);
            _router.Push(second);

            _router.Push(first);

            Assert.AreEqual(2, _router.Count);
            _router.HandleEscape();
            Assert.AreEqual(1, first.Calls);
            Assert.AreEqual(0, second.Calls);
        }

        [Test]
        public void HandlerClosingItself_NextEscapeIsUnhandled()
        {
            var panel = new FakeHandler(handles: true);
            panel.OnHandle = () => _router.Remove(panel);
            _router.Push(panel);

            _router.HandleEscape();
            _router.HandleEscape();

            Assert.AreEqual(1, panel.Calls);
            Assert.AreEqual(1, _unhandled);
        }

        private sealed class FakeHandler : IEscapeHandler
        {
            private readonly bool _handles;

            public FakeHandler(bool handles)
            {
                _handles = handles;
            }

            public int Calls { get; private set; }

            public Action OnHandle { get; set; }

            public bool TryHandleEscape()
            {
                Calls++;
                OnHandle?.Invoke();
                return _handles;
            }
        }
    }
}
