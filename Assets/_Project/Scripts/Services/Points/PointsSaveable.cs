using System;
using System.Collections.Generic;
using AutoService.Domain.Points;
using AutoService.Domain.Upgrades;
using AutoService.Services.Core;
using AutoService.Services.Save;
using AutoService.Services.Staff;
using AutoService.Services.Upgrades;

namespace AutoService.Services.Points
{
    /// <summary>
    /// Saves and restores everything per point (<see cref="SaveData.points"/>): upgrade levels, the supply left and
    /// whether a worker is hired.
    /// </summary>
    /// <remarks>
    /// Covers every registered point, including bays restored by <c>BuildSaveable</c> — which is why it must be restored
    /// after it.
    /// </remarks>
    public sealed class PointsSaveable : ISaveable
    {
        private readonly IServicePointService _points;
        private readonly IUpgradeService _upgrades;
        private readonly IStaffService _staff;
        private readonly UnknownSaveIds _unknown;

        /// <summary>Creates the saveable.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public PointsSaveable(IServicePointService points, IUpgradeService upgrades, IStaffService staff, IGameLogger logger)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
            _staff = staff ?? throw new ArgumentNullException(nameof(staff));
            _unknown = new UnknownSaveIds(logger, "service point");
        }

        /// <inheritdoc />
        public void Capture(SaveData data)
        {
            IReadOnlyList<ServicePoint> all = _points.All;
            var entries = new PointSaveData[all.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                ServicePoint point = all[i];
                string id = point.Definition.Id;
                entries[i] = new PointSaveData
                {
                    pointId = id,
                    speedLevel = _upgrades.GetLevel(id, UpgradeKind.Speed),
                    priceLevel = _upgrades.GetLevel(id, UpgradeKind.Price),
                    supply = point.Supply != null ? point.Supply.Current : PointSaveData.UnsavedSupply,
                    hasWorker = _staff.HasWorker(id),
                };
            }

            data.points = entries;
        }

        /// <inheritdoc />
        public void Restore(SaveData data)
        {
            PointSaveData[] entries = data.points;
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                PointSaveData entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (!_points.TryGet(entry.pointId, out ServicePoint point))
                {
                    _unknown.Report(entry.pointId);
                    continue;
                }

                _upgrades.Restore(entry.pointId, UpgradeKind.Speed, entry.speedLevel);
                _upgrades.Restore(entry.pointId, UpgradeKind.Price, entry.priceLevel);

                // Why: −1 marks "not saved" (a point that had no stock when the save was written) — keep the current fill.
                if (point.Supply != null && entry.supply >= 0)
                {
                    point.Supply.Restore(entry.supply);
                }

                if (entry.hasWorker)
                {
                    _staff.RestoreWorker(entry.pointId);
                }
            }
        }
    }
}
