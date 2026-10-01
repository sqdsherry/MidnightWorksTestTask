using System;
using System.Collections.Generic;
using AutoService.Services.Core;

namespace AutoService.Services.Save
{
    /// <summary>
    /// Collects the state of every registered <see cref="ISaveable"/> into one <see cref="SaveData"/> and hands it to
    /// <see cref="ISaveService"/>: on demand, on a deferred request and on a periodic autosave.
    /// </summary>
    /// <remarks>
    /// Saveables are captured and restored in registration order, so a slice that depends on another one
    /// (e.g. workers on built plots) can rely on it being restored first.
    /// </remarks>
    public sealed class SaveCoordinator : ITickable, IDisposable
    {
        /// <summary>Autosave interval used when none is given, in seconds.</summary>
        public const float DefaultAutosaveIntervalSeconds = 30f;

        private readonly ISaveService _saveService;
        private readonly IGameLogger _logger;
        private readonly float _autosaveInterval;
        private readonly List<ISaveable> _saveables = new List<ISaveable>();

        private float _sinceLastSave;
        private bool _saveRequested;
        private bool _savingSuspended;
        private bool _disposed;

        /// <summary>Creates the coordinator.</summary>
        /// <param name="saveService">Writes and reads the save slot.</param>
        /// <param name="logger">Receives errors from saveables that fail to capture or restore.</param>
        /// <param name="autosaveIntervalSeconds">Game time between autosaves; must be positive.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="autosaveIntervalSeconds"/> is not positive.</exception>
        public SaveCoordinator(ISaveService saveService, IGameLogger logger, float autosaveIntervalSeconds = DefaultAutosaveIntervalSeconds)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Why: the negated comparison also rejects NaN, which would otherwise disable autosave silently.
            if (!(autosaveIntervalSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(autosaveIntervalSeconds), autosaveIntervalSeconds, "Autosave interval must be positive.");
            }

            _autosaveInterval = autosaveIntervalSeconds;
        }

        /// <summary>Registers <paramref name="saveable"/>. Adding the same instance twice is ignored.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="saveable"/> is null.</exception>
        public void Add(ISaveable saveable)
        {
            if (saveable == null)
            {
                throw new ArgumentNullException(nameof(saveable));
            }

            if (IndexOf(saveable) < 0)
            {
                _saveables.Add(saveable);
            }
        }

        /// <summary>Unregisters <paramref name="saveable"/>; does nothing if it is not registered.</summary>
        public void Remove(ISaveable saveable)
        {
            int index = IndexOf(saveable);
            if (index >= 0)
            {
                _saveables.RemoveAt(index);
            }
        }

        /// <summary>
        /// Loads the save and restores every saveable from it in registration order. Re-enables saving after
        /// <see cref="ResetProgress"/>.
        /// </summary>
        /// <returns>True if a save was loaded; false if there was none (or it was unusable) and nothing was restored.</returns>
        public bool TryRestore()
        {
            _savingSuspended = false;

            if (!_saveService.TryLoad(out SaveData data))
            {
                return false;
            }

            for (int i = 0; i < _saveables.Count; i++)
            {
                try
                {
                    _saveables[i].Restore(data);
                }
                catch (Exception exception)
                {
                    // Why: one broken slice must not leave the rest of the game unrestored.
                    _logger.Error("[Save] " + _saveables[i].GetType().FullName + " failed to restore its state: " + exception);
                }
            }

            return true;
        }

        /// <summary>
        /// Captures every saveable in registration order and writes the save immediately.
        /// Restarts the autosave interval, clears a pending <see cref="RequestSave"/> and re-enables saving after
        /// <see cref="ResetProgress"/>.
        /// </summary>
        /// <remarks>
        /// A saveable that throws is logged and skipped (its slice keeps the empty defaults); the others are still
        /// captured and the save is still written. Failures of the save service are logged, never thrown, because
        /// this runs from the game loop and from application quit.
        /// </remarks>
        public void SaveNow()
        {
            if (_disposed)
            {
                return;
            }

            _savingSuspended = false;
            _saveRequested = false;
            _sinceLastSave = 0f;

            SaveData data = SaveData.CreateEmpty();
            for (int i = 0; i < _saveables.Count; i++)
            {
                try
                {
                    _saveables[i].Capture(data);
                }
                catch (Exception exception)
                {
                    _logger.Error("[Save] " + _saveables[i].GetType().FullName + " failed to capture its state; saving the rest: " + exception);
                }
            }

            try
            {
                _saveService.Save(data);
            }
            catch (Exception exception)
            {
                _logger.Error("[Save] Failed to write the save: " + exception);
            }
        }

        /// <summary>
        /// Asks for a save on the next <see cref="Tick"/>. Any number of requests before that tick produce one save.
        /// Use it after important actions (building, hiring) instead of <see cref="SaveNow"/>, so a burst of actions
        /// in one frame is written once. Ignored after <see cref="ResetProgress"/>.
        /// </summary>
        public void RequestSave()
        {
            if (!_savingSuspended)
            {
                _saveRequested = true;
            }
        }

        /// <summary>
        /// Deletes the save and stops all saving until the next <see cref="TryRestore"/> or <see cref="SaveNow"/>.
        /// </summary>
        /// <remarks>
        /// Why the suspension: the scene is reloaded after a reset, and without it an autosave or a pending request
        /// in between would write the old in-memory progress straight back to disk.
        /// </remarks>
        public void ResetProgress()
        {
            _savingSuspended = true;
            _saveRequested = false;
            _sinceLastSave = 0f;
            _saveService.Delete();
        }

        /// <inheritdoc />
        /// <remarks>
        /// A pending request is served even on a zero delta (paused game). The autosave timer only advances with
        /// game time, so there is no autosave while paused. Allocates nothing when there is nothing to save.
        /// </remarks>
        public void Tick(float deltaTime)
        {
            if (_disposed || _savingSuspended)
            {
                return;
            }

            if (_saveRequested)
            {
                SaveNow();
                return;
            }

            if (deltaTime <= 0f)
            {
                return;
            }

            _sinceLastSave += deltaTime;
            if (_sinceLastSave >= _autosaveInterval)
            {
                SaveNow();
            }
        }

        /// <summary>
        /// Forgets every saveable and stops ticking. Does not save: by disposal time the saveables may already be
        /// disposed, so the final save belongs to the quit / scene-unload handler that runs before it.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _saveRequested = false;
            _saveables.Clear();
        }

        // Why: a manual loop with ReferenceEquals instead of List.Contains, because "the same saveable" means
        // the same instance even if an implementation overrides Equals.
        private int IndexOf(ISaveable saveable)
        {
            for (int i = 0; i < _saveables.Count; i++)
            {
                if (ReferenceEquals(_saveables[i], saveable))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
