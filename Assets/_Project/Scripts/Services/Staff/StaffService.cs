using System;
using System.Collections.Generic;
using AutoService.Domain.Common;
using AutoService.Domain.Points;
using AutoService.Domain.Staff;
using AutoService.Domain.Supplies;
using AutoService.Services.Building;
using AutoService.Services.Config;
using AutoService.Services.Core;
using AutoService.Services.Economy;
using AutoService.Services.Events;
using AutoService.Services.Points;
using AutoService.Services.Supplies;

namespace AutoService.Services.Staff
{
    /// <summary>
    /// Default <see cref="IStaffService"/>: hires staff (unlock gate + wallet), spawns their bodies through
    /// <see cref="IStaffAgents"/> and drives their job FSMs (<see cref="StaffMember"/>) from arrivals and the tick.
    /// </summary>
    /// <remarks>
    /// <para><b>Worker:</b> hire → pay → spawn → walk to the work spot → occupy it as <see cref="OccupantKind.Worker"/>.
    /// If the player stands there, the worker waits and retries every tick. Why: pushing the player off would desync the
    /// character's own FSM; the point view stops offering the spot to the player once a worker is hired, so the player
    /// cannot take it again after leaving.</para>
    /// <para><b>Storekeeper:</b> idle until a point of its location drops below the restock threshold → warehouse → buy the
    /// box for the hungriest point (no money: wait, retry every second) → carry it there (counted as incoming) → deliver.
    /// If the target filled up meanwhile, the box goes to another point that can take it, otherwise it is dropped.</para>
    /// <para>The tick only walks the staff list and calls allocation-free lookups.</para>
    /// </remarks>
    public sealed class StaffService : IStaffService, ITickable, IDisposable
    {
        /// <summary>Seconds between purchase attempts of a storekeeper waiting for money.</summary>
        public const float MoneyRetryInterval = 1f;

        private readonly IServicePointService _points;
        private readonly ISupplyService _supplies;
        private readonly IWalletService _wallet;
        private readonly IUnlockGate _gate;
        private readonly IStaffAgents _agents;
        private readonly IConfigProvider _config;
        private readonly IEventBus _eventBus;

        private readonly List<StaffMember> _staff = new List<StaffMember>();
        private readonly List<StaffEntry> _entries = new List<StaffEntry>();
        private readonly Dictionary<int, StaffEntry> _entriesById = new Dictionary<int, StaffEntry>();
        private readonly Dictionary<string, StaffMember> _workersByPoint = new Dictionary<string, StaffMember>(StringComparer.Ordinal);
        private readonly Dictionary<string, StaffMember> _storekeepersByLocation = new Dictionary<string, StaffMember>(StringComparer.Ordinal);

        private int _nextId;
        private bool _disposed;

        /// <summary>Creates the service and starts listening to the agents' arrivals.</summary>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is null.</exception>
        public StaffService(
            IServicePointService points,
            ISupplyService supplies,
            IWalletService wallet,
            IUnlockGate gate,
            IStaffAgents agents,
            IConfigProvider config,
            IEventBus eventBus)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _supplies = supplies ?? throw new ArgumentNullException(nameof(supplies));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _agents = agents ?? throw new ArgumentNullException(nameof(agents));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _agents.Arrived += OnArrived;
        }

        /// <inheritdoc />
        public event Action<StaffMember> Hired;

        /// <inheritdoc />
        public IReadOnlyList<StaffMember> Staff => _staff;

        /// <inheritdoc />
        public HireAvailability GetWorkerAvailability(string pointId)
        {
            if (!TryGetWorkerSettings(pointId, out _, out PointWorkerSettings worker))
            {
                return HireAvailability.NotSupported;
            }

            return Availability(_workersByPoint.ContainsKey(pointId), worker.RequiredLevel, worker.HireCost);
        }

        /// <inheritdoc />
        public Money GetWorkerCost(string pointId)
        {
            return TryGetWorkerSettings(pointId, out _, out PointWorkerSettings worker) ? worker.HireCost : Money.Zero;
        }

