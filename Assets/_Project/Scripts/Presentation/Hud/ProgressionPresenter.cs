using System;
using AutoService.Services.Progression;

namespace AutoService.Presentation.Hud
{
    /// <summary>
    /// Writes level and XP progression into a <see cref="ProgressionView"/>. Event-driven: strings are formatted
    /// only when progression changes or upon leveling up, never per frame.
    /// </summary>
    public sealed class ProgressionPresenter : IDisposable
    {
        private readonly IProgressionService _progression;
        private readonly ProgressionView _view;
        private bool _disposed;

        /// <summary>Shows the current progression and starts listening for changes.</summary>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public ProgressionPresenter(IProgressionService progression, ProgressionView view)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));

            _progression.Changed += OnProgressionChanged;
            _progression.LeveledUp += OnLeveledUp;

            UpdateView();
        }

        /// <summary>Stops listening to the progression service. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _progression.Changed -= OnProgressionChanged;
            _progression.LeveledUp -= OnLeveledUp;
        }

        private void OnProgressionChanged()
        {
            UpdateView();
        }

        private void OnLeveledUp(int newLevel)
        {
            UpdateView();
        }

        private void UpdateView()
        {
            if (_view == null)
            {
                return;
            }

            int level = _progression.Level;
            int xp = _progression.Xp;
            int neededForNext = xp + _progression.XpToNextLevel;
            float progress01 = _progression.LevelProgress01;

            _view.SetLevelText($"Lvl {level}");
            _view.SetXpText($"{xp} / {neededForNext} XP");
            _view.SetProgress(progress01);
        }
    }
}
