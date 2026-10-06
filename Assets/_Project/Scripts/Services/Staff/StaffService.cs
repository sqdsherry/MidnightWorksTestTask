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
    /// <para><b>Storekeepers</b> (up to <see cref="StaffSettings.MaxStorekeepers"/> per location, each with its own waiting
    /// spot at the warehouse): waiting (or walking) home until the hungriest point holds
    /// <see cref="StaffSettings.RestockAtOrBelow"/> units or fewer → the point is reserved at once (its box counts as
    /// incoming, so a second storekeeper deciding in the same tick picks another point) → warehouse → the target is
    /// re-checked (the player may have restocked it) → buy the box (no money: wait, retry every second) → carry it there →
    /// deliver → walk home. If the target filled up meanwhile, the box goes to another point that can take it, otherwise
    /// it is dropped.</para>
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
        private readonly IStaffAgents _defaultAgents;
        private readonly Dictionary<string, IStaffAgents> _agentsByLocation = new Dictionary<string, IStaffAgents>(StringComparer.Ordinal);
        private readonly IConfigProvider _config;
        private readonly IEventBus _eventBus;
        private readonly IGameLogger _logger;

        private readonly List<StaffMember> _staff = new List<StaffMember>();
        private readonly List<StaffEntry> _entries = new List<StaffEntry>();
        private readonly Dictionary<int, StaffEntry> _entriesById = new Dictionary<int, StaffEntry>();
        private readonly Dictionary<string, StaffMember> _workersByPoint = new Dictionary<string, StaffMember>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _storekeeperCounts = new Dictionary<string, int>(StringComparer.Ordinal);

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
            IEventBus eventBus,
            IGameLogger logger)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _supplies = supplies ?? throw new ArgumentNullException(nameof(supplies));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _defaultAgents = agents;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_defaultAgents != null)
            {
                _defaultAgents.Arrived += OnArrived;
            }
        }

        /// <summary>Registers an agent handler for a specific location.</summary>
        public void RegisterAgents(string locationId, IStaffAgents agents)
        {
            if (string.IsNullOrEmpty(locationId) || agents == null)
            {
                return;
            }

            if (!_agentsByLocation.ContainsKey(locationId))
            {
                _agentsByLocation.Add(locationId, agents);
                agents.Arrived += OnArrived;
            }
        }

        private IStaffAgents GetAgents(string locationId)
        {
            if (locationId != null && _agentsByLocation.TryGetValue(locationId, out IStaffAgents agents))
            {
                return agents;
            }

            return _defaultAgents;
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

            int hired = StorekeeperCount(locationId);
            return Availability(hired >= staff.MaxStorekeepers, staff.StorekeeperRequiredLevel, staff.StorekeeperCostAt(hired));
        }

        /// <inheritdoc />
        public Money GetStorekeeperCost(string locationId)
        {
            StaffSettings staff = _config.Staff;
            return staff != null ? staff.StorekeeperCostAt(StorekeeperCount(locationId)) : Money.Zero;
        }

        /// <inheritdoc />
        public int StorekeeperCount(string locationId)
        {
            return locationId != null && _storekeeperCounts.TryGetValue(locationId, out int count) ? count : 0;
        }

        /// <inheritdoc />
        public bool TryHireStorekeeper(string locationId)
        {
            if (GetStorekeeperAvailability(locationId) != HireAvailability.Available || !_wallet.TrySpend(GetStorekeeperCost(locationId)))
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
            if (string.IsNullOrEmpty(locationId))
            {
                return;
            }

            // Why: same guard as GetStorekeeperAvailability — without staff settings the storekeeper has no restock rule.
            if (_config.Staff == null)
            {
                _logger.Warning("[Staff] Storekeeper of '" + locationId + "' is not restored: GameConfig has no staff settings.");
                return;
            }

            // Why: a save from a config that allowed more storekeepers keeps only as many as are allowed now.
            if (StorekeeperCount(locationId) < _config.Staff.MaxStorekeepers)
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
                    case StaffState.ReturningHome:
                        TickWaitingStorekeeper(entry);
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
            if (_defaultAgents != null)
            {
                _defaultAgents.Arrived -= OnArrived;
            }

            foreach (IStaffAgents agents in _agentsByLocation.Values)
            {
                if (agents != null && agents != _defaultAgents)
                {
                    agents.Arrived -= OnArrived;
                }
            }

            _agentsByLocation.Clear();
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
            GetAgents(member.LocationId)?.MoveTo(member.Id, StaffDestination.WorkSpot(definition.Id));
            return member;
        }

        private StaffMember AddStorekeeper(string locationId)
        {
            // Why: the n-th storekeeper of a location waits at spot n, so they never stand on each other.
            int index = StorekeeperCount(locationId);
            StaffMember member = StaffMember.CreateStorekeeper(_nextId++, locationId, index);
            _storekeeperCounts[locationId] = index + 1;
            Add(member);
            GetAgents(member.LocationId)?.MoveTo(member.Id, StaffDestination.Home(locationId, index));
            return member;
        }

        private void Add(StaffMember member)
        {
            var entry = new StaffEntry(member);
            _staff.Add(member);
            _entries.Add(entry);
            _entriesById.Add(member.Id, entry);
            GetAgents(member.LocationId)?.Spawn(member.Id, member.Role, member.LocationId);
            Hired?.Invoke(member);
        }

        // Why: also while walking home — a hungry point turns the storekeeper straight back to the warehouse.
        private void TickWaitingStorekeeper(StaffEntry entry)
        {
            StaffMember member = entry.Member;
            if (!TryFindRestockTarget(member.LocationId, out ServicePoint target))
            {
                return;
            }

            string targetId = target.Definition.Id;
            member.GoToWarehouse(targetId);
            Reserve(entry, target);
            GetAgents(member.LocationId)?.MoveTo(member.Id, StaffDestination.Warehouse(member.LocationId));
        }

        /// <summary>
        /// The point a storekeeper should restock: the hungriest one, if it holds <see cref="StaffSettings.RestockAtOrBelow"/>
        /// units or fewer, counting the boxes other storekeepers already bring.
        /// </summary>
        private bool TryFindRestockTarget(string locationId, out ServicePoint point)
        {
            point = _supplies.FindHungriest(locationId);
            StaffSettings staff = _config.Staff;
            if (point != null
                && staff != null
                && point.Supply.Current + _supplies.GetIncoming(point.Definition.Id) <= staff.RestockAtOrBelow)
            {
                return true;
            }

            point = null;
            return false;
        }

        // Why: reserved when the storekeeper decides, not when it buys — otherwise two storekeepers deciding in the same
        // tick would both walk for the same point.
        private void Reserve(StaffEntry entry, ServicePoint target)
        {
            int units = _config.TryGetSupplyType(target.Supply.SupplyTypeId, out SupplyTypeSettings settings) ? settings.UnitsPerBox : 0;
            _supplies.MarkIncoming(target.Definition.Id, units);
            entry.ReservedUnits = units;
        }

        private void ReleaseReservation(StaffEntry entry)
        {
            _supplies.ClearIncoming(entry.Member.TargetPointId, entry.ReservedUnits);
            entry.ReservedUnits = 0;
        }

        private void GoHome(StaffMember member)
        {
            GetAgents(member.LocationId)?.MoveTo(member.Id, StaffDestination.Home(member.LocationId, member.HomeIndex));
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
                    Deliver(entry);
                    break;
                case StaffState.ReturningHome:
                    member.ArriveHome();
                    break;
            }
        }

        private void BuyAtWarehouse(StaffEntry entry)
        {
            StaffMember member = entry.Member;

            // Why: the target is checked again at the warehouse (without this storekeeper's own reservation) — the player
            // may have restocked it while the storekeeper walked; it must not buy a box for a point that is fine now (nor
            // wait for money for it). Another hungry point may take its place.
            ReleaseReservation(entry);
            if (!TryFindRestockTarget(member.LocationId, out ServicePoint target))
            {
                member.CancelRestock();
                GoHome(member);
                return;
            }

            string targetId = target.Definition.Id;
            member.Retarget(targetId);
            Reserve(entry, target);
            if (_supplies.TryBuyBoxFor(targetId, false, out SupplyBox box))
            {
                member.PickUp(box, targetId);
                GetAgents(member.LocationId)?.SetCarried(member.Id, box.SupplyTypeId);
                GetAgents(member.LocationId)?.MoveTo(member.Id, StaffDestination.SupplyDrop(targetId));
                return;
            }

            if (member.State == StaffState.ToWarehouse)
            {
                member.WaitForMoney();
            }

            entry.RetryTimer = MoneyRetryInterval;
        }

        private void Deliver(StaffEntry entry)
        {
            StaffMember member = entry.Member;
            SupplyBox box = member.CarriedBox;
            string targetId = member.TargetPointId;
            ReleaseReservation(entry);
            if (_supplies.TryDeliver(box, targetId, false))
            {
                member.ReleaseBox();
                GetAgents(member.LocationId)?.SetCarried(member.Id, string.Empty);
                GoHome(member);
                return;
            }

            // Why: the player filled the target meanwhile; a paid box is not thrown away while another point can use it.
            ServicePoint other = _supplies.FindRestockTarget(member.LocationId, box);
            if (other != null)
            {
                string otherId = other.Definition.Id;
                member.Redirect(otherId);
                _supplies.MarkIncoming(otherId, box.Units);
                entry.ReservedUnits = box.Units;
                GetAgents(member.LocationId)?.MoveTo(member.Id, StaffDestination.SupplyDrop(otherId));
                return;
            }

            member.ReleaseBox();
            GetAgents(member.LocationId)?.SetCarried(member.Id, string.Empty);
            GoHome(member);
        }

        /// <summary>A hired member plus service-side bookkeeping: the money retry timer and the units it reserved at its target.</summary>
        private sealed class StaffEntry
        {
            public StaffEntry(StaffMember member)
            {
                Member = member;
            }

            public StaffMember Member { get; }

            public float RetryTimer { get; set; }

            public int ReservedUnits { get; set; }
        }
    }
}
