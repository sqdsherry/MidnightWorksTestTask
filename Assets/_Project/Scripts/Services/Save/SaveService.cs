using System;
using AutoService.Services.Core;

namespace AutoService.Services.Save
{
    /// <summary>
    /// Default <see cref="ISaveService"/>: serializes through <see cref="ISaveSerializer"/>, stores through
    /// <see cref="ISaveStorage"/> and validates the version of everything it loads.
    /// </summary>
    public sealed class SaveService : ISaveService
    {
        private readonly ISaveStorage _storage;
        private readonly ISaveSerializer _serializer;
        private readonly ITimeProvider _timeProvider;
        private readonly IGameLogger _logger;

        /// <summary>Creates the service.</summary>
        /// <param name="storage">Where the serialized save lives.</param>
        /// <param name="serializer">Converts snapshots to and from text.</param>
        /// <param name="timeProvider">Source of the <see cref="SaveData.savedAtUtcTicks"/> stamp.</param>
        /// <param name="logger">Receives a warning for every save that cannot be loaded.</param>
        public SaveService(ISaveStorage storage, ISaveSerializer serializer, ITimeProvider timeProvider, IGameLogger logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public bool HasSave => _storage.Exists;

        /// <inheritdoc />
        /// <remarks>
        /// A corrupted save is reported but NOT deleted: the caller decides whether to start over or to keep the file
        /// (e.g. so a bug in this build does not wipe the player's progress).
        /// </remarks>
        public bool TryLoad(out SaveData data)
        {
            data = null;

            // No save is a normal first launch, not worth a warning; I/O errors are logged by the storage itself.
            if (!_storage.TryRead(out string json))
            {
                return false;
            }

            if (!_serializer.TryDeserialize(json, out SaveData loaded) || loaded == null)
            {
                _logger.Warning("[Save] The save is corrupted and cannot be parsed; it was left untouched.");
                return false;
            }

            if (loaded.version > SaveData.CurrentVersion)
            {
                _logger.Warning("[Save] The save has version " + loaded.version + ", newer than the supported "
                    + SaveData.CurrentVersion + "; it was left untouched.");
                return false;
            }

            if (loaded.version < 1)
            {
                _logger.Warning("[Save] The save has invalid version " + loaded.version + "; it was left untouched.");
                return false;
            }

            loaded = Migrate(loaded);
            NormalizeArrays(loaded);
            data = loaded;
            return true;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public void Save(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.version = SaveData.CurrentVersion;
            data.savedAtUtcTicks = _timeProvider.UtcNow.Ticks;
            _storage.Write(_serializer.Serialize(data));
        }

        /// <inheritdoc />
        public void Delete() => _storage.Delete();

        // Why: the extension point for format changes. Each future step upgrades one version to the next
        // (e.g. "if (data.version == 1) { ...; data.version = 2; }"), so an old save reaches CurrentVersion
        // before any saveable sees it. Version 1 is the first format, so there are no steps yet.
        private SaveData Migrate(SaveData data)
        {
            return data;
        }

        // Why: JsonUtility (or a hand-edited file) can leave arrays null when their keys are missing;
        // saveables are promised non-null arrays so none of them needs its own null checks.
        private static void NormalizeArrays(SaveData data)
        {
            data.builtPlotIds = data.builtPlotIds ?? Array.Empty<string>();
            data.points = data.points ?? Array.Empty<PointSaveData>();
            data.storekeeperLocationIds = data.storekeeperLocationIds ?? Array.Empty<string>();
            data.unlockedLocationIds = data.unlockedLocationIds ?? Array.Empty<string>();
        }
    }
}
