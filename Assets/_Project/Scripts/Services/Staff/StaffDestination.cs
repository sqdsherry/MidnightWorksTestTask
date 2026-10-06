using System;

namespace AutoService.Services.Staff
{
    /// <summary>
    /// Engine-agnostic "where to walk" for a staff NPC: a kind plus the id of a point or a location (and, for
    /// <see cref="StaffDestinationKind.Home"/>, the index of the storekeeper's spot). Presentation resolves it into a transform.
    /// </summary>
    public readonly struct StaffDestination : IEquatable<StaffDestination>
    {
        private StaffDestination(StaffDestinationKind kind, string id, int index = 0)
        {
            Kind = kind;
            Id = id;
            Index = index;
        }

        /// <summary>Kind of place.</summary>
        public StaffDestinationKind Kind { get; }

        /// <summary>Point id (work spot, supply drop) or location id (warehouse, home).</summary>
        public string Id { get; }

        /// <summary>Spot index of <see cref="StaffDestinationKind.Home"/>; 0 otherwise.</summary>
        public int Index { get; }

        /// <summary>The work spot of <paramref name="pointId"/>.</summary>
        public static StaffDestination WorkSpot(string pointId) => new StaffDestination(StaffDestinationKind.WorkSpot, pointId);

        /// <summary>The warehouse of <paramref name="locationId"/>.</summary>
        public static StaffDestination Warehouse(string locationId) => new StaffDestination(StaffDestinationKind.Warehouse, locationId);

        /// <summary>Waiting spot <paramref name="index"/> of the storekeepers of <paramref name="locationId"/>.</summary>
        public static StaffDestination Home(string locationId, int index) => new StaffDestination(StaffDestinationKind.Home, locationId, index);

        /// <summary>The box hand-over place of <paramref name="pointId"/>.</summary>
        public static StaffDestination SupplyDrop(string pointId) => new StaffDestination(StaffDestinationKind.SupplyDrop, pointId);

        /// <inheritdoc />
        public bool Equals(StaffDestination other) =>
            Kind == other.Kind && Index == other.Index && string.Equals(Id, other.Id, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is StaffDestination other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (((int)Kind * 397) ^ (Id?.GetHashCode() ?? 0)) * 31 + Index;

        /// <inheritdoc />
        public override string ToString() => Kind == StaffDestinationKind.Home ? Kind + "(" + Id + ", " + Index + ")" : Kind + "(" + Id + ")";
    }
}
