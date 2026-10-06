using AutoService.Domain.Building;
using AutoService.Domain.Common;
using AutoService.Services.Building;
using AutoService.Services.Save;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="BuildSaveable"/>.</summary>
    public sealed class BuildSaveableTests
    {
        [Test]
        public void RestoredPlot_IsBuiltWithoutPayment_AndRegistersItsBay()
        {
            SaveData data = SaveData.CreateEmpty();
            using (var original = new SaveTestWorld())
            {
                Assert.IsTrue(original.Build.TryBuild(SaveTestWorld.Plot2));
                new BuildSaveable(original.Build, original.Logger).Capture(data);
            }

            using (var world = new SaveTestWorld(startingMoney: 100))
            {
                new BuildSaveable(world.Build, world.Logger).Restore(data);

                CollectionAssert.AreEqual(new[] { SaveTestWorld.Plot2 }, data.builtPlotIds);
                Assert.IsTrue(world.Build.TryGet(SaveTestWorld.Plot2, out BuildPlot plot) && plot.IsBuilt);
                Assert.AreEqual(new Money(100), world.Wallet.Balance, "Restoring is free.");
                Assert.IsNotNull(world.Point(SaveTestWorld.Wash2), "The bay of the plot is registered.");
            }
        }

        [Test]
        public void UnknownPlot_IsSkipped_WithOneWarning()
        {
            SaveData data = SaveData.CreateEmpty();
            data.builtPlotIds = new[] { "plot_gone", SaveTestWorld.Plot2, "plot_gone" };

            using (var world = new SaveTestWorld())
            {
                new BuildSaveable(world.Build, world.Logger).Restore(data);

                Assert.IsTrue(world.Build.TryGet(SaveTestWorld.Plot2, out BuildPlot plot) && plot.IsBuilt);
                Assert.AreEqual(1, world.Build.BuiltPlotIds.Count);
                Assert.AreEqual(1, world.Logger.Warnings.Count, "One warning per unknown id.");
            }
        }

        [Test]
        public void MissingArray_RestoresNothing()
        {
            SaveData data = SaveData.CreateEmpty();
            data.builtPlotIds = null;

            using (var world = new SaveTestWorld())
            {
                new BuildSaveable(world.Build, world.Logger).Restore(data);

                Assert.AreEqual(0, world.Build.BuiltPlotIds.Count);
            }
        }
    }
}
