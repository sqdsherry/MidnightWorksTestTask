using System;
using System.Collections.Generic;
using AutoService.Domain.Staff;
using AutoService.Services.Staff;

namespace AutoService.Tests.EditMode
{
    /// <summary>Test double of the staff bodies: records commands; arrivals are raised explicitly by the test.</summary>
    public sealed class FakeStaffAgents : IStaffAgents
    {
        /// <inheritdoc />
        public event Action<int> Arrived;

        /// <summary>Spawned staff ids with their role, in order.</summary>
        public List<KeyValuePair<int, StaffRole>> Spawned { get; } = new List<KeyValuePair<int, StaffRole>>();

        /// <summary>Latest destination per staff id.</summary>
        public Dictionary<int, StaffDestination> Destinations { get; } = new Dictionary<int, StaffDestination>();

        /// <summary>Latest carried supply type per staff id ("" = empty hands).</summary>
        public Dictionary<int, string> Carried { get; } = new Dictionary<int, string>();

        /// <summary>Number of <see cref="Arrived"/> subscribers (to check unsubscription).</summary>
        public int SubscriberCount => Arrived == null ? 0 : Arrived.GetInvocationList().Length;

        /// <inheritdoc />
        public void Spawn(int staffId, StaffRole role, string locationId) => Spawned.Add(new KeyValuePair<int, StaffRole>(staffId, role));

        /// <inheritdoc />
        public void MoveTo(int staffId, StaffDestination destination) => Destinations[staffId] = destination;

        /// <inheritdoc />
        public void SetCarried(int staffId, string supplyTypeIdOrEmpty) => Carried[staffId] = supplyTypeIdOrEmpty;

        /// <summary>Simulates the NPC reaching its latest destination.</summary>
        public void Arrive(int staffId) => Arrived?.Invoke(staffId);
    }
}
