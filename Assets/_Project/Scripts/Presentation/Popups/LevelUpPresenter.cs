using System;
using System.Collections.Generic;
using AutoService.Services.Config;
using AutoService.Services.Progression;

namespace AutoService.Presentation.Popups
{
    /// <summary>
    /// Shows the <see cref="LevelUpPopupView"/> on every level-up with the buildables that the new level unlocks
    /// (taken from the config, so the popup always matches the real gates). Several level-ups in a row are queued.
    /// </summary>
    public sealed class LevelUpPresenter : IDisposable
    {
        private readonly IProgressionService _progression;
        private readonly LevelUpPopupView _view;
        private readonly IReadOnlyList<BuildableSettings> _buildables;
        private readonly Queue<int> _pendingLevels = new Queue<int>();
        private readonly List<string> _unlocks = new List<string>();
        private readonly Action _onPopupClosed;

        private bool _isShowing;
        private bool _disposed;

        /// <summary>Creates the presenter and subscribes to level-ups.</summary>
        /// <param name="progression">Source of level-ups.</param>
        /// <param name="view">The popup.</param>
        /// <param name="buildables">All buildables of the config; their required levels define what each level unlocks.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public LevelUpPresenter(IProgressionService progression, LevelUpPopupView view, IReadOnlyList<BuildableSettings> buildables)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _buildables = buildables ?? throw new ArgumentNullException(nameof(buildables));
            _onPopupClosed = OnPopupClosed;

            _progression.LeveledUp += OnLeveledUp;
        }

        /// <summary>Fills <paramref name="names"/> with the display names of buildables that require exactly <paramref name="level"/>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when a list is null.</exception>
        public static void CollectUnlocks(IReadOnlyList<BuildableSettings> buildables, int level, List<string> names)
        {
            if (buildables == null)
            {
                throw new ArgumentNullException(nameof(buildables));
            }

            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            names.Clear();
            for (int i = 0; i < buildables.Count; i++)
            {
                if (buildables[i].RequiredLevel == level && !names.Contains(buildables[i].DisplayName))
                {
                    names.Add(buildables[i].DisplayName);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _progression.LeveledUp -= OnLeveledUp;
        }

        private void OnLeveledUp(int newLevel)
        {
            if (_isShowing)
            {
                _pendingLevels.Enqueue(newLevel);
                return;
            }

            ShowLevel(newLevel);
        }

        private void ShowLevel(int level)
        {
            _isShowing = true;
            CollectUnlocks(_buildables, level, _unlocks);
            _view.Show(level, _unlocks, _onPopupClosed);
        }

        private void OnPopupClosed()
        {
            _isShowing = false;
            if (_pendingLevels.Count > 0)
            {
                ShowLevel(_pendingLevels.Dequeue());
            }
        }
    }
}
