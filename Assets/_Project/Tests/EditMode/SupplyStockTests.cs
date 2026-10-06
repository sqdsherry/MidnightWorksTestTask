using System;
using AutoService.Domain.Supplies;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="SupplyStock"/> and <see cref="SupplyBox"/>.</summary>
    public sealed class SupplyStockTests
    {
        private SupplyStock _stock;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            _stock = new SupplyStock("shampoo", 10);
            _changes = 0;
            _stock.Changed += stock => _changes++;
        }

        [Test]
        public void NewStock_IsFull()
        {
            Assert.AreEqual(10, _stock.Current);
            Assert.AreEqual(1f, _stock.Fill01);
            Assert.IsFalse(_stock.IsEmpty);
            Assert.IsFalse(_stock.CanAdd(1));
            Assert.IsTrue(_stock.CanAdd(0));
        }

        [Test]
        public void Constructor_RejectsEmptyTypeAndNonPositiveCapacity()
        {
            Assert.Throws<ArgumentException>(() => new SupplyStock(" ", 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyStock("oil", 0));
        }

        [Test]
        public void TryConsume_UsesOneUnit_UntilEmpty()
        {
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(_stock.TryConsume());
            }

            Assert.IsTrue(_stock.IsEmpty);
            Assert.AreEqual(0f, _stock.Fill01);
            Assert.IsFalse(_stock.TryConsume());
            Assert.AreEqual(10, _changes, "No change event for a failed consume.");
        }

        [Test]
        public void Add_FillsWhenItFits()
        {
            ConsumeUnits(6);

            Assert.IsTrue(_stock.CanAdd(5));
            Assert.IsFalse(_stock.CanAdd(7));
            _stock.Add(5);

            Assert.AreEqual(9, _stock.Current);
            Assert.AreEqual(7, _changes);
        }

        [Test]
        public void Add_Overflow_Throws_AndChangesNothing()
        {
            ConsumeUnits(3);

            Assert.Throws<InvalidOperationException>(() => _stock.Add(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => _stock.Add(-1));
            Assert.AreEqual(7, _stock.Current);
            Assert.IsFalse(_stock.CanAdd(-1));
        }

        [Test]
        public void Restore_Clamps()
        {
            _stock.Restore(4);
            Assert.AreEqual(4, _stock.Current);

            _stock.Restore(-3);
            Assert.AreEqual(0, _stock.Current);

            _stock.Restore(99);
            Assert.AreEqual(10, _stock.Current);
            Assert.AreEqual(3, _changes);
        }

        [Test]
        public void SupplyBox_None_IsEmptyHands()
        {
            Assert.IsTrue(SupplyBox.None.IsNone);
            Assert.IsFalse(new SupplyBox("oil", 5).IsNone);
            Assert.AreEqual(new SupplyBox("oil", 5), new SupplyBox("oil", 5));
            Assert.Throws<ArgumentException>(() => new SupplyBox("", 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SupplyBox("oil", 0));
        }

        private void ConsumeUnits(int units)
        {
            for (int i = 0; i < units; i++)
            {
                _stock.TryConsume();
            }
        }
    }
}