        /// <inheritdoc />
        public bool TryHireWorker(string pointId)
        {
            if (GetWorkerAvailability(pointId) != HireAvailability.Available)
            {
                return false;
            }

            TryGetWorkerSettings(pointId, out ServicePoint point, out PointWorkerSettings worker);

            // Why: affordability was just checked, but the wallet stays the single authority on spending.
            if (!_wallet.TrySpend(worker.HireCost))
            {
                return false;
            }

            StaffMember member = AddWorker(point);
            _eventBus.Publish(new StaffHiredEvent(member.Role, member.LocationId, member.AssignedPointId));
            return true;
        }

        /// <inheritdoc />
        public bool HasWorker(string pointId) => pointId != null && _workersByPoint.ContainsKey(pointId);

        /// <inheritdoc />
        public HireAvailability GetStorekeeperAvailability(string locationId)
        {
            StaffSettings staff = _config.Staff;
            if (string.IsNullOrEmpty(locationId) || staff == null)
            {
                return HireAvailability.NotSupported;
            }

            return Availability(_storekeepersByLocation.ContainsKey(locationId), staff.StorekeeperRequiredLevel, staff.StorekeeperCost);
        }

        /// <inheritdoc />
        public Money GetStorekeeperCost(string locationId) => _config.Staff?.StorekeeperCost ?? Money.Zero;

        /// <inheritdoc />
        public bool TryHireStorekeeper(string locationId)
        {
            if (GetStorekeeperAvailability(locationId) != HireAvailability.Available || !_wallet.TrySpend(_config.Staff.StorekeeperCost))
            {
                return false;
            }

            StaffMember member = AddStorekeeper(locationId);
            _eventBus.Publish(new StaffHiredEvent(member.Role, member.LocationId, null));
            return true;
        }

        /// <inheritdoc />
        public void RestoreWorker(string pointId)
        {
            if (TryGetWorkerSettings(pointId, out ServicePoint point, out _) && !_workersByPoint.ContainsKey(pointId))
            {
                AddWorker(point);
            }
        }

        /// <inheritdoc />
        public void RestoreStorekeeper(string locationId)
        {
            if (!string.IsNullOrEmpty(locationId) && !_storekeepersByLocation.ContainsKey(locationId))
            {
                AddStorekeeper(locationId);
            }
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            // Why: dt is 0 while paused; the staff must not take spots or go shopping during the pause.
            if (deltaTime <= 0f)
            {
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                StaffEntry entry = _entries[i];
                StaffMember member = entry.Member;
                switch (member.State)
                {
                    case StaffState.WaitingForSpot:
                        if (_points.TryOccupy(member.AssignedPointId, OccupantKind.Worker))
                        {
                            member.TakeSpot();
                        }

                        break;
                    case StaffState.Idle:
                        TickIdleStorekeeper(member);
                        break;
                    case StaffState.WaitingForMoney:
                        entry.RetryTimer -= deltaTime;
                        if (entry.RetryTimer <= 0f)
                        {
                            BuyAtWarehouse(entry);
                        }

                        break;
                }
            }
        }

