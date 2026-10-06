using System;
using AutoService.Domain.Traffic;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="ParkingLot"/>.</summary>
    public sealed class ParkingLotTests
    {
        [Test]
        public void Reserve_TakesFirstFreeSlotUntilFull()
        {
            var lot = new ParkingLot(2);

            Assert.IsTrue(lot.TryReserve(10, out int first));
            Assert.IsTrue(lot.TryReserve(11, out int second));
            Assert.IsFalse(lot.TryReserve(12, out int third));

            Assert.AreEqual(0, first);
            Assert.AreEqual(1, second);
            Assert.AreEqual(ParkingLot.None, third);
            Assert.AreEqual(0, lot.FreeCount);
            Assert.AreEqual(11, lot.CarAt(1));
        }

        [Test]
        public void Release_FreesSlotForReuse()
        {
            var lot = new ParkingLot(2);
            lot.TryReserve(10, out _);
            lot.TryReserve(11, out _);

            lot.Release(0);
            lot.Release(0);

            Assert.AreEqual(1, lot.FreeCount);
            Assert.AreEqual(ParkingLot.None, lot.CarAt(0));
            Assert.IsTrue(lot.TryReserve(12, out int slot));
            Assert.AreEqual(0, slot);
        }

        [Test]
        public void SetCapacity_GrowsAndKeepsReservations()
        {
            var lot = new ParkingLot(1);
            lot.TryReserve(10, out _);

            lot.SetCapacity(3);

            Assert.AreEqual(3, lot.Capacity);
            Assert.AreEqual(2, lot.FreeCount);
            Assert.AreEqual(10, lot.CarAt(0));
            Assert.AreEqual(ParkingLot.None, lot.CarAt(2));
        }

        [Test]
        public void SetCapacity_Shrink_Throws()
        {
            var lot = new ParkingLot(2);

            Assert.Throws<ArgumentOutOfRangeException>(() => lot.SetCapacity(1));
        }

        [Test]
        public void ZeroCapacity_NeverReserves()
        {
            var lot = new ParkingLot(0);

            Assert.IsFalse(lot.TryReserve(1, out _));
        }

        [Test]
        public void Release_OutOfRange_Throws()
        {
            var lot = new ParkingLot(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => lot.Release(1));
        }
    }
}
