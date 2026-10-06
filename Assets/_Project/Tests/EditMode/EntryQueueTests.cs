using System;
using AutoService.Domain.Traffic;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="EntryQueue"/>.</summary>
    public sealed class EntryQueueTests
    {
        [Test]
        public void Enqueue_FillsSlotsFromHeadUntilFull()
        {
            var queue = new EntryQueue(2);

            Assert.IsTrue(queue.TryEnqueue(10, out int first));
            Assert.IsTrue(queue.TryEnqueue(11, out int second));
            Assert.IsFalse(queue.TryEnqueue(12, out int third));

            Assert.AreEqual(0, first);
            Assert.AreEqual(1, second);
            Assert.AreEqual(EntryQueue.None, third);
            Assert.IsTrue(queue.IsFull);
            Assert.AreEqual(10, queue.Head);
        }

        [Test]
        public void RemoveHead_ShiftsCarsForwardAndRaisesShifted()
        {
            var queue = new EntryQueue(3);
            queue.TryEnqueue(10, out _);
            queue.TryEnqueue(11, out _);
            queue.TryEnqueue(12, out _);
            int shifted = 0;
            queue.Shifted += q => shifted++;

            queue.RemoveHead();

            Assert.AreEqual(1, shifted);
            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual(11, queue.Head);
            Assert.AreEqual(0, queue.SlotOf(11));
            Assert.AreEqual(1, queue.SlotOf(12));
            Assert.AreEqual(EntryQueue.None, queue.GetAt(2));
            Assert.AreEqual(EntryQueue.None, queue.SlotOf(10));
        }

        [Test]
        public void EmptyQueue_HeadIsNoneAndRemoveThrows()
        {
            var queue = new EntryQueue(1);

            Assert.AreEqual(EntryQueue.None, queue.Head);
            Assert.Throws<InvalidOperationException>(() => queue.RemoveHead());
        }

        [Test]
        public void Enqueue_SameCarTwice_Throws()
        {
            var queue = new EntryQueue(2);
            queue.TryEnqueue(10, out _);

            Assert.Throws<InvalidOperationException>(() => queue.TryEnqueue(10, out _));
        }

        [Test]
        public void Capacity_MustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EntryQueue(0));
        }
    }
}
