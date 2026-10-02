using AutoService.Domain.Building;
using AutoService.Domain.Common;
using AutoService.Domain.Upgrades;
using AutoService.Services.Save;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>
    /// Integration: a world changed in every saved way is captured through the coordinator and restored into a brand-new
    /// world, which must end up the same — including the bay that only exists because its plot was built.
    /// </summary>
    public sealed class SaveRoundTripTests
    {
        [Test]
        public void EverythingSaved_ComesBackInANewWorld()
        {
            var storage = new FakeSaveService();
            Money balance;
            using (var original = new SaveTestWorld())
            {
                Assert.IsTrue(original.Build.TryBuild(SaveTestWorld.Plot2));
                Assert.IsTrue(original.Upgrades.TryUpgrade(SaveTestWorld.Wash1, UpgradeKind.Speed));
                Assert.IsTrue(original.Upgrades.TryUpgrade(SaveTestWorld.Wash2, UpgradeKind.Price));
                Assert.IsTrue(original.Upgrades.TryUpgrade(SaveTestWorld.Wash2, UpgradeKind.Price));
                original.Point(SaveTestWorld.Wash1).Supply.Restore(3);
                original.Point(SaveTestWorld.Wash2).Supply.Restore(8);
                Assert.IsTrue(original.Staff.TryHireWorker(SaveTestWorld.Wash2));
                Assert.IsTrue(original.Staff.TryHireStorekeeper(SaveTestWorld.LocationId));
                balance = original.Wallet.Balance;

                original.CreateCoordinator(storage).SaveNow();
            }

            using (var world = new SaveTestWorld(startingMoney: 100))
            {
                Assert.IsTrue(world.CreateCoordinator(storage).TryRestore());

                Assert.AreEqual(balance, world.Wallet.Balance);
                Assert.IsTrue(world.Build.TryGet(SaveTestWorld.Plot2, out BuildPlot plot) && plot.IsBuilt);
                Assert.AreEqual(1, world.Upgrades.GetLevel(SaveTestWorld.Wash1, UpgradeKind.Speed));
                Assert.AreEqual(0, world.Upgrades.GetLevel(SaveTestWorld.Wash1, UpgradeKind.Price));
                Assert.AreEqual(2, world.Upgrades.GetLevel(SaveTestWorld.Wash2, UpgradeKind.Price));
                Assert.AreEqual(3, world.Point(SaveTestWorld.Wash1).Supply.Current);
                Assert.AreEqual(8, world.Point(SaveTestWorld.Wash2).Supply.Current);
                Assert.IsFalse(world.Staff.HasWorker(SaveTestWorld.Wash1));
                Assert.IsTrue(world.Staff.HasWorker(SaveTestWorld.Wash2));
                Assert.AreEqual(1, world.Staff.StorekeeperCount(SaveTestWorld.LocationId));
                Assert.AreEqual(2, world.Staff.Staff.Count);
                Assert.IsEmpty(world.Logger.Warnings);
                Assert.IsEmpty(world.Logger.Errors);
            }
        }

        [Test]
        public void NoSave_RestoresNothing()
        {
            using (var world = new SaveTestWorld(startingMoney: 100))
            {
                Assert.IsFalse(world.CreateCoordinator(new FakeSaveService()).TryRestore());

                Assert.AreEqual(new Money(100), world.Wallet.Balance);
                Assert.AreEqual(0, world.Build.BuiltPlotIds.Count);
            }
        }
    }
}
