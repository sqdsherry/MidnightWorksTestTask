using AutoService.Services.Save;
using AutoService.Services.Staff;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="StaffSaveable"/>.</summary>
    public sealed class StaffSaveableTests
    {
        [Test]
        public void TwoStorekeepers_AreSavedOnceEach_AndRestoredAsTwo()
        {
            SaveData data = SaveData.CreateEmpty();
            using (var original = new SaveTestWorld())
            {
                Assert.IsTrue(original.Staff.TryHireStorekeeper(SaveTestWorld.LocationId));
                Assert.IsTrue(original.Staff.TryHireStorekeeper(SaveTestWorld.LocationId));
                new StaffSaveable(original.Staff, original.Points, original.Logger).Capture(data);
            }

            CollectionAssert.AreEqual(new[] { SaveTestWorld.LocationId, SaveTestWorld.LocationId }, data.storekeeperLocationIds);

            using (var world = new SaveTestWorld())
            {
                new StaffSaveable(world.Staff, world.Points, world.Logger).Restore(data);

                Assert.AreEqual(2, world.Staff.StorekeeperCount(SaveTestWorld.LocationId));
            }
        }

        [Test]
        public void UnknownLocation_IsSkipped_WithWarning()
        {
            SaveData data = SaveData.CreateEmpty();
            data.storekeeperLocationIds = new[] { "loc_gone" };

            using (var world = new SaveTestWorld())
            {
                new StaffSaveable(world.Staff, world.Points, world.Logger).Restore(data);

                Assert.AreEqual(0, world.Staff.Staff.Count);
                Assert.AreEqual(1, world.Logger.Warnings.Count);
            }
        }
    }
}
