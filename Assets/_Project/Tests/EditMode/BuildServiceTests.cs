using System;
using System.Collections.Generic;
using AutoService.Domain.Building;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Services.Building;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="BuildService"/> with a real wallet and bus and a scripted unlock gate.</summary>
    public sealed class BuildServiceTests
    {
        private const string OilPlot = "loc1_build_oil";
        private const string ParkingPlot = "loc1_build_parking_3";
        private const long OilCost = 500;
        private const int OilLevel = 3;

        private EventBus _bus;
        private WalletService _wallet;
        private FakeUnlockGate _gate;
        private BuildService _service;

        private readonly List<BuildCompletedEvent> _completed = new List<BuildCompletedEvent>();
        private readonly List<BuildPlot> _built = new List<BuildPlot>();
        private readonly List<BuildPlot> _restored = new List<BuildPlot>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(new FakeGameLogger());
            _gate = new FakeUnlockGate();
            _completed.Clear();
            _built.Clear();
            _restored.Clear();
            _bus.Subscribe<BuildCompletedEvent>(OnCompleted);
            CreateService(Money.Zero);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Unsubscribe<BuildCompletedEvent>(OnCompleted);
            _wallet.Dispose();
        }

        [Test]
        public void NotEnoughMoney_IsReported_AndNothingIsSpent()
        {
            CreateService(new Money(OilCost - 1));

            Assert.AreEqual(BuildAvailability.NotEnoughMoney, _service.GetAvailability(OilPlot));
            Assert.IsFalse(_service.TryBuild(OilPlot));
            Assert.AreEqual(new Money(OilCost - 1), _wallet.Balance);
            Assert.IsFalse(Plot(OilPlot).IsBuilt);
            Assert.AreEqual(0, _built.Count);
            Assert.AreEqual(0, _completed.Count);
        }

        [Test]
        public void TryBuild_Pays_MarksBuilt_AndNotifies()
        {
            CreateService(new Money(OilCost + 50));

            Assert.AreEqual(BuildAvailability.Available, _service.GetAvailability(OilPlot));
            Assert.IsTrue(_service.TryBuild(OilPlot));

            Assert.AreEqual(new Money(50), _wallet.Balance);
            Assert.IsTrue(Plot(OilPlot).IsBuilt);
            Assert.AreEqual(BuildAvailability.Built, _service.GetAvailability(OilPlot));
            Assert.AreEqual(1, _built.Count);
            Assert.AreSame(Plot(OilPlot), _built[0]);
            Assert.AreEqual(1, _completed.Count);
            Assert.AreEqual(OilPlot, _completed[0].PlotId);
            Assert.AreEqual(BuildableKind.ServicePoint, _completed[0].Kind);
            Assert.AreEqual("loc1_oil_1", _completed[0].TargetId);
            CollectionAssert.AreEqual(new[] { OilPlot }, _service.BuiltPlotIds);
        }

        [Test]
        public void TryBuild_Twice_ReturnsFalse_AndChargesOnce()
        {
            CreateService(new Money(OilCost * 2));

            Assert.IsTrue(_service.TryBuild(OilPlot));
            Assert.IsFalse(_service.TryBuild(OilPlot));

            Assert.AreEqual(new Money(OilCost), _wallet.Balance);
            Assert.AreEqual(1, _built.Count);
            Assert.AreEqual(1, _completed.Count);
        }

        [Test]
        public void LockedPlot_CannotBeBuilt_EvenWithMoney()
        {
            CreateService(new Money(OilCost * 2));
            _gate.UnlockUpTo(OilLevel - 1);

            Assert.AreEqual(BuildAvailability.Locked, _service.GetAvailability(OilPlot));
            Assert.IsFalse(_service.TryBuild(OilPlot));
            Assert.AreEqual(new Money(OilCost * 2), _wallet.Balance);
            Assert.AreEqual(BuildAvailability.Available, _service.GetAvailability(ParkingPlot), "Level 2 is open.");

            _gate.UnlockUpTo(OilLevel);
            Assert.AreEqual(BuildAvailability.Available, _service.GetAvailability(OilPlot));
        }

        [Test]
        public void RestoreBuilt_MarksWithoutPayment_AndSkipsUnknownIds()
        {
            CreateService(Money.Zero);

            _service.RestoreBuilt(new[] { ParkingPlot, "removed_plot", ParkingPlot });

            Assert.IsTrue(Plot(ParkingPlot).IsBuilt);
            Assert.IsFalse(Plot(OilPlot).IsBuilt);
            Assert.AreEqual(Money.Zero, _wallet.Balance);
            Assert.AreEqual(1, _restored.Count, "Duplicates are restored once.");
            Assert.AreEqual(0, _built.Count, "A restore is not a construction.");
            Assert.AreEqual(0, _completed.Count);
            CollectionAssert.AreEqual(new[] { ParkingPlot }, _service.BuiltPlotIds);
        }

        [Test]
        public void UnknownPlot_IsRejected()
        {
            Assert.IsFalse(_service.TryBuild("missing"));
            Assert.IsFalse(_service.TryGet("missing", out _));
            Assert.Throws<ArgumentException>(() => _service.GetAvailability("missing"));
        }

        [Test]
        public void Register_RejectsDuplicateIds()
        {
            Assert.Throws<InvalidOperationException>(() => _service.Register(OilDefinition()));
        }

        [Test]
        public void Definition_ValidatesParkingSlotTargets()
        {
            Assert.AreEqual(2, ParkingDefinition().ParkingSlotIndex);
            Assert.AreEqual(BuildPlotDefinition.NoSlot, OilDefinition().ParkingSlotIndex);
            Assert.Throws<ArgumentException>(
                () => new BuildPlotDefinition("p", BuildableKind.ParkingSlot, "-1", Money.Zero, 0, 0.0));
            Assert.Throws<ArgumentException>(
                () => new BuildPlotDefinition("p", BuildableKind.ParkingSlot, "two", Money.Zero, 0, 0.0));
            Assert.Throws<ArgumentException>(
                () => new BuildPlotDefinition("p", BuildableKind.ServicePoint, " ", Money.Zero, 0, 0.0));
            Assert.Throws<ArgumentException>(
                () => new BuildPlotDefinition("p", BuildableKind.ServicePoint, "x", Money.Zero, 0, double.NaN));
        }

        [Test]
        public void BuildPlot_MarkBuiltTwice_Throws()
        {
            var plot = new BuildPlot(OilDefinition());
            plot.MarkBuilt();

            Assert.Throws<InvalidOperationException>(plot.MarkBuilt);
        }

        private void CreateService(Money startingMoney)
        {
            if (_service != null)
            {
                _service.Built -= OnBuilt;
                _service.BuiltRestored -= OnRestored;
                _wallet.Dispose();
            }

            _wallet = new WalletService(new Wallet(startingMoney), _bus);
            _service = new BuildService(_wallet, _gate, _bus);
            _service.Register(OilDefinition());
            _service.Register(ParkingDefinition());
            _service.Built += OnBuilt;
            _service.BuiltRestored += OnRestored;
        }

        private static BuildPlotDefinition OilDefinition() =>
            new BuildPlotDefinition(OilPlot, BuildableKind.ServicePoint, "loc1_oil_1", new Money(OilCost), OilLevel, 0.2);

        private static BuildPlotDefinition ParkingDefinition() =>
            new BuildPlotDefinition(ParkingPlot, BuildableKind.ParkingSlot, "2", new Money(200), 2, 0.05);

        private BuildPlot Plot(string plotId)
        {
            Assert.IsTrue(_service.TryGet(plotId, out BuildPlot plot));
            return plot;
        }

        private void OnCompleted(BuildCompletedEvent gameEvent) => _completed.Add(gameEvent);

        private void OnBuilt(BuildPlot plot) => _built.Add(plot);

        private void OnRestored(BuildPlot plot) => _restored.Add(plot);
    }
}
