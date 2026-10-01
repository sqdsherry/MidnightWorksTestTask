using System;
using System.Collections.Generic;
using AutoService.Domain.Staff;
using AutoService.Presentation.Points;
using AutoService.Presentation.Supplies;
using AutoService.Presentation.Traffic;
using AutoService.Services.Core;
using AutoService.Services.Staff;
using UnityEngine;

namespace AutoService.Presentation.Staff
{
    /// <summary>
    /// <see cref="IStaffAgents"/> of one location: instantiates a <see cref="StaffView"/> per hired NPC in the staff room
    /// (no pool — there are only a few, TDD T6) and walks it to the scene transforms behind <see cref="StaffDestination"/>s.
    /// </summary>
    /// <remarks>
    /// Destinations: work spot = the point view's work spot, warehouse = the warehouse's approach point, supply drop = the
    /// approach point of the point's blue pad (next to the point, never on its work spot).
    /// <para>Arrivals are collected during the tick and raised after the pass over the NPCs (handlers react with
    /// <see cref="MoveTo"/>), never synchronously. A missing prefab or an unresolvable destination is logged and reported
    /// as reached on the next tick, so the staff logic never stalls.</para>
    /// <para>Steady state is allocation-free.</para>
    /// </remarks>
    public sealed class StaffAgents : IStaffAgents, ITickable, IDisposable
    {
        private readonly LocationLayout _layout;
        private readonly StaffView _prefab;
        private readonly StaffVisualCatalog _staffVisuals;
        private readonly SupplyVisualCatalog _supplyVisuals;
        private readonly Transform _root;
        private readonly Dictionary<int, Body> _bodies = new Dictionary<int, Body>();
        private readonly List<int> _ids = new List<int>();

        // Why: two buffers swapped per tick — arrivals queued by handlers while raising go to the next tick's batch.
        private List<int> _pendingArrivals = new List<int>();
        private List<int> _raisingArrivals = new List<int>();
        private bool _disposed;

        /// <summary>Creates the agents of <paramref name="layout"/>'s location.</summary>
        /// <param name="layout">Markup with the staff room, the warehouse, the points and their pads.</param>
        /// <param name="prefab">NPC body prefab; may be null (invisible NPCs, logged).</param>
        /// <param name="staffVisuals">Body material per role; may be null.</param>
        /// <param name="supplyVisuals">Box colors; may be null.</param>
        /// <param name="root">Parent of the instances; may be null (scene root).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> is null.</exception>
        public StaffAgents(LocationLayout layout, StaffView prefab, StaffVisualCatalog staffVisuals, SupplyVisualCatalog supplyVisuals, Transform root)
        {
            _layout = layout != null ? layout : throw new ArgumentNullException(nameof(layout));
            _prefab = prefab;
            _staffVisuals = staffVisuals;
            _supplyVisuals = supplyVisuals;
            _root = root;
        }

        /// <inheritdoc />
        public event Action<int> Arrived;

        /// <inheritdoc />
        public void Spawn(int staffId, StaffRole role, string locationId)
        {
            if (_bodies.ContainsKey(staffId))
            {
                Debug.LogError("[StaffAgents] Staff " + staffId + " is already spawned.");
                return;
            }

            if (!string.Equals(locationId, _layout.LocationId, StringComparison.Ordinal))
            {
                Debug.LogError("[StaffAgents] Location '" + _layout.LocationId + "' cannot spawn staff of location '" + locationId + "'.", _layout);
            }

            StaffView view = null;
            if (_prefab != null)
            {
                Transform door = _layout.StaffRoom;
                Vector3 position = door != null ? door.position : _layout.transform.position;
                Quaternion rotation = door != null ? door.rotation : Quaternion.identity;
                view = UnityEngine.Object.Instantiate(_prefab, position, rotation, _root);
                view.name = _prefab.name + "_" + role + "_" + staffId;
                view.Place(position, rotation);
                view.SetBodyMaterial(_staffVisuals != null ? _staffVisuals.MaterialOf(role) : null);
            }
            else
            {
                Debug.LogError("[StaffAgents] No staff prefab assigned; staff " + staffId + " (" + role + ") is invisible.");
            }

            _bodies.Add(staffId, new Body(view));
            _ids.Add(staffId);
        }

        /// <inheritdoc />
        public void MoveTo(int staffId, StaffDestination destination)
        {
            if (!_bodies.TryGetValue(staffId, out Body body))
            {
                Debug.LogError("[StaffAgents] MoveTo for unknown staff " + staffId + ".");
                return;
            }

            if (body.View == null)
            {
                _pendingArrivals.Add(staffId);
                return;
            }

            Transform target = Resolve(destination);
            if (target == null)
            {
                Debug.LogError("[StaffAgents] Location '" + _layout.LocationId + "' cannot resolve " + destination + " for staff "
                    + staffId + "; reporting it as reached.", _layout);
                _pendingArrivals.Add(staffId);
                return;
            }

            if (!body.View.WalkTo(target))
            {
                Debug.LogWarning("[StaffAgents] No NavMesh path to " + destination + " for staff " + staffId + "; snapped there.", target);
                _pendingArrivals.Add(staffId);
            }
        }

        /// <inheritdoc />
        public void SetCarried(int staffId, string supplyTypeIdOrEmpty)
        {
            if (!_bodies.TryGetValue(staffId, out Body body) || body.Box == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(supplyTypeIdOrEmpty))
            {
                body.Box.Hide();
            }
            else
            {
                body.Box.Show(_supplyVisuals != null ? _supplyVisuals.ColorOf(supplyTypeIdOrEmpty) : Color.white);
            }
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            for (int i = 0; i < _ids.Count; i++)
            {
                int staffId = _ids[i];
                StaffView view = _bodies[staffId].View;
                if (view != null && view.TickArrival(deltaTime))
                {
                    _pendingArrivals.Add(staffId);
                }
            }

            if (_pendingArrivals.Count == 0)
            {
                return;
            }

            List<int> raising = _pendingArrivals;
            _pendingArrivals = _raisingArrivals;
            _raisingArrivals = raising;
            for (int i = 0; i < raising.Count; i++)
            {
                int staffId = raising[i];

                // Why: an earlier handler in this batch may have sent the NPC somewhere else already.
                if (_bodies.TryGetValue(staffId, out Body body) && (body.View == null || !body.View.IsMoving))
                {
                    Arrived?.Invoke(staffId);
                }
            }

            raising.Clear();
        }

        /// <summary>Forgets the NPCs (their objects go with the scene). Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _bodies.Clear();
            _ids.Clear();
        }

        private Transform Resolve(in StaffDestination destination)
        {
            switch (destination.Kind)
            {
                case StaffDestinationKind.WorkSpot:
                    return _layout.TryGetPointView(destination.Id, out ServicePointView point) ? point.WorkSpot : null;
                case StaffDestinationKind.SupplyDrop:
                    return _layout.TryGetManagePad(destination.Id, out ManagePadView pad) ? pad.ApproachPoint : null;
                case StaffDestinationKind.Warehouse:
                    return string.Equals(destination.Id, _layout.LocationId, StringComparison.Ordinal) && _layout.Warehouse != null
                        ? _layout.Warehouse.ApproachPoint
                        : null;
                default:
                    return null;
            }
        }

        /// <summary>A spawned NPC: its body (null when there is no prefab) and the box in its hands.</summary>
        private readonly struct Body
        {
            public Body(StaffView view)
            {
                View = view;
                Box = view != null ? new CarriedBoxView(view.BoxRenderer) : null;
            }

            public StaffView View { get; }

            public CarriedBoxView Box { get; }
        }
    }
}
