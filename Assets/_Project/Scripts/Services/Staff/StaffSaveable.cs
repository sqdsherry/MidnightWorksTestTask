using System;
using System.Collections.Generic;
using AutoService.Domain.Points;
using AutoService.Domain.Staff;
using AutoService.Services.Core;
using AutoService.Services.Points;
using AutoService.Services.Save;

namespace AutoService.Services.Staff
{
    /// <summary>
    /// Saves and restores the storekeepers (<see cref="SaveData.storekeeperLocationIds"/>: the location id once per
    /// storekeeper). Point workers are saved per point by <see cref="PointsSaveable"/>.
    /// </summary>
    public sealed class StaffSaveable : ISaveable
    {
        private readonly IStaffService _staff;
        private readonly IServicePointService _points;
        private readonly UnknownSaveIds _unknown;
        private readonly List<string> _ids = new List<string>();

        /// <summary>Creates the saveable.</summary>
        /// <param name="staff">Hired staff and restoring.</param>
        /// <param name="points">Registered points: a location is known when it has at least one.</param>
        /// <param name="logger">Receives warnings about unknown locations.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public StaffSaveable(IStaffService staff, IServicePointService points, IGameLogger logger)
        {
            _staff = staff ?? throw new ArgumentNullException(nameof(staff));
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _unknown = new UnknownSaveIds(logger, "location");
        }

        /// <inheritdoc />
        public void Capture(SaveData data)
        {
            _ids.Clear();
            IReadOnlyList<StaffMember> members = _staff.Staff;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Role == StaffRole.Storekeeper)
                {
                    _ids.Add(members[i].LocationId);
                }
            }

            data.storekeeperLocationIds = _ids.ToArray();
        }

        /// <inheritdoc />
        public void Restore(SaveData data)
        {
            string[] ids = data.storekeeperLocationIds;
            if (ids == null)
            {
                return;
            }

            for (int i = 0; i < ids.Length; i++)
            {
                if (IsKnownLocation(ids[i]))
                {
                    _staff.RestoreStorekeeper(ids[i]);
                }
                else
                {
                    _unknown.Report(ids[i]);
                }
            }
        }

        private bool IsKnownLocation(string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
            {
                return false;
            }

            IReadOnlyList<ServicePoint> all = _points.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (string.Equals(all[i].Definition.LocationId, locationId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