        /// <summary>Stops listening to the agents. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _agents.Arrived -= OnArrived;
        }

        private HireAvailability Availability(bool hired, int requiredLevel, Money cost)
        {
            if (hired)
            {
                return HireAvailability.Hired;
            }

            if (!_gate.IsUnlocked(requiredLevel))
            {
                return HireAvailability.Locked;
            }

            return _wallet.CanAfford(cost) ? HireAvailability.Available : HireAvailability.NotEnoughMoney;
        }

        private bool TryGetWorkerSettings(string pointId, out ServicePoint point, out PointWorkerSettings worker)
        {
            worker = null;
            if (!_points.TryGet(pointId, out point)
                || !_config.TryGetServiceType(point.Definition.ServiceTypeId, out ServiceTypeSettings settings))
            {
                return false;
            }

            worker = settings.Worker;
            return worker != null;
        }

        private StaffMember AddWorker(ServicePoint point)
        {
            ServicePointDefinition definition = point.Definition;
            StaffMember member = StaffMember.CreateWorker(_nextId++, definition.LocationId, definition.Id);
            _workersByPoint.Add(definition.Id, member);
            Add(member);
            _agents.MoveTo(member.Id, StaffDestination.WorkSpot(definition.Id));
            return member;
        }

        private StaffMember AddStorekeeper(string locationId)
        {
            StaffMember member = StaffMember.CreateStorekeeper(_nextId++, locationId);
            _storekeepersByLocation.Add(locationId, member);
            Add(member);
            return member;
        }

        private void Add(StaffMember member)
        {
            var entry = new StaffEntry(member);
            _staff.Add(member);
            _entries.Add(entry);
            _entriesById.Add(member.Id, entry);
            _agents.Spawn(member.Id, member.Role, member.LocationId);
            Hired?.Invoke(member);
        }

        private void TickIdleStorekeeper(StaffMember member)
        {
            ServicePoint hungriest = _supplies.FindHungriest(member.LocationId);
            if (hungriest == null || hungriest.Supply.Fill01 >= _config.Staff.RestockThreshold)
            {
                return;
            }

            member.GoToWarehouse();
            _agents.MoveTo(member.Id, StaffDestination.Warehouse(member.LocationId));
        }

        private void OnArrived(int staffId)
        {
            if (!_entriesById.TryGetValue(staffId, out StaffEntry entry))
            {
                return;
            }

            StaffMember member = entry.Member;
            switch (member.State)
            {
                case StaffState.WalkingToSpot:
                    member.ArriveAtSpot(_points.TryOccupy(member.AssignedPointId, OccupantKind.Worker));
                    break;
                case StaffState.ToWarehouse:
                    BuyAtWarehouse(entry);
                    break;
                case StaffState.ToPoint:
                    Deliver(member);
                    break;
            }
        }

        private void BuyAtWarehouse(StaffEntry entry)
        {
            StaffMember member = entry.Member;
            if (_supplies.TryBuyBoxForHungriest(member.LocationId, false, out SupplyBox box, out ServicePoint target))
            {
                string targetId = target.Definition.Id;
                _supplies.MarkIncoming(targetId, box.Units);
                member.PickUp(box, targetId);
                _agents.SetCarried(member.Id, box.SupplyTypeId);
                _agents.MoveTo(member.Id, StaffDestination.SupplyDrop(targetId));
                return;
            }

            // Why: the purchase fails for two reasons — nothing left to restock (the player did it) or no money.
            if (_supplies.FindHungriest(member.LocationId) == null)
            {
                member.CancelRestock();
                return;
            }

            if (member.State == StaffState.ToWarehouse)
            {
                member.WaitForMoney();
            }

            entry.RetryTimer = MoneyRetryInterval;
        }

        private void Deliver(StaffMember member)
        {
            SupplyBox box = member.CarriedBox;
            string targetId = member.TargetPointId;
            _supplies.ClearIncoming(targetId, box.Units);
            if (_supplies.TryDeliver(box, targetId, false))
            {
                member.ReleaseBox();
                _agents.SetCarried(member.Id, string.Empty);
                return;
            }

            // Why: the player filled the target meanwhile; a paid box is not thrown away while another point can use it.
            ServicePoint other = _supplies.FindRestockTarget(member.LocationId, box);
            if (other != null)
            {
                string otherId = other.Definition.Id;
                _supplies.MarkIncoming(otherId, box.Units);
                member.Redirect(otherId);
                _agents.MoveTo(member.Id, StaffDestination.SupplyDrop(otherId));
                return;
            }

            member.ReleaseBox();
            _agents.SetCarried(member.Id, string.Empty);
        }

        /// <summary>A hired member plus the service-side timer of its money retries.</summary>
        private sealed class StaffEntry
        {
            public StaffEntry(StaffMember member)
            {
                Member = member;
            }

            public StaffMember Member { get; }

            public float RetryTimer { get; set; }
        }
    }
}
