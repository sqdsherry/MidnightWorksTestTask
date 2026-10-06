using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Economy;
using AutoService.Domain.Points;
using AutoService.Domain.Supplies;
using AutoService.Services.Config;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Supplies;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="SupplyService"/> and <see cref="PlayerCarry"/> with real points, wallet and bus.</summary>
    public sealed class SupplyServiceTests
    {
        private const string LocationId = "loc1";
        private const string Wash1 = "loc1_wash_1";
        private const string Wash2 = "loc1_wash_2";
        private const string Oil = "loc1_oil_1";
        private const string Barrier = "loc1_entrance_main";
        private const int Capacity = 10;
        private const int UnitsPerBox = 5;

        private EventBus _bus;
        private WalletService _wallet;
        private ServicePointService _points;
        private FakeConfigProvider _config;
        private SupplyService _service;

        private readonly List<BoxBoughtEvent> _bought = new List<BoxBoughtEvent>();
        private readonly List<SupplyDeliveredEvent> _delivered = new List<SupplyDeliveredEvent>();
        private readonly List<SupplyDepletedEvent> _depleted = new List<SupplyDepletedEvent>();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus(new FakeGameLogger());
            _wallet = new WalletService(new Wallet(new Money(100)), _bus);
            _points = new ServicePointService(_wallet, _bus);
            _config = new FakeConfigProvider();
            _config.SupplyTypeList.Add(new SupplyTypeSettings("shampoo", "Shampoo", new Money(5), UnitsPerBox));
            _config.SupplyTypeList.Add(new SupplyTypeSettings("oil", "Oil", new Money(15), UnitsPerBox));
            var wash = new ServiceTypeSettings("wash", "Wash", PointKind.Service, new Money(12), 0.0, 1f, 0f, 0f, "shampoo", Capacity, null, 0);
            var oil = new ServiceTypeSettings("oil", "Oil", PointKind.Service, new Money(25), 0.0, 1f, 0f, 0f, "oil", Capacity, null, 0);
            var parking = new ServiceTypeSettings("parking", "Parking", PointKind.Barrier, new Money(2), 0.0, 1f, 0f, 0f, "", 0, null, 0);
            _config.ServiceTypeList.Add(wash);
            _config.ServiceTypeList.Add(oil);
            _config.ServiceTypeList.Add(parking);

            _points.Register(parking.CreatePointDefinition(Barrier, LocationId));
            _points.Register(wash.CreatePointDefinition(Wash1, LocationId));
            _service = new SupplyService(_points, _wallet, _config, _bus);

            // Registered after the service: it must pick up points built later, too.
            _points.Register(wash.CreatePointDefinition(Wash2, LocationId));
            _points.Register(oil.CreatePointDefinition(Oil, LocationId));
            _points.Register(wash.CreatePointDefinition("loc2_wash", "loc2"));

            _bought.Clear();
            _delivered.Clear();
            _depleted.Clear();
            _bus.Subscribe<BoxBoughtEvent>(OnBought);
            _bus.Subscribe<SupplyDeliveredEvent>(OnDelivered);
            _bus.Subscribe<SupplyDepletedEvent>(OnDepleted);
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Unsubscribe<BoxBoughtEvent>(OnBought);
            _bus.Unsubscribe<SupplyDeliveredEvent>(OnDelivered);
            _bus.Unsubscribe<SupplyDepletedEvent>(OnDepleted);
            _service.Dispose();
            _points.Dispose();
            _wallet.Dispose();
        }

        [Test]
        public void FullPoints_AreNeverHungriest()
        {
            Assert.IsNull(_service.FindHungriest(LocationId));

            Consume(Wash1, UnitsPerBox - 1);
            Assert.IsNull(_service.FindHungriest(LocationId), "A box of 5 does not fit into 6/10 + 5.");
        }

        [Test]
        public void Hungriest_IsTheLowestFill_OfThisLocation()
        {
            Consume(Wash1, 5);
            Consume(Oil, 8);
            Consume("loc2_wash", 10);

            Assert.AreEqual(Oil, _service.FindHungriest(LocationId).Definition.Id);
            Assert.AreEqual("loc2_wash", _service.FindHungriest("loc2").Definition.Id);
        }

        [Test]
        public void Hungriest_CountsIncomingBoxes()
        {
            Consume(Wash1, 8);
            Consume(Wash2, 6);

            _service.MarkIncoming(Wash1, UnitsPerBox);
            Assert.AreEqual(Wash2, _service.FindHungriest(LocationId).Definition.Id, "Wash 1 is 2 + 5 incoming = 7/10.");

            _service.ClearIncoming(Wash1, UnitsPerBox);
            Assert.AreEqual(Wash1, _service.FindHungriest(LocationId).Definition.Id);
        }

        [Test]
        public void Buy_ChargesThePrice_AndReturnsABoxForTheHungriest()
        {
            Consume(Oil, 6);

            Assert.IsTrue(_service.TryBuyBoxForHungriest(LocationId, true, out SupplyBox box, out ServicePoint target));

            Assert.AreEqual(Oil, target.Definition.Id);
            Assert.AreEqual(new SupplyBox("oil", UnitsPerBox), box);
            Assert.AreEqual(new Money(85), _wallet.Balance);
            Assert.AreEqual(1, _bought.Count);
            Assert.AreEqual(new Money(15), _bought[0].Price);
            Assert.IsTrue(_bought[0].ByPlayer);
        }

        [Test]
        public void Buy_WithoutMoney_FailsWithoutCharging()
        {
            Consume(Oil, 6);
            _wallet.TrySpend(new Money(90));

            Assert.IsFalse(_service.TryBuyBoxForHungriest(LocationId, false, out SupplyBox box, out _));

            Assert.IsTrue(box.IsNone);
            Assert.AreEqual(new Money(10), _wallet.Balance);
            Assert.AreEqual(0, _bought.Count);
        }

        [Test]
        public void Buy_WithNothingToRestock_Fails()
        {
            Assert.IsFalse(_service.TryBuyBoxForHungriest(LocationId, true, out _, out ServicePoint target));
            Assert.IsNull(target);
            Assert.AreEqual(new Money(100), _wallet.Balance);
        }

        [Test]
        public void Deliver_FillsTheStock_AndPublishes()
        {
            Consume(Wash2, 7);
            var box = new SupplyBox("shampoo", UnitsPerBox);

            Assert.IsTrue(_service.CanDeliver(box, Wash2));
            Assert.IsTrue(_service.TryDeliver(box, Wash2, true));

            Assert.AreEqual(8, Stock(Wash2).Current);
            Assert.AreEqual(1, _delivered.Count);
            Assert.AreEqual(Wash2, _delivered[0].PointId);
            Assert.AreEqual(UnitsPerBox, _delivered[0].Units);
        }

        [Test]
        public void Deliver_OfAnotherTypeOrWithoutRoom_Fails()
        {
            Consume(Wash1, 7);
            Consume(Oil, 2);

            Assert.IsFalse(_service.TryDeliver(new SupplyBox("oil", UnitsPerBox), Wash1, true), "Wrong consumable.");
            Assert.IsFalse(_service.TryDeliver(new SupplyBox("oil", UnitsPerBox), Oil, true), "8/10 + 5 does not fit.");
            Assert.IsFalse(_service.TryDeliver(new SupplyBox("shampoo", UnitsPerBox), Barrier, true), "Barriers have no stock.");
            Assert.IsFalse(_service.TryDeliver(SupplyBox.None, Wash1, true));
            Assert.AreEqual(3, Stock(Wash1).Current);
            Assert.AreEqual(0, _delivered.Count);
        }

        [Test]
        public void RestockTarget_MatchesTheBoxType()
        {
            Consume(Wash1, 9);
            Consume(Oil, 6);

            ServicePoint target = _service.FindRestockTarget(LocationId, new SupplyBox("oil", UnitsPerBox));

            Assert.AreEqual(Oil, target.Definition.Id);
            Assert.IsNull(_service.FindRestockTarget(LocationId, new SupplyBox("tires", UnitsPerBox)));
        }

        [Test]
        public void EmptyStock_PublishesDepleted()
        {
            Consume(Wash2, Capacity);

            Assert.AreEqual(1, _depleted.Count);
            Assert.AreEqual(Wash2, _depleted[0].PointId);
            Assert.AreEqual("shampoo", _depleted[0].SupplyTypeId);
        }

        [Test]
        public void BoxPrice_ComesFromTheConfig()
        {
            Assert.AreEqual(new Money(15), _service.GetBoxPrice("oil"));
            Assert.Throws<ArgumentException>(() => _service.GetBoxPrice("tires"));
        }

        [Test]
        public void PlayerCarry_HoldsOneBox()
        {
            var carry = new PlayerCarry();
            int changes = 0;
            carry.Changed += () => changes++;
            var box = new SupplyBox("oil", UnitsPerBox);

            Assert.IsTrue(carry.TryPick(box));
            Assert.IsFalse(carry.TryPick(new SupplyBox("shampoo", UnitsPerBox)), "Hands full.");
            Assert.IsTrue(carry.HasBox);
            Assert.AreEqual(box, carry.Drop());
            Assert.IsFalse(carry.HasBox);
            Assert.IsTrue(carry.Drop().IsNone);
            Assert.AreEqual(2, changes);
        }

        private SupplyStock Stock(string pointId)
        {
            Assert.IsTrue(_points.TryGet(pointId, out ServicePoint point));
            return point.Supply;
        }

        private void Consume(string pointId, int units)
        {
            SupplyStock stock = Stock(pointId);
            for (int i = 0; i < units; i++)
            {
                stock.TryConsume();
            }
        }

        private void OnBought(BoxBoughtEvent gameEvent) => _bought.Add(gameEvent);

        private void OnDelivered(SupplyDeliveredEvent gameEvent) => _delivered.Add(gameEvent);

        private void OnDepleted(SupplyDepletedEvent gameEvent) => _depleted.Add(gameEvent);
    }
}
