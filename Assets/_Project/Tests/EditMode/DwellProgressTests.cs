using System;
using AutoService.Domain.Common;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="DwellProgress"/>.</summary>
    public sealed class DwellProgressTests
    {
        private const float Duration = 1.5f;
        private const float Tolerance = 0.0001f;

        [Test]
        public void Dwelling_FillsOverTheDuration_AndCompletesOnce()
        {
            var dwell = new DwellProgress(Duration);
            dwell.Begin();

            Assert.IsFalse(dwell.Tick(0.75f));
            Assert.AreEqual(0.5f, dwell.Progress01, Tolerance);

            Assert.IsTrue(dwell.Tick(0.75f));
            Assert.AreEqual(1f, dwell.Progress01, Tolerance);

            Assert.IsFalse(dwell.Tick(1f), "Completion is reported once per visit.");
            Assert.AreEqual(1f, dwell.Progress01, Tolerance);
        }

        [Test]
        public void Leaving_DrainsThreeTimesFaster_AndKeepsTheRest()
        {
            var dwell = new DwellProgress(Duration);
            dwell.Begin();
            dwell.Tick(0.9f);
            dwell.End();

            dwell.Tick(0.1f);
            Assert.AreEqual(0.6f - 0.2f, dwell.Progress01, Tolerance);

            dwell.Begin();
            Assert.IsFalse(dwell.Tick(0.3f));
            Assert.AreEqual(0.6f, dwell.Progress01, Tolerance, "Refills from where it was.");

            dwell.End();
            dwell.Tick(10f);
            Assert.AreEqual(0f, dwell.Progress01, "Never below zero.");
        }

        [Test]
        public void NextVisit_CanCompleteAgain()
        {
            var dwell = new DwellProgress(Duration);
            dwell.Begin();
            Assert.IsTrue(dwell.Tick(Duration));
            dwell.End();
            dwell.Tick(0.1f);

            dwell.Begin();

            Assert.IsTrue(dwell.Tick(Duration));
        }

        [Test]
        public void ZeroDelta_ChangesNothing()
        {
            var dwell = new DwellProgress(Duration);
            dwell.Begin();

            Assert.IsFalse(dwell.Tick(0f));
            Assert.AreEqual(0f, dwell.Progress01);
        }

        [Test]
        public void ZeroDuration_CompletesOnTheFirstTick()
        {
            var dwell = new DwellProgress(0f);
            dwell.Begin();

            Assert.IsTrue(dwell.Tick(0.01f));
        }

        [Test]
        public void Reset_EmptiesTheProgress()
        {
            var dwell = new DwellProgress(Duration);
            dwell.Begin();
            dwell.Tick(1f);

            dwell.Reset();

            Assert.AreEqual(0f, dwell.Progress01);
            Assert.IsTrue(dwell.IsDwelling);
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DwellProgress(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DwellProgress(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DwellProgress(1f, 0f));
        }
    }
}
