using System;
using AutoService.Services.Core;

namespace AutoService.Services.Settings
{
    /// <summary>
    /// Default <see cref="ISettingsService"/>: loads the saved settings (or the defaults) on construction
    /// and applies them in <see cref="Initialize"/>.
    /// </summary>
    public sealed class SettingsService : ISettingsService, IInitializable
    {
        private readonly ISettingsStore _store;
        private readonly ISettingsApplier _applier;

        /// <summary>Creates the service and loads the saved settings.</summary>
        /// <param name="store">Persistent settings storage.</param>
        /// <param name="applier">Pushes settings into the engine.</param>
        /// <param name="defaults">Used when nothing has been saved yet (first launch).</param>
        public SettingsService(ISettingsStore store, ISettingsApplier applier, GameSettings defaults)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));

            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            Current = _store.TryLoad(out GameSettings saved) && saved != null ? saved : defaults;
        }

        /// <inheritdoc />
        public event Action<GameSettings> Changed;

        /// <inheritdoc />
        public GameSettings Current { get; private set; }

        /// <summary>Applies the loaded settings to the engine.</summary>
        /// <remarks>
        /// Why not in the constructor: applying touches the engine (resolution, quality), which the entry point
        /// should trigger explicitly once the graph is built; it also keeps construction side-effect free for tests.
        /// </remarks>
        public void Initialize()
        {
            _applier.Apply(Current);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public void Set(GameSettings settings)
        {
            Current = settings ?? throw new ArgumentNullException(nameof(settings));
            _store.Save(settings);
            _applier.Apply(settings);
            Changed?.Invoke(settings);
        }
    }
}
